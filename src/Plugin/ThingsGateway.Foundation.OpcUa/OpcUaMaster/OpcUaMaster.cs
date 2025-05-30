﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

//修改自https://github.com/dathlin/OpcUaHelper 与OPC基金会net库

using System.Collections.Concurrent;

namespace ThingsGateway.Foundation.OpcUa;

/// <summary>
/// 订阅委托
/// </summary>
/// <param name="value"></param>
public delegate void DataChangedEventHandler((VariableNode variableNode, MonitoredItem monitoredItem, DataValue dataValue, JToken jToken) value);

/// <summary>
/// 日志输出
/// </summary>
public delegate void LogEventHandler(byte level, object sender, string message, Exception ex);

/// <summary>
/// OpcUaMaster
/// </summary>
public class OpcUaMaster : IDisposable
{
    #region 属性，变量等

    /// <summary>
    /// Raised after the client status change
    /// </summary>
    public LogEventHandler LogEvent;

    /// <summary>
    /// 当前配置
    /// </summary>
    public OpcUaProperty OpcUaProperty;

    /// <summary>
    /// ProductUri
    /// </summary>
    public string ProductUri = "https://thingsgateway.cn/";

    /// <summary>
    /// 当前的变量名称/OPC变量节点
    /// </summary>
    private readonly ConcurrentDictionary<string, VariableNode> _variableDicts = new();

    private readonly object checkLock = new();

    /// <summary>
    /// 当前的订阅组，组名称/组
    /// </summary>
    private readonly ConcurrentDictionary<string, Subscription> _subscriptionDicts = new();

    private readonly ApplicationInstance m_application = new();

    private readonly ApplicationConfiguration m_configuration;
    private EventHandler<bool> m_ConnectComplete;
    private EventHandler<KeepAliveEventArgs> m_KeepAliveComplete;
    private EventHandler m_ReconnectComplete;
    private SessionReconnectHandler m_reConnectHandler;
    private EventHandler m_ReconnectStarting;
    private ISession m_session;

    private ComplexTypeSystem typeSystem;

    /// <summary>
    /// 默认的构造函数，实例化一个新的OPC UA类
    /// </summary>
    public OpcUaMaster()
    {
        var certificateValidator = new CertificateValidator();
        certificateValidator.CertificateValidation += CertificateValidation;
        // 构建应用程序配置
        m_configuration = new ApplicationConfiguration
        {
            ApplicationName = OPCUAName,
            ApplicationType = ApplicationType.Client,
            CertificateValidator = certificateValidator,
            ApplicationUri = Utils.Format(@"urn:{0}:{1}", System.Net.Dns.GetHostName(), OPCUAName),
            ProductUri = ProductUri,

            ServerConfiguration = new ServerConfiguration
            {
                MaxSubscriptionCount = 100000,
                MaxMessageQueueSize = 1000000,
                MaxNotificationQueueSize = 1000000,
                MaxPublishRequestCount = 10000000,
            },

            SecurityConfiguration = new SecurityConfiguration
            {
                UseValidatedCertificates = true,
                AutoAcceptUntrustedCertificates = true,//自动接受证书
                RejectSHA1SignedCertificates = true,
                AddAppCertToTrustedStore = true,
                SendCertificateChain = true,
                MinimumCertificateKeySize = 1024,
                SuppressNonceValidationErrors = true,

                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.X509Store,
                    StorePath = "CurrentUser\\" + OPCUAName,
                    SubjectName = $"CN={OPCUAName}, C=CN, S=GUANGZHOU, O=ThingsGateway, DC=" + System.Net.Dns.GetHostName(),
                },

                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = AppContext.BaseDirectory + @"OPCUAClientCertificate\pki\trustedIssuer",
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = AppContext.BaseDirectory + @"OPCUAClientCertificate\pki\trustedPeer",
                },
                RejectedCertificateStore = new CertificateStoreIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = AppContext.BaseDirectory + @"OPCUAClientCertificate\pki\rejected",
                },
                UserIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = AppContext.BaseDirectory + @"OPCUAClientCertificate\pki\issuerUser",
                },
                TrustedUserCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = AppContext.BaseDirectory + @"OPCUAClientCertificate\pki\trustedUser",
                }
            },

            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 6000000,
                MaxStringLength = int.MaxValue,
                MaxByteStringLength = int.MaxValue,
                MaxArrayLength = 65535,
                MaxMessageSize = 419430400,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = 60000,
                MinSubscriptionLifetime = 10000,

            },

        };

        certificateValidator.Update(m_configuration);

        m_configuration.Validate(ApplicationType.Client);
        m_application.ApplicationConfiguration = m_configuration;
    }

    /// <summary>
    /// Raised after successfully connecting to or disconnecing from a server.
    /// </summary>
    public event EventHandler<bool> ConnectComplete
    {
        add { m_ConnectComplete += value; }
        remove { m_ConnectComplete -= value; }
    }

    /// <summary>
    /// 订阅
    /// </summary>
    public event DataChangedEventHandler DataChangedHandler;

    /// <summary>
    /// Raised when a good keep alive from the server arrives.
    /// </summary>
    public event EventHandler<KeepAliveEventArgs> KeepAliveComplete
    {
        add { m_KeepAliveComplete += value; }
        remove { m_KeepAliveComplete -= value; }
    }

    /// <summary>
    /// Raised when a reconnect operation completes.
    /// </summary>
    public event EventHandler ReconnectComplete
    {
        add { m_ReconnectComplete += value; }
        remove { m_ReconnectComplete -= value; }
    }

    /// <summary>
    /// Raised when a reconnect operation starts.
    /// </summary>
    public event EventHandler ReconnectStarting
    {
        add { m_ReconnectStarting += value; }
        remove { m_ReconnectStarting -= value; }
    }

    /// <summary>
    /// 配置信息
    /// </summary>
    public ApplicationConfiguration AppConfig => m_configuration;

    /// <summary>
    /// 连接状态
    /// </summary>
    public bool Connected => m_session?.Connected == true;

    /// <summary>
    /// OpcUaMaster
    /// </summary>
    public string OPCUAName { get; set; } = "ThingsGateway";

    /// <summary>
    /// SessionReconnectHandler
    /// </summary>
    public SessionReconnectHandler ReConnectHandler => m_reConnectHandler;

    /// <summary>
    /// 当前活动会话。
    /// </summary>
    public ISession Session => m_session;

    #endregion 属性，变量等

    #region 订阅

    /// <summary>
    /// 新增订阅，需要指定订阅组名称，订阅的tag名数组
    /// </summary>
    public async Task AddSubscriptionAsync(string subscriptionName, string[] items, bool loadType = true, CancellationToken cancellationToken = default)
    {
        Subscription m_subscription = new(m_session.DefaultSubscription)
        {
            PublishingEnabled = true,
            PublishingInterval = 0,
            KeepAliveCount = uint.MaxValue,
            LifetimeCount = uint.MaxValue,
            MaxNotificationsPerPublish = uint.MaxValue,
            Priority = 100,
            DisplayName = subscriptionName
        };
        List<MonitoredItem> monitoredItems = new();
        var variableNodes = loadType ? await ReadNodesAsync(items, false, cancellationToken).ConfigureAwait(false) : null;
        for (int i = 0; i < items.Length; i++)
        {
            try
            {
                var item = new MonitoredItem
                {
                    StartNodeId = items[i],
                    AttributeId = Attributes.Value,
                    DisplayName = items[i],
                    Filter = OpcUaProperty.DeadBand == 0 ? null : new DataChangeFilter() { DeadbandValue = OpcUaProperty.DeadBand, DeadbandType = (int)DeadbandType.Absolute, Trigger = DataChangeTrigger.StatusValue },
                    SamplingInterval = OpcUaProperty?.UpdateRate ?? 1000,
                };
                item.Notification += Callback;
                monitoredItems.Add(item);
            }
            catch (Exception ex)
            {
                Log(3, ex, $"Failed to initialize {items[i]} variable subscription");
            }
        }

        m_session.AddSubscription(m_subscription);
        await m_subscription.CreateAsync(cancellationToken).ConfigureAwait(false);

        m_subscription.AddItems(monitoredItems);
        foreach (var item in m_subscription.MonitoredItems.Where(a => a.Status.Error != null && StatusCode.IsBad(a.Status.Error.StatusCode)))
        {
            item.Filter = OpcUaProperty.DeadBand == 0 ? null : new DataChangeFilter() { DeadbandValue = OpcUaProperty.DeadBand, DeadbandType = (int)DeadbandType.None, Trigger = DataChangeTrigger.StatusValue };
        }

        await m_subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
        var isError = m_subscription.MonitoredItems.Any(a => a.Status.Error != null && StatusCode.IsBad(a.Status.Error.StatusCode));
        if (isError)
        {
            Log(3, null, $"Failed to create subscription for the following variables：{Environment.NewLine}{m_subscription.MonitoredItems.Where(
                a => a.Status.Error != null && StatusCode.IsBad(a.Status.Error.StatusCode))
                .Select(a => $"{a.StartNodeId}：{a.Status.Error}").ToJsonString()}");
        }

        if (_subscriptionDicts.TryAdd(subscriptionName, m_subscription))
        {

        }
        else if (_subscriptionDicts.TryGetValue(subscriptionName, out var existingSubscription))
        {
            // remove
            existingSubscription.Delete(true);
            await m_session.RemoveSubscriptionAsync(existingSubscription, cancellationToken).ConfigureAwait(false);
            try { existingSubscription.Dispose(); } catch { }
            _subscriptionDicts[subscriptionName] = m_subscription;
        }
    }

    /// <summary>
    /// 浏览一个节点的引用
    /// </summary>
    /// <param name="tag">节点值</param>
    /// <returns>引用节点描述</returns>
    public async Task<ReferenceDescription[]> BrowseNodeReferenceAsync(string tag)
    {
        NodeId sourceId = new(tag);

        // 该节点可以读取到方法
        BrowseDescription nodeToBrowse1 = new()
        {
            NodeId = sourceId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.Aggregates,
            IncludeSubtypes = true,
            NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method),
            ResultMask = (uint)BrowseResultMask.All
        };

        // find all nodes organized by the node.
        BrowseDescription nodeToBrowse2 = new()
        {
            NodeId = sourceId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.Organizes,
            IncludeSubtypes = true,
            NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
            ResultMask = (uint)BrowseResultMask.All
        };

        BrowseDescriptionCollection nodesToBrowse = new()
        {
            nodeToBrowse1,
            nodeToBrowse2
        };

        // fetch references from the server.
        ReferenceDescriptionCollection references = await OpcUaUtils.BrowseAsync(m_session, nodesToBrowse, false).ConfigureAwait(false);

        return references.ToArray();
    }

    /// <summary>
    /// 调用服务器的方法
    /// </summary>
    /// <param name="tagParent">方法的父节点tag</param>
    /// <param name="tag">方法的节点tag</param>
    /// <param name="args">传递的参数</param>
    /// <returns>输出的结果值</returns>
    public object[] CallMethodByNodeId(string tagParent, string tag, params object[] args)
    {
        if (m_session == null)
        {
            return null;
        }

        IList<object> outputArguments = m_session.Call(
            new NodeId(tagParent),
            new NodeId(tag),
            args);

        return outputArguments.ToArray();
    }

    /// <summary>
    /// 连接到服务器
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await ConnectAsync(OpcUaProperty.OpcUrl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 断开连接。
    /// </summary>
    public void Disconnect()
    {
        PrivateDisconnect();
        // disconnect any existing session.
        if (m_session != null)
        {
            m_session = null;
        }

    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Disconnect();
        _variableDicts?.Clear();
        _subscriptionDicts?.Clear();
    }

    /// <summary>
    /// 获取变量说明
    /// </summary>
    /// <returns></returns>
    public string GetAddressDescription()
    {
        return "ItemId";
    }

    /// <summary>
    /// 读取历史数据
    /// </summary>
    /// <param name="tag">节点的索引</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <param name="count">读取的个数</param>
    /// <param name="containBound">是否包含边界</param>
    /// <param name="cancellationToken">cancellationToken</param>
    /// <returns>读取的数据列表</returns>
    public async Task<List<DataValue>> ReadHistoryRawDataValues(string tag, DateTime start, DateTime end, uint count = 1, bool containBound = false, CancellationToken cancellationToken = default)
    {
        HistoryReadValueId m_nodeToContinue = new()
        {
            NodeId = new NodeId(tag),
        };

        ReadRawModifiedDetails m_details = new()
        {
            StartTime = start,
            EndTime = end,
            NumValuesPerNode = count,
            IsReadModified = false,
            ReturnBounds = containBound
        };

        HistoryReadValueIdCollection nodesToRead = new()
        {
            m_nodeToContinue
        };

        var result = await m_session.HistoryReadAsync(
             null,
             new ExtensionObject(m_details),
             TimestampsToReturn.Both,
             false,
             nodesToRead,
             cancellationToken).ConfigureAwait(false);
        var results = result.Results;
        var diagnosticInfos = result.DiagnosticInfos;
        ClientBase.ValidateResponse(results, nodesToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

        if (StatusCode.IsBad(results[0].StatusCode))
        {
            throw new ServiceResultException(results[0].StatusCode);
        }

        HistoryData values = ExtensionObject.ToEncodeable(results[0].HistoryData) as HistoryData;
        return values.DataValues;
    }

    /// <summary>
    /// 从服务器读取值
    /// </summary>
    public async Task<List<(string, DataValue, JToken)>> ReadJTokenValueAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        var result = await ReadJTokenValueAsync(tags.Select(a => new NodeId(a)).ToArray(), cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 读取一个节点的所有属性
    /// </summary>
    public async Task<List<OPCNodeAttribute>> ReadNoteAttributeAsync(string tag, uint attributesId, CancellationToken cancellationToken = default)
    {
        BrowseDescriptionCollection nodesToBrowse = new();
        ReadValueIdCollection nodesToRead = new();
        NodeId sourceId = new(tag);

        ReadValueId nodeToRead = new()
        {
            NodeId = sourceId,
            AttributeId = attributesId
        };
        nodesToRead.Add(nodeToRead);
        BrowseDescription nodeToBrowse = new()
        {
            NodeId = sourceId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            IncludeSubtypes = true,
            NodeClassMask = 0,
            ResultMask = (uint)BrowseResultMask.All
        };
        nodesToBrowse.Add(nodeToBrowse);

        var result1 = await ReadNoteAttributeAsync(nodesToBrowse, nodesToRead, cancellationToken).ConfigureAwait(false);

        var result2 = result1.Values.FirstOrDefault();
        return result2;
    }

    /// <summary>
    /// 读取节点的所有属性
    /// </summary>
    public async Task<Dictionary<string, List<OPCNodeAttribute>>> ReadNoteAttributeAsync(List<string> tags, CancellationToken cancellationToken)
    {
        BrowseDescriptionCollection nodesToBrowse = new();
        ReadValueIdCollection nodesToRead = new();
        foreach (var tag in tags)
        {
            NodeId sourceId = new(tag);

            for (uint ii = Attributes.NodeClass; ii <= Attributes.UserExecutable; ii++)
            {
                ReadValueId nodeToRead = new()
                {
                    NodeId = sourceId,
                    AttributeId = ii
                };
                nodesToRead.Add(nodeToRead);
            }
            BrowseDescription nodeToBrowse = new()
            {
                NodeId = sourceId,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HasProperty,
                IncludeSubtypes = true,
                NodeClassMask = 0,
                ResultMask = (uint)BrowseResultMask.All
            };
            nodesToBrowse.Add(nodeToBrowse);
        }

        return await ReadNoteAttributeAsync(nodesToBrowse, nodesToRead, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 读取一个节点的所有属性
    /// </summary>
    /// <param name="tag">节点信息</param>
    /// <returns>节点的特性值</returns>
    public OPCNodeAttribute[] ReadNoteAttributes(string tag)
    {
        NodeId sourceId = new(tag);
        ReadValueIdCollection nodesToRead = new();

        for (uint ii = Attributes.NodeClass; ii <= Attributes.UserExecutable; ii++)
        {
            ReadValueId nodeToRead = new()
            {
                NodeId = sourceId,
                AttributeId = ii
            };
            nodesToRead.Add(nodeToRead);
        }

        int startOfProperties = nodesToRead.Count;

        // find all of the pror of the node.
        BrowseDescription nodeToBrowse1 = new()
        {
            NodeId = sourceId,
            BrowseDirection = BrowseDirection.Forward,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            IncludeSubtypes = true,
            NodeClassMask = 0,
            ResultMask = (uint)BrowseResultMask.All
        };

        BrowseDescriptionCollection nodesToBrowse = new()
        {
            nodeToBrowse1
        };

        // fetch property references from the server.
        ReferenceDescriptionCollection references = OpcUaUtils.Browse(m_session, nodesToBrowse, false);

        if (references == null)
        {
            return Array.Empty<OPCNodeAttribute>();
        }

        for (int ii = 0; ii < references.Count; ii++)
        {
            // ignore external references.
            if (references[ii].NodeId.IsAbsolute)
            {
                continue;
            }

            ReadValueId nodeToRead = new()
            {
                NodeId = (NodeId)references[ii].NodeId,
                AttributeId = Attributes.Value
            };
            nodesToRead.Add(nodeToRead);
        }

        // read all values.

        m_session.Read(
            null,
            0,
            TimestampsToReturn.Neither,
            nodesToRead,
            out DataValueCollection results,
            out DiagnosticInfoCollection diagnosticInfos);

        ClientBase.ValidateResponse(results, nodesToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

        // process results.

        List<OPCNodeAttribute> nodeAttribute = new();
        for (int ii = 0; ii < results.Count; ii++)
        {
            OPCNodeAttribute item = new();

            // process attribute value.
            if (ii < startOfProperties)
            {
                // ignore attributes which are invalid for the node.
                if (results[ii].StatusCode == StatusCodes.BadAttributeIdInvalid)
                {
                    continue;
                }

                // get the name of the attribute.
                item.Name = Attributes.GetBrowseName(nodesToRead[ii].AttributeId);

                // display any unexpected error.
                if (StatusCode.IsBad(results[ii].StatusCode))
                {
                    item.Type = Utils.Format("{0}", Attributes.GetDataTypeId(nodesToRead[ii].AttributeId));
                    item.Value = Utils.Format("{0}", results[ii].StatusCode);
                }

                // display the value.
                else
                {
                    TypeInfo typeInfo = TypeInfo.Construct(results[ii].Value);

                    item.Type = typeInfo.BuiltInType.ToString();

                    if (typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions)
                    {
                        item.Type += "[]";
                    }

                    item.Value = results[ii].Value;//Utils.Format("{0}", results[ii].Value);
                }
            }

            // process property value.
            else
            {
                // ignore properties which are invalid for the node.
                if (results[ii].StatusCode == StatusCodes.BadNodeIdUnknown)
                {
                    continue;
                }

                // get the name of the property.
                item.Name = Utils.Format("{0}", references[ii - startOfProperties]);

                // display any unexpected error.
                if (StatusCode.IsBad(results[ii].StatusCode))
                {
                    item.Type = String.Empty;
                    item.Value = Utils.Format("{0}", results[ii].StatusCode);
                }

                // display the value.
                else
                {
                    TypeInfo typeInfo = TypeInfo.Construct(results[ii].Value);

                    item.Type = typeInfo.BuiltInType.ToString();

                    if (typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions)
                    {
                        item.Type += "[]";
                    }

                    item.Value = results[ii].Value;
                }
            }

            nodeAttribute.Add(item);
        }

        return nodeAttribute.ToArray();
    }

    /// <summary>
    /// 移除所有的订阅消息
    /// </summary>
    public async Task RemoveAllSubscriptionAsync()
    {
        foreach (var item in _subscriptionDicts)
        {
            item.Value.Delete(true);
            await m_session.RemoveSubscriptionAsync(item.Value).ConfigureAwait(false);
            try { item.Value.Dispose(); } catch { }
        }
        _subscriptionDicts.Clear();
    }

    /// <summary>
    /// 移除订阅消息
    /// </summary>
    /// <param name="subscriptionName">组名称</param>
    public async Task RemoveSubscriptionAsync(string subscriptionName)
    {

        if (_subscriptionDicts.TryGetValue(subscriptionName, out var subscription))
        {
            // remove
            subscription.Delete(true);
            await m_session.RemoveSubscriptionAsync(subscription).ConfigureAwait(false);
            try { subscription.Dispose(); } catch { }
            _subscriptionDicts.TryRemove(subscriptionName, out _);
        }

    }

    /// <summary>
    /// 异步写opc标签
    /// </summary>
    public async Task<Dictionary<string, Tuple<bool, string>>> WriteNodeAsync(Dictionary<string, JToken> writeInfoLists, CancellationToken cancellationToken = default)
    {
        Dictionary<string, Tuple<bool, string>> results = new();
        try
        {
            WriteValueCollection valuesToWrite = new();
            foreach (var item in writeInfoLists)
            {
                WriteValue valueToWrite = new()
                {
                    NodeId = new NodeId(item.Key),
                    AttributeId = Attributes.Value,
                };
                var variableNode = await ReadNodeAsync(item.Key, false, false, cancellationToken).ConfigureAwait(false);
                var dataValue = JsonUtils.Decode(
                    m_session.MessageContext,
                    variableNode.DataType,
                    TypeInfo.GetBuiltInType(variableNode.DataType, m_session.SystemContext.TypeTable),
                    item.Value.CalculateActualValueRank(),
                    item.Value
                    );
                valueToWrite.Value = dataValue;

                valuesToWrite.Add(valueToWrite);
            }

            var result = await m_session.WriteAsync(
     requestHeader: null,
     nodesToWrite: valuesToWrite, cancellationToken).ConfigureAwait(false);

            ClientBase.ValidateResponse(result.Results, valuesToWrite);
            ClientBase.ValidateDiagnosticInfos(result.DiagnosticInfos, valuesToWrite);

            var keys = writeInfoLists.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!StatusCode.IsGood(result.Results[i]))
                    results.Add(keys[i], Tuple.Create(false, result.Results[i].ToString()));
                else
                {
                    results.Add(keys[i], Tuple.Create(true, "Success"));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            var keys = writeInfoLists.Keys.ToList();
            foreach (var item in keys)
            {
                results.Add(item, Tuple.Create(false, ex.Message));
            }
            return results;
        }
    }

    private void Callback(MonitoredItem monitoreditem, MonitoredItemNotificationEventArgs monitoredItemNotificationEventArgs)
    {
        try
        {
            if (m_session != null)
            {
                foreach (var value in monitoreditem.DequeueValues())
                {
                    var variableNode = ReadNode(monitoreditem.StartNodeId.ToString(), false, StatusCode.IsGood(value.StatusCode));

                    if (value.Value != null)
                    {
                        var data = JsonUtils.Encode(m_session.MessageContext, TypeInfo.GetBuiltInType(variableNode.DataType, m_session.SystemContext.TypeTable), value.Value);
                        if (data == null && value.Value != null)
                        {
                            Log(3, null, $"{monitoreditem.StartNodeId}Conversion error, original value is{value.Value}");
                            var data1 = JsonUtils.Encode(m_session.MessageContext, TypeInfo.GetBuiltInType(variableNode.DataType, m_session.SystemContext.TypeTable), value.Value);
                        }
                        DataChangedHandler?.Invoke((variableNode, monitoreditem, value, data!));
                    }
                    else
                    {
                        var data = JValue.CreateNull();
                        DataChangedHandler?.Invoke((variableNode, monitoreditem, value, data));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log(3, ex, $"{monitoreditem.StartNodeId} - Subscription processing error");
        }
    }

    #endregion 订阅

    #region 连接

    /// <summary>
    /// 检查/创建证书
    /// </summary>
    /// <returns></returns>
    public async Task CheckApplicationInstanceCertificate()
    {
        await m_application.CheckApplicationInstanceCertificates(true, 1200).ConfigureAwait(false);
    }

    SemaphoreSlim waitLock = new(1, 1);

    private string LastServerUrl { get; set; }
    /// <summary>
    /// Creates a new session.
    /// </summary>
    /// <returns>The new session object.</returns>
    private async Task ConnectAsync(string serverUrl, CancellationToken cancellationToken)
    {
        if (m_session != null)
        {
            return;
        }
        try
        {

            await waitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (m_session != null)
            {
                return;
            }
            PrivateDisconnect();
            if (LastServerUrl != serverUrl)
            {
                _variableDicts.Clear();
            }
            LastServerUrl = serverUrl;
            if (m_configuration == null)
            {
                throw new ArgumentNullException(nameof(m_configuration));
            }
            var useSecurity = OpcUaProperty?.UseSecurity ?? true;

            EndpointDescription endpointDescription = await OpcUaUtils.SelectEndpointAsync(m_configuration, serverUrl, useSecurity, 10000).ConfigureAwait(false);
            EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);
            ConfiguredEndpoint endpoint = new(null, endpointDescription, endpointConfiguration);
            UserIdentity userIdentity;
            if (!string.IsNullOrEmpty(OpcUaProperty.UserName))
            {
                userIdentity = new UserIdentity(OpcUaProperty.UserName, OpcUaProperty.Password);
            }
            else
            {
                userIdentity = new UserIdentity(new AnonymousIdentityToken());
            }
            //创建本地证书
            if (useSecurity)
                await m_application.CheckApplicationInstanceCertificates(true, 1200, cancellationToken).ConfigureAwait(false);

            m_session = await Opc.Ua.Client.Session.Create(

            m_configuration,
            endpoint,
            false,
            OpcUaProperty.CheckDomain,
            (string.IsNullOrEmpty(OPCUAName)) ? m_configuration.ApplicationName : OPCUAName,
            600000,
            userIdentity,
            null, cancellationToken
            ).ConfigureAwait(false);

            m_session.KeepAliveInterval = OpcUaProperty.KeepAliveInterval == 0 ? 60000 : OpcUaProperty.KeepAliveInterval;
            m_session.KeepAlive += Session_KeepAlive;

            typeSystem = new ComplexTypeSystem(m_session);


            // raise an event.
            DoConnectComplete(true);

            Log(2, null, "Connected");


        }
        finally
        {
            waitLock.Release();
        }
    }

    private void PrivateDisconnect()
    {
        bool state = m_session?.Connected == true;

        if (m_reConnectHandler != null)
        {
            try { m_reConnectHandler.Dispose(); } catch { }
            m_reConnectHandler = null;
        }
        if (m_session != null)
        {
            m_session.KeepAlive -= Session_KeepAlive;
            m_session.Close(10000);
            m_session.Dispose();
            m_session = null;
        }

        if (state)
        {
            Log(2, null, "Disconnected");
            DoConnectComplete(false);
        }
    }

    #endregion 连接

    #region 读取/写入

    /// <summary>
    /// 从服务器读取值
    /// </summary>
    private async Task<List<(string, DataValue, JToken)>> ReadJTokenValueAsync(NodeId[] nodeIds, CancellationToken cancellationToken = default)
    {
        if (m_session == null)
        {
            throw new ArgumentNullException(nameof(m_session));
        }
        ReadValueIdCollection nodesToRead = new();
        for (int i = 0; i < nodeIds.Length; i++)
        {
            nodesToRead.Add(new ReadValueId()
            {
                NodeId = nodeIds[i],
                AttributeId = Attributes.Value
            });
        }

        // 读取当前的值
        var result = await m_session.ReadAsync(
             null,
             0,
             TimestampsToReturn.Both,
             nodesToRead,
             cancellationToken).ConfigureAwait(false);
        var results = result.Results;
        var diagnosticInfos = result.DiagnosticInfos;
        ClientBase.ValidateResponse(results, nodesToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);
        List<(string, DataValue, JToken)> jTokens = new();
        for (int i = 0; i < results.Count; i++)
        {
            var variableNode = await ReadNodeAsync(nodeIds[i].ToString(), false, StatusCode.IsGood(results[i].StatusCode), cancellationToken).ConfigureAwait(false);
            var type = TypeInfo.GetBuiltInType(variableNode.DataType, m_session.SystemContext.TypeTable);
            var jToken = JsonUtils.Encode(m_session.MessageContext, type, results[i].Value);
            jTokens.Add((variableNode.NodeId.ToString(), results[i], jToken));
        }
        return jTokens.ToList();
    }

    /// <summary>
    /// 从服务器或缓存读取节点
    /// </summary>
    private VariableNode ReadNode(string nodeIdStr, bool isOnlyServer = true, bool cache = true)
    {
        if (!isOnlyServer)
        {
            if (_variableDicts.TryGetValue(nodeIdStr, out var value))
            {
                return value;
            }
        }
        NodeId nodeToRead = new(nodeIdStr);


        uint[] attributes = new uint[] { Attributes.NodeId, Attributes.DataType };

        // build list of values to read.
        ReadValueIdCollection itemsToRead = new ReadValueIdCollection();
        foreach (uint attributeId in attributes)
        {
            ReadValueId itemToRead = new ReadValueId
            {
                NodeId = nodeToRead,
                AttributeId = attributeId
            };
            itemsToRead.Add(itemToRead);
        }

        // read from server.
        DataValueCollection values = null;
        DiagnosticInfoCollection diagnosticInfos = null;

        ResponseHeader responseHeader = m_session.Read(
            null,
            0,
            TimestampsToReturn.Neither,
            itemsToRead,
            out values,
            out diagnosticInfos);

        VariableNode variableNode = GetVariableNodes(itemsToRead, values, diagnosticInfos, responseHeader).FirstOrDefault();

        if (cache)
            _variableDicts.AddOrUpdate(nodeIdStr, a => variableNode, (a, b) => variableNode);
        return variableNode;
    }

    /// <summary>
    /// 从服务器或缓存读取节点
    /// </summary>
    private async Task<VariableNode> ReadNodeAsync(string nodeIdStr, bool isOnlyServer = true, bool cache = true, CancellationToken cancellationToken = default)
    {
        if (!isOnlyServer)
        {
            if (_variableDicts.TryGetValue(nodeIdStr, out var value))
            {
                return value;
            }
        }

        NodeId nodeToRead = new(nodeIdStr);


        uint[] attributes = new uint[] { Attributes.NodeId, Attributes.DataType };

        // build list of values to read.
        ReadValueIdCollection itemsToRead = new ReadValueIdCollection();
        foreach (uint attributeId in attributes)
        {
            ReadValueId itemToRead = new ReadValueId
            {
                NodeId = nodeToRead,
                AttributeId = attributeId
            };
            itemsToRead.Add(itemToRead);
        }

        // read from server.
        ReadResponse readResponse = await m_session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Neither,
            itemsToRead, cancellationToken).ConfigureAwait(false);

        DataValueCollection values = readResponse.Results;
        DiagnosticInfoCollection diagnosticInfos = readResponse.DiagnosticInfos;
        var responseHeader = readResponse.ResponseHeader;
        VariableNode variableNode = GetVariableNodes(itemsToRead, values, diagnosticInfos, responseHeader).FirstOrDefault();

        if (OpcUaProperty.LoadType && variableNode.DataType != NodeId.Null && TypeInfo.GetBuiltInType(variableNode.DataType, m_session.SystemContext.TypeTable) == BuiltInType.ExtensionObject)
            await typeSystem.LoadType(variableNode.DataType, ct: cancellationToken).ConfigureAwait(false);

        if (cache)
            _variableDicts.AddOrUpdate(nodeIdStr, a => variableNode, (a, b) => variableNode);
        return variableNode;
    }


    private static List<VariableNode> GetVariableNodes(ReadValueIdCollection itemsToRead, DataValueCollection values, DiagnosticInfoCollection diagnosticInfos, ResponseHeader responseHeader, int count = 1)
    {
        ClientBase.ValidateResponse(values, itemsToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, itemsToRead);
        List<VariableNode> variableNodes = new();
        for (int i = 0; i < count; i++)
        {

            VariableNode variableNode = new VariableNode();
            DataValue value;

            if (!DataValue.IsGood(values[1 + 2 * i]))
            {
                //throw ServiceResultException.Create(values[1+2 * i].StatusCode, 1+2 * i, diagnosticInfos, responseHeader.StringTable);
            }
            // DataType Attribute
            value = values[1 + 2 * i];

            if (value == null)
            {
                //throw ServiceResultException.Create(StatusCodes.BadUnexpectedError, "Variable does not support the DataType attribute.");
                variableNode.DataType = NodeId.Null;
            }
            else
            {
                variableNode.DataType = (NodeId)value.GetValue(typeof(NodeId));
            }

            // NodeId Attribute
            if (!DataValue.IsGood(values[0 + 2 * i]))
            {
                throw ServiceResultException.Create(values[0 + 2 * i].StatusCode, 0 + 2 * i, diagnosticInfos, responseHeader.StringTable);
            }
            value = values[0 + 2 * i];
            if (value == null)
            {
                throw ServiceResultException.Create(StatusCodes.BadUnexpectedError, "Node does not support the NodeId attribute.");
            }

            variableNode.NodeId = (NodeId)value.GetValue(typeof(NodeId));
            variableNodes.Add(variableNode);
        }

        return variableNodes;
    }

    /// <summary>
    /// 从服务器读取节点
    /// </summary>
    private async Task<List<Node>> ReadNodesAsync(string[] nodeIdStrs, bool cache = false, CancellationToken cancellationToken = default)
    {
        List<Node> result = new(nodeIdStrs.Length);
        foreach (var items in nodeIdStrs.ChunkBetter(OpcUaProperty.GroupSize))
        {
            uint[] attributes = new uint[] { Attributes.NodeId, Attributes.DataType };

            // build list of values to read.
            ReadValueIdCollection itemsToRead = new ReadValueIdCollection();
            foreach (var item in items)
            {

                foreach (uint attributeId in attributes)
                {
                    ReadValueId itemToRead = new ReadValueId
                    {
                        NodeId = item,
                        AttributeId = attributeId
                    };
                    itemsToRead.Add(itemToRead);
                }
            }

            // read from server.
            ReadResponse readResponse = await m_session.ReadAsync(
                null,
                0,
                TimestampsToReturn.Neither,
                itemsToRead, cancellationToken).ConfigureAwait(false);

            DataValueCollection values = readResponse.Results;
            DiagnosticInfoCollection diagnosticInfos = readResponse.DiagnosticInfos;
            var responseHeader = readResponse.ResponseHeader;

            var variableNodes = GetVariableNodes(itemsToRead, values, diagnosticInfos, responseHeader, nodeIdStrs.Length);

            for (int i = 0; i < variableNodes.Count; i++)
            {
                var node = variableNodes[i];
                if (_variableDicts.TryGetValue(nodeIdStrs[i], out var value))
                {
                }
                else
                {
                    if (cache)
                        _variableDicts.AddOrUpdate(nodeIdStrs[i], a => node, (a, b) => node);
                    if (node.DataType != NodeId.Null && TypeInfo.GetBuiltInType(node.DataType, m_session.SystemContext.TypeTable) == BuiltInType.ExtensionObject)
                    {
                        await typeSystem.LoadType(node.DataType, ct: cancellationToken).ConfigureAwait(false);
                    }
                }
                result.Add(node);
            }
        }

        return result;
    }

    #endregion 读取/写入

    #region 私有方法

    private void CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs eventArgs)
    {
        if (ServiceResult.IsGood(eventArgs.Error))
            eventArgs.Accept = true;
        else if (OpcUaProperty.AutoAcceptUntrustedCertificates)
            eventArgs.Accept = true;
        else
            throw new Exception(string.Format("Verification certificate failed with error code: {0}: {1}", eventArgs.Error.Code, eventArgs.Error.AdditionalInfo));
    }

    /// <summary>
    /// Raises the connect complete event on the main GUI thread.
    /// </summary>
    private void DoConnectComplete(bool state)
    {
        m_ConnectComplete?.Invoke(this, state);
    }

    /// <summary>
    /// Report the client status
    /// </summary>
    /// <param name="logLevel">Whether the status represents an error. </param>
    /// <param name="exception">exception</param>
    /// <param name="status">The status message.</param>
    /// <param name="args">Arguments used to format the status message.</param>
    private void Log(byte logLevel, Exception exception, string status, params object[] args)
    {
        LogEvent?.Invoke(logLevel, this, string.Format(status, args), exception);
    }

    private async Task<Dictionary<string, List<OPCNodeAttribute>>> ReadNoteAttributeAsync(BrowseDescriptionCollection nodesToBrowse, ReadValueIdCollection nodesToRead, CancellationToken cancellationToken)
    {
        int startOfProperties = nodesToRead.Count;

        ReferenceDescriptionCollection references = await OpcUaUtils.BrowseAsync(m_session, nodesToBrowse, false, cancellationToken).ConfigureAwait(false);

        if (references == null)
        {
            throw new("浏览失败");
        }

        for (int ii = 0; ii < references.Count; ii++)
        {
            if (references[ii].NodeId.IsAbsolute)
            {
                continue;
            }

            ReadValueId nodeToRead = new()
            {
                NodeId = (NodeId)references[ii].NodeId,
                AttributeId = Attributes.Value
            };
            nodesToRead.Add(nodeToRead);
        }

        var result = await m_session.ReadAsync(
            null,
            0,
            TimestampsToReturn.Neither,
            nodesToRead, cancellationToken).ConfigureAwait(false);

        ClientBase.ValidateResponse(result.Results, nodesToRead);
        ClientBase.ValidateDiagnosticInfos(result.DiagnosticInfos, nodesToRead);

        Dictionary<string, List<OPCNodeAttribute>> nodeAttributes = new();
        for (int ii = 0; ii < result.Results.Count; ii++)
        {
            DataValue nodeValue = result.Results[ii];
            var nodeToRead = nodesToRead[ii];
            OPCNodeAttribute item = new();
            if (ii < startOfProperties)
            {
                if (nodeValue.StatusCode == StatusCodes.BadAttributeIdInvalid)
                {
                    continue;
                }

                item.Name = Attributes.GetBrowseName(nodesToRead[ii].AttributeId);
                if (StatusCode.IsBad(nodeValue.StatusCode))
                {
                    item.Type = Utils.Format("{0}", Attributes.GetDataTypeId(nodesToRead[ii].AttributeId));
                    item.Value = Utils.Format("{0}", nodeValue.StatusCode);
                }
                else
                {
                    TypeInfo typeInfo = TypeInfo.Construct(nodeValue.Value);
                    item.Type = typeInfo.BuiltInType.ToString();

                    if (typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions)
                    {
                        item.Type += "[]";
                    }
                    if (item.Name == nameof(Attributes.NodeClass))
                    {
                        item.Value = ((NodeClass)nodeValue.Value).ToString();
                    }
                    else if (item.Name == nameof(Attributes.EventNotifier))
                    {
                        item.Value = ((EventNotifierType)nodeValue.Value).ToString();
                    }
                    else
                        item.Value = nodeValue.Value;
                }
            }

            if (nodeAttributes.ContainsKey(nodeToRead.NodeId.ToString()))
            {
                nodeAttributes[nodeToRead.NodeId.ToString()].Add(item);
            }
            else
            {
                nodeAttributes.Add(nodeToRead.NodeId.ToString(), new() { item });
            }
        }
        return nodeAttributes;
    }

    /// <summary>
    /// 连接处理器连接事件处理完成。
    /// </summary>
    private void Server_ReconnectComplete(object? sender, EventArgs e)
    {
        try
        {
            if (!Object.ReferenceEquals(sender, m_reConnectHandler))
            {
                return;
            }
            // if session recovered, Session property is null
            if (m_reConnectHandler.Session != null)
            {
                // ensure only a new instance is disposed
                // after reactivate, the same session instance may be returned
                if (!Object.ReferenceEquals(m_session, m_reConnectHandler.Session))
                {
                    var session = m_session;
                    m_session = m_reConnectHandler.Session;
                    Utils.SilentDispose(session);
                }
            }

            m_reConnectHandler.Dispose();
            m_reConnectHandler = null;

            // raise any additional notifications.
            m_ReconnectComplete?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            Log(3, ex, $"{nameof(Server_ReconnectComplete)}");
        }
    }

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        lock (checkLock)
        {
            if (!Object.ReferenceEquals(session, m_session))
            {
                return;
            }

            if (ServiceResult.IsBad(e.Status))
            {
                if (m_reConnectHandler == null)
                {
                    Log(3, null, "Reconnecting in {0}s", 1);
                    m_ReconnectStarting?.Invoke(this, e);

                    m_reConnectHandler = new SessionReconnectHandler(true, 10000);
                    m_reConnectHandler.BeginReconnect(m_session, 1000, Server_ReconnectComplete);

                    e.CancelKeepAlive = true;
                }
            }


            // raise any additional notifications.
            m_KeepAliveComplete?.Invoke(this, e);
        }
    }

    #endregion 私有方法
}
