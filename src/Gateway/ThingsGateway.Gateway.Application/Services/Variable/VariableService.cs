﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using Mapster;

using Microsoft.AspNetCore.Components.Forms;

using MiniExcelLibs;

using SqlSugar;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Reflection;
using System.Text;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Foundation.Extension.Dynamic;
using ThingsGateway.NewLife;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

internal sealed class VariableService : BaseService<Variable>, IVariableService
{
    private readonly IChannelService _channelService;
    private readonly IDeviceService _deviceService;
    private readonly IPluginService _pluginService;
    private readonly IDispatchService<bool> _allDispatchService;
    private readonly IDispatchService<Variable> _dispatchService;

    /// <inheritdoc cref="IVariableService"/>
    public VariableService(
   IDispatchService<Variable> dispatchService,
   IDispatchService<bool> allDispatchService
        )
    {
        _channelService = App.RootServices.GetRequiredService<IChannelService>();
        _pluginService = App.RootServices.GetRequiredService<IPluginService>();
        _deviceService = App.RootServices.GetRequiredService<IDeviceService>();
        _dispatchService = dispatchService;
        _allDispatchService = allDispatchService;
    }

    #region 测试

    public async Task<(List<Channel>, List<Device>, List<Variable>)> InsertTestDataAsync(int variableCount, int deviceCount, string slaveUrl = "127.0.0.1:502")
    {
        if (slaveUrl.IsNullOrWhiteSpace()) slaveUrl = "127.0.0.1:502";
        if (deviceCount > variableCount) variableCount = deviceCount;
        List<Channel> newChannels = new();
        List<Device> newDevices = new();
        List<Variable> newVariables = new();
        var addressNum = 1;
        // 计算每个设备分配的默认变量数
        var groupVariableCount = (int)Math.Ceiling((decimal)variableCount / deviceCount);

        for (int i = 0; i < deviceCount; i++)
        {
            Channel channel = new Channel();
            Device device = new Device();
            {
                var id = CommonUtils.GetSingleId();
                var name = $"modbusChannel{id}";
                channel.ChannelType = ChannelTypeEnum.TcpClient;
                channel.Name = name;
                channel.Id = id;
                channel.CreateUserId = UserManager.UserId;
                channel.CreateOrgId = UserManager.OrgId;
                channel.RemoteUrl = slaveUrl;
                channel.PluginName = "ThingsGateway.Plugin.Modbus.ModbusMaster";
                //动态插件属性默认
                newChannels.Add(channel);
            }
            {
                var id = CommonUtils.GetSingleId();
                var name = $"modbusDevice{id}";
                device.Name = name;
                device.Id = id;
                device.ChannelId = channel.Id;
                device.CreateUserId = UserManager.UserId;
                device.CreateOrgId = UserManager.OrgId;
                device.IntervalTime = "1000";
                //动态插件属性默认
                newDevices.Add(device);
            }

            // 计算当前设备应该分配的变量数量
            int currentGroupVariableCount = (i == deviceCount - 1)
                ? variableCount - (deviceCount - 1) * groupVariableCount // 最后一个设备分配剩余的变量
                : groupVariableCount;

            for (int i1 = 0; i1 < currentGroupVariableCount; i1++)
            {
                if (addressNum > 65535) addressNum = 1;
                var address = $"4{addressNum}";
                addressNum++;
                var id = CommonUtils.GetSingleId();
                var name = $"modbusVariable{address}_{id}";
                Variable variable = new Variable();
                variable.DataType = DataTypeEnum.Int16;
                variable.Name = name;
                variable.Id = id;
                variable.CreateOrgId = UserManager.OrgId;
                variable.CreateUserId = UserManager.UserId;
                variable.DeviceId = device.Id;
                variable.RegisterAddress = address;
                newVariables.Add(variable);
            }
        }

        Channel serviceChannel = new Channel();
        Device serviceDevice = new Device();

        {
            var id = CommonUtils.GetSingleId();
            var name = $"modbusSlaveChannel{id}";
            serviceChannel.ChannelType = ChannelTypeEnum.TcpService;
            serviceChannel.Name = name;
            serviceChannel.Enable = true;
            serviceChannel.Id = id;
            serviceChannel.CreateUserId = UserManager.UserId;
            serviceChannel.CreateOrgId = UserManager.OrgId;
            serviceChannel.BindUrl = "127.0.0.1:502";
            serviceChannel.PluginName = "ThingsGateway.Plugin.Modbus.ModbusSlave";
            newChannels.Add(serviceChannel);
        }
        {
            var id = CommonUtils.GetSingleId();
            var name = $"modbusSlaveDevice{id}";
            serviceDevice.Name = name;
            serviceDevice.Id = id;
            serviceDevice.CreateUserId = UserManager.UserId;
            serviceDevice.CreateOrgId = UserManager.OrgId;
            serviceDevice.ChannelId = serviceChannel.Id;
            serviceDevice.IntervalTime = "1000";
            newDevices.Add(serviceDevice);
        }

        Channel mqttChannel = new Channel();
        Device mqttDevice = new Device();

        {
            var id = CommonUtils.GetSingleId();
            var name = $"mqttChannel{id}";
            mqttChannel.ChannelType = ChannelTypeEnum.Other;
            mqttChannel.Name = name;
            mqttChannel.Id = id;
            mqttChannel.CreateUserId = UserManager.UserId;
            mqttChannel.CreateOrgId = UserManager.OrgId;
            mqttChannel.PluginName = "ThingsGateway.Plugin.Mqtt.MqttServer";
            newChannels.Add(mqttChannel);
        }
        {
            var id = CommonUtils.GetSingleId();
            var name = $"mqttDevice{id}";
            mqttDevice.Name = name;
            mqttDevice.Id = id;
            mqttDevice.CreateUserId = UserManager.UserId;
            mqttDevice.CreateOrgId = UserManager.OrgId;
            mqttDevice.ChannelId = mqttChannel.Id;
            mqttDevice.IntervalTime = "1000";
            mqttDevice.DevicePropertys = new Dictionary<string, string>
            {
              {"IsAllVariable", "true"}
            };
            newDevices.Add(mqttDevice);
        }
        using var db = GetDB();

        var result = await db.UseTranAsync(async () =>
        {
            await db.Fastest<Channel>().PageSize(100000).BulkCopyAsync(newChannels).ConfigureAwait(false);
            await db.Fastest<Device>().PageSize(100000).BulkCopyAsync(newDevices).ConfigureAwait(false);
            await db.Fastest<Variable>().PageSize(100000).BulkCopyAsync(newVariables).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            _channelService.DeleteChannelFromCache();//刷新缓存
            _deviceService.DeleteDeviceFromCache();
            _allDispatchService.Dispatch(new());
            DeleteCache();
        }
        else
        {
            throw new(result.ErrorMessage, result.ErrorException);
        }
        return (newChannels, newDevices, newVariables);
    }

    #endregion 测试

    /// <summary>
    /// 保存初始值
    /// </summary>
    public async Task UpdateInitValueAsync(List<Variable> variables)
    {
        if (variables.Count > 0)
        {
            using var db = GetDB();
            var result = await db.Updateable<Variable>(variables).UpdateColumns(a => a.Value).ExecuteCommandAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    [OperDesc("SaveVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task AddBatchAsync(List<Variable> input)
    {
        using var db = GetDB();
        var result = await db.Insertable(input).ExecuteCommandAsync().ConfigureAwait(false);

        if (result > 0)
            DeleteCache();
        _dispatchService.Dispatch(new());
    }

    /// <inheritdoc/>
    [OperDesc("SaveVariable", localizerType: typeof(Variable), isRecordPar: false)]
    public async Task<bool> BatchEditAsync(IEnumerable<Variable> models, Variable oldModel, Variable model)
    {
        var differences = models.GetDiffProperty(oldModel, model);
        differences.Remove(nameof(Variable.VariablePropertys));
        if (differences?.Count > 0)
        {
            using var db = GetDB();
            var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
            var data = models
                            .WhereIf(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIf(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
             .ToList();

            var result = (await db.Updateable(data).UpdateColumns(differences.Select(a => a.Key).ToArray()).ExecuteCommandAsync().ConfigureAwait(false)) > 0;
            _dispatchService.Dispatch(new());
            if (result)
                DeleteCache();
            return result;
        }
        else
        {
            return true;
        }
    }

    [OperDesc("DeleteVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task DeleteByDeviceIdAsync(IEnumerable<long> input, SqlSugarClient db)
    {
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        var ids = input.ToList();
        var result = await db.Deleteable<Variable>().Where(a => ids.Contains(a.DeviceId))
                          .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
            .ExecuteCommandAsync().ConfigureAwait(false);

        if (result > 0)
            DeleteCache();
        _dispatchService.Dispatch(new());
    }

    [OperDesc("DeleteVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task<bool> DeleteVariableAsync(IEnumerable<long> input)
    {
        using var db = GetDB();
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        var ids = input.ToList();
        var result = (await db.Deleteable<Variable>().Where(a => ids.Contains(a.Id))
                          .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
             .ExecuteCommandAsync().ConfigureAwait(false)) > 0;
        _dispatchService.Dispatch(new());

        if (result)
            DeleteCache();
        return result;
    }

    public async Task<List<Variable>> GetAllAsync(long? devId = null)
    {
        using var db = GetDB();
        if (devId == null)
        {
            var deviceVariables = await db.Queryable<Variable>().Where(a => a.DeviceId > 0).ToListAsync().ConfigureAwait(false);
            return deviceVariables;
        }
        else
        {
            var deviceVariables = await db.Queryable<Variable>().Where(a => a.DeviceId == devId).ToListAsync().ConfigureAwait(false);
            return deviceVariables;
        }
    }

    /// <summary>
    /// 报表查询
    /// </summary>
    /// <param name="exportFilter">查询条件</param>
    public async Task<QueryData<Variable>> PageAsync(ExportFilter exportFilter)
    {
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        HashSet<long>? deviceId = null;
        if (!exportFilter.PluginName.IsNullOrWhiteSpace())
        {
            var channel = (await _channelService.GetAllAsync().ConfigureAwait(false)).Where(a => a.PluginName == exportFilter.PluginName).Select(a => a.Id).ToHashSet();
            deviceId = (await _deviceService.GetAllAsync().ConfigureAwait(false)).Where(a => channel.Contains(a.ChannelId)).Select(a => a.Id).ToHashSet();
        }
        else if (exportFilter.ChannelId != null)
        {
            deviceId = (await _deviceService.GetAllAsync().ConfigureAwait(false)).Where(a => a.ChannelId == exportFilter.ChannelId).Select(a => a.Id).ToHashSet();
        }
        return await QueryAsync(exportFilter.QueryPageOptions, a => a
        .WhereIF(!exportFilter.QueryPageOptions.SearchText.IsNullOrWhiteSpace(), a => a.Name.Contains(exportFilter.QueryPageOptions.SearchText!))
        .WhereIF(exportFilter.PluginType == PluginTypeEnum.Collect, a => a.DeviceId == exportFilter.DeviceId)
        .WhereIF(deviceId != null, a => deviceId.Contains(a.DeviceId))

                .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
        .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)


        .WhereIF(exportFilter.PluginType == PluginTypeEnum.Business, u => SqlFunc.JsonLike(u.VariablePropertys, exportFilter.DeviceId.ToString()))

        ).ConfigureAwait(false);
    }

    /// <summary>
    /// 保存变量
    /// </summary>
    /// <param name="input">变量</param>
    /// <param name="type">保存类型</param>
    [OperDesc("SaveVariable", localizerType: typeof(Variable))]
    public async Task<bool> SaveVariableAsync(Variable input, ItemChangedType type)
    {
        if (type == ItemChangedType.Update)
            await GlobalData.SysUserService.CheckApiDataScopeAsync(input.CreateOrgId, input.CreateUserId).ConfigureAwait(false);

        if (await base.SaveAsync(input, type).ConfigureAwait(false))
        {
            _dispatchService.Dispatch(new());
            DeleteCache();
            return true;
        }
        return false;
    }

    private static void DeleteCache()
    {
        App.CacheService.Remove(ThingsGatewayCacheConst.Cache_Variable);
    }

    #region 导出

    /// <summary>
    /// 导出文件
    /// </summary>
    [OperDesc("ExportVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task<MemoryStream> ExportMemoryStream(IEnumerable<Variable> data, string deviceName = null)
    {
        Dictionary<string, object> sheets = await ExportCoreAsync(data, deviceName).ConfigureAwait(false);

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(sheets).ConfigureAwait(false);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    /// <summary>
    /// 导出文件
    /// </summary>
    [OperDesc("ExportVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task<Dictionary<string, object>> ExportVariableAsync(ExportFilter exportFilter)
    {
        var data = (await PageAsync(exportFilter).ConfigureAwait(false));
        var sheets = await ExportCoreAsync(data.Items).ConfigureAwait(false);
        return sheets;
    }

    private async Task<Dictionary<string, object>> ExportCoreAsync(IEnumerable<Variable> data, string deviceName = null)
    {
        if (data == null || !data.Any())
        {
            data = new List<Variable>();
        }
        var deviceDicts = (await _deviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        var channelDicts = (await _channelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        var driverPluginDicts = _pluginService.GetList(PluginTypeEnum.Business).ToDictionary(a => a.FullName);
        //总数据
        Dictionary<string, object> sheets = new();
        //变量页
        ConcurrentList<Dictionary<string, object>> variableExports = new();
        //变量附加属性，转成Dict<表名,List<Dict<列名，列数据>>>的形式
        ConcurrentDictionary<string, ConcurrentList<Dictionary<string, object>>> devicePropertys = new();
        ConcurrentDictionary<string, (VariablePropertyBase, Dictionary<string, PropertyInfo>)> propertysDict = new();

        #region 列名称

        var type = typeof(Variable);
        var propertyInfos = type.GetRuntimeProperties().Where(a => a.GetCustomAttribute<IgnoreExcelAttribute>() == null)
             .OrderBy(
            a =>
            {
                var order = a.GetCustomAttribute<AutoGenerateColumnAttribute>()?.Order ?? int.MaxValue; ;
                if (order < 0)
                {
                    order = order + 10000000;
                }
                else if (order == 0)
                {
                    order = 10000000;
                }
                return order;
            }
            )
            ;

        #endregion 列名称
        var varName = nameof(Variable.Name);
        data.ParallelForEach((variable, state, index) =>
        {
            Dictionary<string, object> varExport = new();
            deviceDicts.TryGetValue(variable.DeviceId, out var device);
            //设备实体没有包含设备名称，手动插入
            varExport.TryAdd(ExportString.DeviceName, device?.Name ?? deviceName);
            foreach (var item in propertyInfos)
            {
                //描述
                var desc = type.GetPropertyDisplayName(item.Name);
                if (item.Name == varName)
                {
                    varName = desc;
                }
                //数据源增加
                varExport.TryAdd(desc ?? item.Name, item.GetValue(variable)?.ToString());
            }

            //添加完整设备信息
            variableExports.Add(varExport);

            #region 插件sheet
            if (variable.VariablePropertys != null)
            {

                foreach (var item in variable.VariablePropertys)
                {
                    //插件属性
                    //单个设备的行数据
                    Dictionary<string, object> driverInfo = new();
                    var has = deviceDicts.TryGetValue(item.Key, out var businessDevice);
                    if (!has)
                        continue;

                    channelDicts.TryGetValue(businessDevice.ChannelId, out var channel);

                    //没有包含设备名称，手动插入
                    driverInfo.TryAdd(ExportString.DeviceName, businessDevice.Name);
                    driverInfo.TryAdd(ExportString.VariableName, variable.Name);

                    var propDict = item.Value;

                    if (propertysDict.TryGetValue(channel.PluginName, out var propertys))
                    {
                    }
                    else
                    {
                        try
                        {

                            var variableProperty = ((BusinessBase)_pluginService.GetDriver(channel.PluginName))?.VariablePropertys;
                            propertys.Item1 = variableProperty;
                            var variablePropertyType = variableProperty.GetType();
                            propertys.Item2 = variablePropertyType.GetRuntimeProperties()
               .Where(a => a.GetCustomAttribute<DynamicPropertyAttribute>() != null)
               .ToDictionary(a => variablePropertyType.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description));
                            propertysDict.TryAdd(channel.PluginName, propertys);

                        }
                        catch
                        {

                        }
                    }
                    if (propertys.Item2?.Count == null)
                    {
                        continue;
                    }
                    //根据插件的配置属性项生成列，从数据库中获取值或者获取属性默认值
                    foreach (var item1 in propertys.Item2)
                    {
                        if (propDict.TryGetValue(item1.Value.Name, out var dependencyProperty))
                        {
                            driverInfo.TryAdd(item1.Key, dependencyProperty);
                        }
                        else
                        {
                            //添加对应属性数据
                            driverInfo.TryAdd(item1.Key, ThingsGatewayStringConverter.Default.Serialize(null, item1.Value.GetValue(propertys.Item1)));
                        }
                    }

                    if (!driverPluginDicts.ContainsKey(channel.PluginName))
                        continue;

                    var pluginName = PluginServiceUtil.GetFileNameAndTypeName(channel.PluginName);
                    //lock (devicePropertys)
                    {
                        if (devicePropertys.ContainsKey(pluginName.Item2))
                        {
                            if (driverInfo.Count > 0)
                                devicePropertys[pluginName.Item2].Add(driverInfo);
                        }
                        else
                        {
                            lock (devicePropertys)
                            {
                                if (devicePropertys.ContainsKey(pluginName.Item2))
                                {
                                    if (driverInfo.Count > 0)
                                        devicePropertys[pluginName.Item2].Add(driverInfo);
                                }
                                else
                                {
                                    if (driverInfo.Count > 0)
                                        devicePropertys.TryAdd(pluginName.Item2, new() { driverInfo });
                                }

                            }
                        }
                    }
                }
            }

            #endregion 插件sheet
        });



        variableExports = new(variableExports.OrderBy(a => a[ExportString.DeviceName]).ThenBy(a => a[varName]));

        //添加设备页
        sheets.Add(ExportString.VariableName, variableExports);

        //HASH
        foreach (var item in devicePropertys.Keys)
        {
            devicePropertys[item] = new(devicePropertys[item].OrderBy(a => a[ExportString.DeviceName]).ThenBy(a => a[ExportString.VariableName]));
            //HashSet<string> allKeys = item.Value.SelectMany(a => a.Keys).ToHashSet();

            //foreach (var dict in item.Value)
            //{
            //    foreach (var key in allKeys)
            //    {
            //        if (!dict.ContainsKey(key))
            //        {
            //            // 添加缺失的键，并设置默认值
            //            dict.TryAdd(key, null);
            //        }
            //    }
            //}
            sheets.Add(item, devicePropertys[item]);
        }

        return sheets;
    }

    #endregion 导出

    #region 导入

    /// <inheritdoc/>
    [OperDesc("ImportVariable", isRecordPar: false, localizerType: typeof(Variable))]
    public async Task<HashSet<long>> ImportVariableAsync(Dictionary<string, ImportPreviewOutputBase> input)
    {
        var variables = new List<Variable>();
        foreach (var item in input)
        {
            if (item.Key == ExportString.VariableName)
            {
                var variableImports = ((ImportPreviewOutput<Variable>)item.Value).Data;
                variables = new List<Variable>(variableImports.Values);
                break;
            }
        }
        var upData = variables.Where(a => a.IsUp).ToList();
        var insertData = variables.Where(a => !a.IsUp).ToList();
        using var db = GetDB();
        await db.Fastest<Variable>().PageSize(100000).BulkCopyAsync(insertData).ConfigureAwait(false);
        await db.Fastest<Variable>().PageSize(100000).BulkUpdateAsync(upData).ConfigureAwait(false);
        _dispatchService.Dispatch(new());
        DeleteCache();
        return variables.Select(a => a.Id).ToHashSet();
    }

    private static readonly WaitLock _cacheLock = new();

    private async Task<Dictionary<string, VariableImportData>> GetVariableImportData()
    {
        var key = ThingsGatewayCacheConst.Cache_Variable;
        var datas = App.CacheService.Get<Dictionary<string, VariableImportData>>(key);

        if (datas == null)
        {
            try
            {
                await _cacheLock.WaitAsync().ConfigureAwait(false);
                datas = App.CacheService.Get<Dictionary<string, VariableImportData>>(key);
                if (datas == null)
                {
                    using var db = GetDB();
                    datas = (await db.Queryable<Variable>().Select(it => new VariableImportData()
                    {
                        Id = it.Id,
                        Name = it.Name,
                        CreateOrgId = it.CreateOrgId,
                        CreateUserId = it.CreateUserId
                    }).ToListAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

                    App.CacheService.Set(key, datas);
                }
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        return datas;
    }

    public Task PreheatCache()
    {
        return GetVariableImportData();
    }

    private sealed class VariableImportData
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long CreateOrgId { get; set; }
        public long CreateUserId { get; set; }
    }
    public async Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile)
    {
        // 上传文件并获取文件路径
        var path = await browserFile.StorageLocal().ConfigureAwait(false);
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

        try
        {
            // 获取Excel文件中所有工作表的名称
            var sheetNames = MiniExcel.GetSheetNames(path);

            // 获取所有设备的字典，以设备名称作为键
            var deviceDicts = (await _deviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 存储导入检验结果的字典
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();

            // 设备页导入预览输出
            ImportPreviewOutput<Variable> deviceImportPreview = new();

            // 获取驱动插件的全名和名称的字典
            var driverPluginFullNameDict = _pluginService.GetList().ToDictionary(a => a.FullName);
            var driverPluginNameDict = _pluginService.GetList().ToDictionary(a => a.Name);
            ConcurrentDictionary<string, (Type, Dictionary<string, PropertyInfo>, Dictionary<string, PropertyInfo>)> propertysDict = new();

            // 遍历每个工作表
            foreach (var sheetName in sheetNames)
            {
                // 获取当前工作表的所有行数据
                var rows = MiniExcel.Query(path, useHeaderRow: true, sheetName: sheetName).Cast<IDictionary<string, object>>();

                // 变量页处理
                if (sheetName == ExportString.VariableName)
                {
                    int row = 0;
                    ImportPreviewOutput<Variable> importPreviewOutput = new();
                    ImportPreviews.Add(sheetName, importPreviewOutput);
                    deviceImportPreview = importPreviewOutput;

                    // 线程安全的变量列表
                    var variables = new ConcurrentList<Variable>();
                    var type = typeof(Variable);
                    // 获取目标类型的所有属性，并根据是否需要过滤 IgnoreExcelAttribute 进行筛选
                    var variableProperties = type.GetRuntimeProperties().Where(a => (a.GetCustomAttribute<IgnoreExcelAttribute>() == null) && a.CanWrite)
                                                .ToDictionary(a => type.GetPropertyDisplayName(a.Name));

                    var dbVariableDicts = await GetVariableImportData().ConfigureAwait(false);

                    // 并行处理每一行数据
                    rows.ParallelForEach((item, state, index) =>
                    {
                        try
                        {
                            // 尝试将行数据转换为 Variable 对象
                            var variable = ((ExpandoObject)item!).ConvertToEntity<Variable>(variableProperties);
                            variable.Row = index;

                            // 获取设备名称并查找对应的设备
                            item.TryGetValue(ExportString.DeviceName, out var value);
                            var deviceName = value?.ToString();
                            deviceDicts.TryGetValue(deviceName, out var device);
                            var deviceId = device?.Id;

                            // 如果找不到对应的设备，则添加错误信息到导入预览结果并返回
                            if (deviceId == null)
                            {
                                importPreviewOutput.HasError = true;
                                importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["NotNull", deviceName]));
                                return;
                            }
                            // 手动补录变量ID和设备ID
                            variable.DeviceId = deviceId.Value;

                            // 对 Variable 对象进行验证
                            var validationContext = new ValidationContext(variable);
                            var validationResults = new List<ValidationResult>();
                            validationContext.ValidateProperty(validationResults);

                            // 构建验证结果的错误信息
                            StringBuilder stringBuilder = new();
                            foreach (var validationResult in validationResults.Where(v => !string.IsNullOrEmpty(v.ErrorMessage)))
                            {
                                foreach (var memberName in validationResult.MemberNames)
                                {
                                    stringBuilder.Append(validationResult.ErrorMessage!);
                                }
                            }

                            // 如果有验证错误，则添加错误信息到导入预览结果并返回
                            if (stringBuilder.Length > 0)
                            {
                                importPreviewOutput.HasError = true;
                                importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, stringBuilder.ToString()));
                                return;
                            }

                            if (dbVariableDicts.TryGetValue(variable.Name, out var dbvar1))
                            {
                                variable.Id = dbvar1.Id;
                                variable.CreateOrgId = dbvar1.CreateOrgId;
                                variable.CreateUserId = dbvar1.CreateUserId;
                                variable.IsUp = true;
                            }
                            else
                            {
                                variable.IsUp = false;
                                variable.CreateOrgId = UserManager.OrgId;
                                variable.CreateUserId = UserManager.UserId;
                            }

                            if (device.IsUp && ((dataScope != null && dataScope?.Count > 0 && !dataScope.Contains(variable.CreateOrgId)) || dataScope?.Count == 0 && variable.CreateUserId != UserManager.UserId))
                            {
                                importPreviewOutput.Results.Add((row++, false, "Operation not permitted"));
                            }
                            else
                            {
                                variables.Add(variable);
                                importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), true, null));
                            }

                        }
                        catch (Exception ex)
                        {
                            // 捕获异常并添加错误信息到导入预览结果
                            importPreviewOutput.HasError = true;
                            importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, ex.Message));
                        }
                    });

                    // 为未成功上传的变量生成新的ID
                    foreach (var item in variables.OrderBy(a => a.Row))
                    {
                        if (!item.IsUp)
                            item.Id = CommonUtils.GetSingleId();
                    }

                    // 将变量列表转换为字典，并赋值给导入预览输出对象的 Data 属性
                    importPreviewOutput.Data = variables.OrderBy(a => a.Row).ToDictionary(a => a.Name);
                }

                // 其他工作表处理
                else
                {
                    int row = 1;
                    ImportPreviewOutput<string> importPreviewOutput = new();
                    ImportPreviews.Add(sheetName, importPreviewOutput);

                    _ = driverPluginNameDict.TryGetValue(sheetName, out var driverPluginType);

                    try
                    {
                        if (driverPluginType == null)
                        {
                            importPreviewOutput.HasError = true;
                            importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["NotNull", sheetName]));
                            continue;
                        }

                        if (propertysDict.TryGetValue(driverPluginType.FullName, out var propertys))
                        {
                        }
                        else
                        {
                            try
                            {


                                var variableProperty = ((BusinessBase)_pluginService.GetDriver(driverPluginType.FullName)).VariablePropertys;
                                var variablePropertyType = variableProperty.GetType();
                                propertys.Item1 = variablePropertyType;
                                propertys.Item2 = variablePropertyType.GetRuntimeProperties()
                                    .Where(a => a.GetCustomAttribute<DynamicPropertyAttribute>() != null)
                                    .ToDictionary(a => variablePropertyType.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description));

                                // 获取目标类型的所有属性，并根据是否需要过滤 IgnoreExcelAttribute 进行筛选
                                var properties = propertys.Item1.GetRuntimeProperties().Where(a => (a.GetCustomAttribute<IgnoreExcelAttribute>() == null) && a.CanWrite)
                                                .ToDictionary(a => propertys.Item1.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description));

                                propertys.Item3 = properties;
                                propertysDict.TryAdd(driverPluginType.FullName, propertys);
                            }
                            catch (Exception)
                            {

                            }
                        }

                        rows.ParallelForEach(item =>
                        {
                            try
                            {
                                if (propertys.Item3?.Count == null || propertys.Item1 == null)
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["ImportNullError"]));
                                    return;
                                }

                                // 尝试将导入的项转换为对象
                                var pluginProp = (item as ExpandoObject)?.ConvertToEntity(propertys.Item1, propertys.Item3);

                                // 如果转换失败，则添加错误信息到导入预览结果并返回
                                if (pluginProp == null)
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["ImportNullError"]));
                                    return;
                                }

                                // 转化插件名称和变量名称
                                item.TryGetValue(ExportString.VariableName, out var variableNameObj);
                                item.TryGetValue(ExportString.DeviceName, out var businessDevName);
                                deviceDicts.TryGetValue(businessDevName?.ToString(), out var businessDevice);

                                // 如果设备名称或变量名称为空，或者找不到对应的设备，则添加错误信息到导入预览结果并返回
                                if (businessDevName == null || businessDevice == null)
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["DeviceNotNull"]));
                                    return;
                                }
                                if (variableNameObj == null)
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["VariableNotNull"]));
                                    return;
                                }

                                // 对对象进行验证
                                var validationContext = new ValidationContext(pluginProp);
                                var validationResults = new List<ValidationResult>();
                                validationContext.ValidateProperty(validationResults);

                                // 构建验证结果的错误信息
                                StringBuilder stringBuilder = new();
                                foreach (var validationResult in validationResults.Where(v => !string.IsNullOrEmpty(v.ErrorMessage)))
                                {
                                    foreach (var memberName in validationResult.MemberNames)
                                    {
                                        stringBuilder.Append(validationResult.ErrorMessage!);
                                    }
                                }

                                // 如果有验证错误，则添加错误信息到导入预览结果并返回
                                if (stringBuilder.Length > 0)
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, stringBuilder.ToString()));
                                    return;
                                }

                                // 创建依赖属性字典
                                Dictionary<string, string> dependencyProperties = new();
                                foreach (var keyValuePair in item)
                                {
                                    if (propertys.Item2.TryGetValue(keyValuePair.Key, out var propertyInfo))
                                    {
                                        dependencyProperties.Add(propertyInfo.Name, keyValuePair.Value?.ToString());
                                    }
                                }

                                // 获取变量名称并检查是否存在于设备导入预览数据中
                                var variableName = variableNameObj?.ToString();
                                var has = deviceImportPreview.Data.TryGetValue(variableName, out var deviceVariable);

                                // 如果存在，则更新变量属性字典，并添加成功信息到导入预览结果；否则，添加错误信息到导入预览结果并返回
                                if (has)
                                {
                                    deviceVariable.VariablePropertys ??= new();
                                    deviceVariable.VariablePropertys?.AddOrUpdate(businessDevice.Id, dependencyProperties);
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), true, null));
                                }
                                else
                                {
                                    importPreviewOutput.HasError = true;
                                    importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, Localizer["VariableNotNull"]));
                                    return;
                                }
                            }
                            catch (Exception ex)
                            {
                                // 捕获异常并添加错误信息到导入预览结果
                                importPreviewOutput.HasError = true;
                                importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, ex.Message));
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        // 捕获异常并添加错误信息到导入预览结果
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Add(ref row, 1), false, ex.Message));
                    }
                }
            }

            return ImportPreviews;
        }
        finally
        {
            // 最终清理：删除临时上传的文件
            FileUtility.Delete(path);
        }
    }

    #endregion 导入
}
