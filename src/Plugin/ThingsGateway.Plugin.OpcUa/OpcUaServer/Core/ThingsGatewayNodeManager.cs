//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Mapster;

using Newtonsoft.Json.Linq;

using Opc.Ua;
using Opc.Ua.Server;

using System.Globalization;

using ThingsGateway.Foundation.OpcUa;
using ThingsGateway.Gateway.Application;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.OpcUa;

/// <summary>
/// 数据节点
/// </summary>
public class ThingsGatewayNodeManager : CustomNodeManager2
{
    private const string ReferenceServer = "https://thingsgateway.cn/";

    private readonly TypeAdapterConfig _config;
    /// <summary>
    /// OPC和网关对应表
    /// </summary>
    private readonly Dictionary<string, OpcUaTag> NodeIdTags = new();

    private BusinessBase _businessBase;
    private volatile bool success = true;

    /// <inheritdoc cref="ThingsGatewayNodeManager"/>
    public ThingsGatewayNodeManager(BusinessBase businessBase, IServerInternal server, ApplicationConfiguration configuration) : base(server, configuration, ReferenceServer)
    {
        _businessBase = businessBase;
        _config = new TypeAdapterConfig();
        _config.ForType<IDBHistoryValue, DataValue>()
        .Map(dest => dest.WrappedValue, (src) => new Variant(src.Value))
        .Map(dest => dest.SourceTimestamp, src => DateTime.SpecifyKind(src.CollectTime, DateTimeKind.Local))
        .Map(dest => dest.ServerTimestamp, src => DateTime.SpecifyKind(src.CreateTime, DateTimeKind.Local))
        .Map(dest => dest.StatusCode, (src) =>
        src.IsOnline ? StatusCodes.Good : StatusCodes.Bad);
    }
    ConcurrentList<IDriver>? dbDrivers;
    internal FolderState rootFolder;

    public void RefreshVariable()
    {
        lock (Lock)
        {
            if (rootFolder == null) return;
            rootFolder.RemoveReferences(ReferenceTypes.Organizes, true);
            rootFolder.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
            AddRootNotifier(rootFolder);

            //创建设备树
            var _geviceGroup = _businessBase.IdVariableRuntimes.Select(a => a.Value)
                .GroupBy(a => a.DeviceName);
            // 开始寻找设备信息，并计算一些节点信息
            foreach (var item in _geviceGroup)
            {
                //设备树会有两层
                FolderState fs = CreateFolder(rootFolder, item.Key, item.Key);
                fs.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                fs.EventNotifier = EventNotifiers.SubscribeToEvents;
                if (item?.Count() > 0)
                {
                    foreach (var item2 in item)
                    {
                        CreateVariable(fs, item2);
                    }
                }
            }
            AddPredefinedNode(SystemContext, rootFolder);


        }

    }

    /// <summary>
    /// 创建服务目录结构
    /// </summary>
    /// <param name="externalReferences"></param>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {

            dbDrivers = new(GlobalData.GetEnableDevices().Where(a => a.Driver is IDBHistoryValueService).Select(a => a.Driver));
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference> references))
            {
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            }
            //首节点
            rootFolder = CreateFolder(null, "ThingsGateway", "ThingsGateway");
            references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, rootFolder.NodeId));
            rootFolder.EventNotifier = EventNotifiers.SubscribeToEvents;

            RefreshVariable();
        }
    }

    /// <summary>
    /// 读取历史数据
    /// </summary>
    public override void HistoryRead(OperationContext context,
        HistoryReadDetails details,
        TimestampsToReturn timestampsToReturn,
        bool releaseContinuationPoints,
        IList<HistoryReadValueId> nodesToRead,
        IList<HistoryReadResult> results,
        IList<ServiceResult> errors)
    {
        base.HistoryRead(context, details, timestampsToReturn, releaseContinuationPoints, nodesToRead, results, errors);
        //必须带有时间范围
        if (details is not ReadRawModifiedDetails readDetail || readDetail.StartTime == DateTime.MinValue || readDetail.EndTime == DateTime.MinValue || dbDrivers == null)
        {
            errors[0] = StatusCodes.BadHistoryOperationUnsupported;
            return;
        }

        var startTime = readDetail.StartTime;
        var endTime = readDetail.EndTime;


        for (int i = 0; i < nodesToRead.Count; i++)
        {
            var historyRead = nodesToRead[i];
            if (NodeIdTags.TryGetValue(historyRead.NodeId.Identifier.ToString(), out OpcUaTag tag))
            {
                if (!GlobalData.ReadOnlyIdVariables.TryGetValue(tag.Id, out var variableRuntime))
                {
                    results[i] = new HistoryReadResult()
                    {
                        StatusCode = StatusCodes.GoodNoData
                    };
                    continue;
                }


                var service = dbDrivers.FirstOrDefault(a => GlobalData.ContainsVariable(a.DeviceId, variableRuntime));
                if (service == null)
                {
                    results[i] = new HistoryReadResult()
                    {
                        StatusCode = StatusCodes.BadNotFound
                    };
                    continue;
                }
                var historyValueService = (IDBHistoryValueService)service;

                var data = historyValueService.GetDBHistoryValuesAsync(new DBHistoryValuePageInput()
                {
                    EndTime = endTime,
                    StartTime = startTime,
                    VariableName = tag.SymbolicName
                }).ConfigureAwait(false).GetAwaiter().GetResult();


                if (data.Count > 0)
                {
                    var hisDataValue = data.Adapt<List<DataValue>>(_config);
                    HistoryData hisData = new();
                    hisData.DataValues.AddRange(hisDataValue);
                    errors[i] = StatusCodes.Good;
                    //切记Processed设为true，否则客户端会报错
                    historyRead.Processed = true;
                    results[i] = new HistoryReadResult()
                    {
                        StatusCode = StatusCodes.Good,
                        HistoryData = new ExtensionObject(hisData)
                    };
                }
                else
                {
                    results[i] = new HistoryReadResult()
                    {
                        StatusCode = StatusCodes.GoodNoData
                    };
                }
            }
            else
            {
                results[i] = new HistoryReadResult()
                {
                    StatusCode = StatusCodes.BadNotFound
                };
            }
        }

    }

    /// <summary>
    /// 更新变量
    /// </summary>
    /// <param name="variable"></param>
    public void UpVariable(VariableBasicData variable)
    {
        if (!NodeIdTags.TryGetValue($"{variable.DeviceName}.{variable.Name}", out var uaTag))
            return;
        object initialItemValue = null;
        initialItemValue = variable.Value;
        if (initialItemValue != null)
        {
            var code = variable.IsOnline ? StatusCodes.Good : StatusCodes.Bad;
            if (code == StatusCodes.Good)
            {
                ChangeNodeData(uaTag, initialItemValue, variable.ChangeTime);
            }

            if (uaTag.StatusCode != code)
            {
                uaTag.StatusCode = code;
            }
            uaTag.UpdateChangeMasks(NodeStateChangeMasks.Value);
            uaTag.ClearChangeMasks(SystemContext, false);
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        NodeIdTags.Clear();
        base.Dispose(disposing);
    }

    private static NodeId DataNodeType(Type tp)
    {
        if (tp == typeof(bool))
            return DataTypeIds.Boolean;
        if (tp == typeof(byte))
            return DataTypeIds.Byte;
        if (tp == typeof(sbyte))
            return DataTypeIds.SByte;
        if (tp == typeof(short))
            return DataTypeIds.Int16;
        if (tp == typeof(ushort))
            return DataTypeIds.UInt16;
        if (tp == typeof(int))
            return DataTypeIds.Int32;
        if (tp == typeof(uint))
            return DataTypeIds.UInt32;
        if (tp == typeof(long))
            return DataTypeIds.Int64;
        if (tp == typeof(ulong))
            return DataTypeIds.UInt64;
        if (tp == typeof(float))
            return DataTypeIds.Float;
        if (tp == typeof(double))
            return DataTypeIds.Double;
        if (tp == typeof(string))
            return DataTypeIds.String;
        if (tp == typeof(DateTime))
            return DataTypeIds.DateTime;
        return DataTypeIds.String;
    }

    /// <summary>
    /// 在服务器端直接更改对应数据节点的值
    /// </summary>
    private void ChangeNodeData(OpcUaTag tag, object value, DateTime dateTime)
    {
        object newValue;
        try
        {
            if (!tag.IsDataTypeInit && value != null)
            {
                SetDataType(tag, value);
                SetRank(tag, value);
            }
            var jToken = JToken.FromObject(value is JToken jToken1 ? jToken1.ToString() : value);
            var dataValue = JsonUtils.DecoderObject(
               Server.MessageContext,
           tag.DataType,
                TypeInfo.GetBuiltInType(tag.DataType, SystemContext.TypeTable),
                jToken.CalculateActualValueRank(),
                jToken
                );
            newValue = dataValue;
            success = true;
        }
        catch (Exception ex)
        {
            if (success)
                _businessBase.LogMessage.LogWarning(ex, "Conversion value error");
            success = false;
            newValue = value;
        }
        tag.Value = newValue;
        tag.Timestamp = dateTime;

        void SetDataType(OpcUaTag tag, object value)
        {
            tag.IsDataTypeInit = true;
            var tp = value.GetType();
            if (tp == typeof(JArray))
            {
                try
                {
                    tp = ((JValue)((JArray)value).FirstOrDefault()).Value.GetType();
                    tag.ValueRank = ValueRanks.OneOrMoreDimensions;
                }
                catch
                {
                }
            }
            if (tp == typeof(JValue))
            {
                tp = ((JValue)value).Value.GetType();
                tag.ValueRank = ValueRanks.Scalar;
            }
            tag.DataType = DataNodeType(tp);
            tag.ClearChangeMasks(SystemContext, false);
        }

        void SetRank(OpcUaTag tag, object value)
        {
            tag.IsDataTypeInit = true;
            var tp = value.GetType();
            if (tp == typeof(JArray))
            {
                try
                {
                    tp = ((JValue)((JArray)value).FirstOrDefault()).Value.GetType();
                    tag.ValueRank = ValueRanks.OneOrMoreDimensions;
                }
                catch
                {
                }
            }
            tag.ClearChangeMasks(SystemContext, false);
        }
    }

    /// <summary>
    /// 创建文件夹
    /// </summary>
    private FolderState CreateFolder(NodeState parent, string name, string description)
    {
        FolderState folder = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            Description = description,
            NodeId = new NodeId(name, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);

        return folder;
    }

    /// <summary>
    /// 创建一个值节点，类型需要在创建的时候指定
    /// </summary>
    private OpcUaTag CreateVariable(NodeState parent, VariableRuntime variableRuntime)
    {
        OpcUaTag variable = new(parent)
        {
            SymbolicName = variableRuntime.Name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId($"{variableRuntime.DeviceName}.{variableRuntime.Name}", NamespaceIndex),
            Description = variableRuntime.Description,
            BrowseName = new QualifiedName(variableRuntime.Name, NamespaceIndex),
            DisplayName = new LocalizedText(variableRuntime.Name),
            WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            ValueRank = ValueRanks.Scalar,
            Id = variableRuntime.Id,
            DataType = DataNodeType(variableRuntime)
        };
        var service = dbDrivers.FirstOrDefault(a => GlobalData.ContainsVariable(a.DeviceId, variableRuntime));
        var level = ThingsGatewayNodeManager.ProtectTypeTrans(variableRuntime, service != null);
        variable.AccessLevel = level;
        variable.UserAccessLevel = level;

        variable.Historizing = service != null;//历史存储状态
        variable.Value = Opc.Ua.TypeInfo.GetDefaultValue(variable.DataType, ValueRanks.Any, Server.TypeTree);
        var code = variableRuntime.IsOnline ? StatusCodes.Good : StatusCodes.Bad;
        variable.StatusCode = code;
        variable.Timestamp = variableRuntime.CollectTime ?? DateTime.MinValue;
        variable.OnWriteValue = OnWriteDataValue;
        parent?.AddChild(variable);
        NodeIdTags.AddOrUpdate($"{variableRuntime.DeviceName}.{variableRuntime.Name}", variable);
        return variable;
    }
    #region 多写
    public override void Write(OperationContext context, IList<WriteValue> nodesToWrite, IList<ServiceResult> errors)
    {
        if (nodesToWrite.Any(a => a.AttributeId != Attributes.Value))
        {
            base.Write(context, nodesToWrite, errors);
            return;
        }

        ServerSystemContext systemContext = SystemContext.Copy(context);
        IDictionary<NodeId, NodeState> operationCache = new NodeIdDictionary<NodeState>();
        List<NodeHandle> nodesToValidate = new List<NodeHandle>();

        lock (Lock)
        {
            bool[] writeEnable = new bool[nodesToWrite.Count];
            Dictionary<string, WriteValue> hashSetNodeId = new();

            for (int ii = 0; ii < nodesToWrite.Count; ii++)
            {
                WriteValue nodeToWrite = nodesToWrite[ii];

                // skip items that have already been processed.
                if (nodeToWrite.Processed)
                {
                    continue;
                }

                // check for valid handle.
                NodeHandle handle = GetManagerHandle(systemContext, nodeToWrite.NodeId, operationCache);

                if (handle == null)
                {
                    continue;
                }

                // owned by this node manager.
                nodeToWrite.Processed = true;

                // index range is not supported.
                if (nodeToWrite.AttributeId != Attributes.Value)
                {
                    if (!String.IsNullOrEmpty(nodeToWrite.IndexRange))
                    {
                        errors[ii] = StatusCodes.BadWriteNotSupported;
                        continue;
                    }
                }

                // check if the node is a area in memory.
                if (handle.Node == null)
                {
                    errors[ii] = StatusCodes.BadNodeIdUnknown;

                    // must validate node in a separate operation.
                    handle.Index = ii;
                    nodesToValidate.Add(handle);

                    continue;
                }

                // check if the node is AnalogItem and the values are outside the InstrumentRange.
                AnalogItemState analogItemState = handle.Node as AnalogItemState;
                if (analogItemState != null && analogItemState.InstrumentRange != null)
                {
                    try
                    {
                        if (nodeToWrite.Value.Value is Array array)
                        {
                            bool isOutOfRange = false;
                            foreach (var arrayValue in array)
                            {
                                double newValue = Convert.ToDouble(arrayValue, CultureInfo.InvariantCulture);
                                if (newValue > analogItemState.InstrumentRange.Value.High ||
                                    newValue < analogItemState.InstrumentRange.Value.Low)
                                {
                                    isOutOfRange = true;
                                    break;
                                }
                            }
                            if (isOutOfRange)
                            {
                                errors[ii] = StatusCodes.BadOutOfRange;
                                continue;
                            }
                        }
                        else
                        {
                            double newValue = Convert.ToDouble(nodeToWrite.Value.Value, CultureInfo.InvariantCulture);

                            if (newValue > analogItemState.InstrumentRange.Value.High ||
                                newValue < analogItemState.InstrumentRange.Value.Low)
                            {
                                errors[ii] = StatusCodes.BadOutOfRange;
                                continue;
                            }
                        }
                    }
                    catch
                    {
                        //skip the InstrumentRange check if the transformation isn't possible.
                    }

                }


                writeEnable[ii] = true;
                hashSetNodeId.Add(nodeToWrite.NodeId.Identifier.ToString(), nodeToWrite);
            }
            var tags = NodeIdTags.Where(a => hashSetNodeId.ContainsKey(a.Key));
            List<(VariableRuntime, string)> writeInfos = new();

            foreach (var item in tags)
            {
                if (GlobalData.ReadOnlyIdVariables.TryGetValue(item.Value.Id, out var variableRuntime))
                {
                    writeInfos.Add((variableRuntime, hashSetNodeId[item.Key].Value.Value?.ToString()));
                }
            }

            var writeDatas = writeInfos.GroupBy(a => a.Item1.DeviceName).ToDictionary(a => a.Key, a =>
                a.ToDictionary(a => a.Item1.Name, a => a.Item2)
            );
            var result = GlobalData.RpcService.InvokeDeviceMethodAsync("OpcUaServer - " + context?.Session?.Identity?.DisplayName, writeDatas
            ).GetAwaiter().GetResult(); ;

            for (int ii = 0; ii < nodesToWrite.Count; ii++)
            {
                if (!writeEnable[ii]) continue;

                WriteValue nodeToWrite = nodesToWrite[ii];
                NodeHandle handle = GetManagerHandle(systemContext, nodeToWrite.NodeId, operationCache);
                PropertyState propertyState = handle.Node as PropertyState;
                object previousPropertyValue = null;

                if (propertyState != null)
                {
                    ExtensionObject extension = propertyState.Value as ExtensionObject;
                    if (extension != null)
                    {
                        previousPropertyValue = extension.Body;
                    }
                    else
                    {
                        previousPropertyValue = propertyState.Value;
                    }
                }

                DataValue oldValue = null;

                if (Server?.Auditing == true)
                {
                    //current server supports auditing 
                    oldValue = new DataValue();
                    // read the old value for the purpose of auditing
                    handle.Node.ReadAttribute(systemContext, nodeToWrite.AttributeId, nodeToWrite.ParsedIndexRange, null, oldValue);
                }


                if (NodeIdTags.TryGetValue(nodeToWrite.NodeId.Identifier.ToString(), out OpcUaTag tag) && GlobalData.ReadOnlyIdVariables.TryGetValue(tag.Id, out var variableRuntime) && result.TryGetValue(variableRuntime.DeviceName, out var deviceResult) && deviceResult.TryGetValue(variableRuntime.Name, out var operResult))
                {
                    if (operResult.IsSuccess == true)
                    {
                        errors[ii] = StatusCodes.Good;
                    }
                    else
                    {
                        errors[ii] = new(StatusCodes.BadWaitingForResponse, operResult.ToString());
                    }
                    // write the attribute value.
                }



                // report the write value audit event 
                Server.ReportAuditWriteUpdateEvent(systemContext, nodeToWrite, oldValue?.Value, errors[ii]?.StatusCode ?? StatusCodes.Good);

                if (!ServiceResult.IsGood(errors[ii]))
                {
                    continue;
                }

                if (propertyState != null)
                {
                    object propertyValue;
                    ExtensionObject extension = nodeToWrite.Value.Value as ExtensionObject;

                    if (extension != null)
                    {
                        propertyValue = extension.Body;
                    }
                    else
                    {
                        propertyValue = nodeToWrite.Value.Value;
                    }

                    CheckIfSemanticsHaveChanged(systemContext, propertyState, propertyValue, previousPropertyValue);
                }

                // updates to source finished - report changes to monitored items.
                handle.Node.ClearChangeMasks(systemContext, true);
            }

            // check for nothing to do.
            if (nodesToValidate.Count == 0)
            {
                return;
            }
        }

        // validates the nodes and writes the value to the underlying system.
        Write(
            systemContext,
            nodesToWrite,
            errors,
            nodesToValidate,
            operationCache);
    }
    private ServiceResult OnWriteDataValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding, ref object value, ref StatusCode statusCode, ref DateTime timestamp)
    {
        try
        {
            var context1 = context as ServerSystemContext;

            //取消注释，插件不限制匿名用户的写入
            //if (context1.UserIdentity.TokenType == UserTokenType.Anonymous)
            //{
            //    return StatusCodes.BadUserAccessDenied;
            //}
            OpcUaTag opcuaTag = node as OpcUaTag;
            if (NodeIdTags.TryGetValue(opcuaTag.NodeId.Identifier.ToString(), out OpcUaTag tag) && GlobalData.ReadOnlyIdVariables.TryGetValue(tag.Id, out var variableRuntime))
            {
                if (StatusCode.IsGood(opcuaTag.StatusCode))
                {
                    //仅当指定了值时才将值写入
                    if (opcuaTag.Value != null)
                    {
                        var result = GlobalData.RpcService.InvokeDeviceMethodAsync("OpcUaServer - " + context1?.OperationContext?.Session?.Identity?.DisplayName,
                            new()
                            {
                                {
                                    variableRuntime.DeviceName,   new Dictionary<string, string>() { {opcuaTag.SymbolicName, value?.ToString() } }
                                }
                            }
                            ).GetAwaiter().GetResult();
                        if (result.Values.FirstOrDefault()?.FirstOrDefault().Value.IsSuccess == true)
                        {
                            return StatusCodes.Good;
                        }
                        else
                        {
                            return new(StatusCodes.BadWaitingForResponse, result.Values.FirstOrDefault()?.FirstOrDefault().Value.ToString());
                        }
                    }
                }
            }
            return StatusCodes.BadWaitingForResponse;
        }
        catch
        {
            return StatusCodes.BadTypeMismatch;
        }
    }

    private void CheckIfSemanticsHaveChanged(ServerSystemContext systemContext, PropertyState property, object newPropertyValue, object previousPropertyValue)
    {
        // check if the changed property is one that can trigger semantic changes
        string propertyName = property.BrowseName.Name;

        if (propertyName != BrowseNames.EURange &&
            propertyName != BrowseNames.InstrumentRange &&
            propertyName != BrowseNames.EngineeringUnits &&
            propertyName != BrowseNames.Title &&
            propertyName != BrowseNames.AxisDefinition &&
            propertyName != BrowseNames.FalseState &&
            propertyName != BrowseNames.TrueState &&
            propertyName != BrowseNames.EnumStrings &&
            propertyName != BrowseNames.XAxisDefinition &&
            propertyName != BrowseNames.YAxisDefinition &&
            propertyName != BrowseNames.ZAxisDefinition)
        {
            return;
        }

        //look for the Parent and its monitoring items
        foreach (var monitoredNode in MonitoredNodes.Values)
        {
            var propertyState = monitoredNode.Node.FindChild(systemContext, property.BrowseName);

            if (propertyState != null && property != null && propertyState.NodeId == property.NodeId && !Utils.IsEqual(newPropertyValue, previousPropertyValue))
            {
                foreach (var monitoredItem in monitoredNode.DataChangeMonitoredItems)
                {
                    if (monitoredItem.AttributeId == Attributes.Value)
                    {
                        NodeState node = monitoredNode.Node;

                        if ((node is AnalogItemState && (propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits)) ||
                            (node is TwoStateDiscreteState && (propertyName == BrowseNames.FalseState || propertyName == BrowseNames.TrueState)) ||
                            (node is MultiStateDiscreteState && (propertyName == BrowseNames.EnumStrings)) ||
                            (node is ArrayItemState && (propertyName == BrowseNames.InstrumentRange || propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits || propertyName == BrowseNames.Title)) ||
                            ((node is YArrayItemState || node is XYArrayItemState) && (propertyName == BrowseNames.InstrumentRange || propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits || propertyName == BrowseNames.Title || propertyName == BrowseNames.XAxisDefinition)) ||
                            (node is ImageItemState && (propertyName == BrowseNames.InstrumentRange || propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits || propertyName == BrowseNames.Title || propertyName == BrowseNames.XAxisDefinition || propertyName == BrowseNames.YAxisDefinition)) ||
                            (node is CubeItemState && (propertyName == BrowseNames.InstrumentRange || propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits || propertyName == BrowseNames.Title || propertyName == BrowseNames.XAxisDefinition || propertyName == BrowseNames.YAxisDefinition || propertyName == BrowseNames.ZAxisDefinition)) ||
                            (node is NDimensionArrayItemState && (propertyName == BrowseNames.InstrumentRange || propertyName == BrowseNames.EURange || propertyName == BrowseNames.EngineeringUnits || propertyName == BrowseNames.Title || propertyName == BrowseNames.AxisDefinition)))
                        {
                            monitoredItem.SetSemanticsChanged();

                            DataValue value = new DataValue();
                            value.ServerTimestamp = DateTime.UtcNow;

                            monitoredNode.Node.ReadAttribute(systemContext, Attributes.Value, monitoredItem.IndexRange, null, value);

                            monitoredItem.QueueValue(value, ServiceResult.Good, true);
                        }
                    }
                }
            }
        }
    }


    #endregion 多写
    /// <summary>
    /// 网关转OPC数据类型
    /// </summary>
    /// <param name="variableRuntime"></param>
    /// <returns></returns>
    private NodeId DataNodeType(VariableRuntime variableRuntime)
    {
        var str = variableRuntime.GetPropertyValue(_businessBase.DeviceId, nameof(OpcUaServerVariableProperty.DataType)) ?? "";
        Type tp;
        if (Enum.TryParse(str, out DataTypeEnum result))
        {
            tp = result.GetSystemType();
        }
        else
        {
            tp = variableRuntime.Value?.GetType() ?? variableRuntime.DataType.GetSystemType(); ;
        }

        return DataNodeType(tp);
    }


    private static byte ProtectTypeTrans(VariableRuntime variableRuntime, bool historizing)
    {
        byte result = 0;
        result = variableRuntime.ProtectType switch
        {
            ProtectTypeEnum.ReadOnly => (byte)(result | AccessLevels.CurrentRead),
            ProtectTypeEnum.ReadWrite => (byte)(result | AccessLevels.CurrentReadOrWrite),
            _ => (byte)(result | AccessLevels.CurrentRead),
        };
        if (historizing)
        {
            result = (byte)(result | AccessLevels.HistoryRead);
        }
        return result;
    }
}
