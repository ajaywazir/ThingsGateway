//------------------------------------------------------------------------------
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
using System.Reflection;
using System.Text;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Foundation.Extension.Dynamic;
using ThingsGateway.FriendlyException;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

internal sealed class DeviceService : BaseService<Device>, IDeviceService
{
    private readonly IChannelService _channelService;
    private readonly IPluginService _pluginService;

    public DeviceService()
    {
        _channelService = App.RootServices.GetRequiredService<IChannelService>();
        _pluginService = App.RootServices.GetRequiredService<IPluginService>();
    }


    /// <inheritdoc/>
    [OperDesc("CopyDevice", localizerType: typeof(Device), isRecordPar: false)]
    public async Task<bool> CopyAsync(Dictionary<Device, List<Variable>> devices)
    {
        using var db = GetDB();

        //事务
        var result = await db.UseTranAsync(async () =>
        {


            var device = devices.Keys.ToList();
            ManageHelper.CheckDeviceCount(device.Count);

            await db.Insertable(device).ExecuteCommandAsync().ConfigureAwait(false);

            var variable = devices.SelectMany(a => a.Value).ToList();
            ManageHelper.CheckVariableCount(variable.Count);

            await db.Insertable(variable).ExecuteCommandAsync().ConfigureAwait(false);


        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteDeviceFromCache();
            App.GetService<IVariableService>().DeleteVariableCache();
            return true;
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }

    }


    public async Task UpdateLogAsync(long channelId, LogLevel logLevel)
    {
        using var db = GetDB();

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            //更新数据库

            await db.Updateable<Device>().SetColumns(it => new Device() { LogLevel = logLevel }).Where(a => a.Id == channelId).ExecuteCommandAsync().ConfigureAwait(false);

        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteDeviceFromCache();
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <inheritdoc/>
    [OperDesc("SaveDevice", localizerType: typeof(Device), isRecordPar: false)]
    public async Task<bool> BatchEditAsync(IEnumerable<Device> models, Device oldModel, Device model)
    {
        var differences = models.GetDiffProperty(oldModel, model);
        differences.Remove(nameof(Device.DevicePropertys));

        if (differences?.Count > 0)
        {
            using var db = GetDB();
            var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
            var data = models
                            .WhereIf(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIf(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
             .ToList();
            var result = (await db.Updateable(data).UpdateColumns(differences.Select(a => a.Key).ToArray()).ExecuteCommandAsync().ConfigureAwait(false)) > 0;
            if (result)
                DeleteDeviceFromCache();
            return result;
        }
        else
        {
            return true;
        }
    }

    [OperDesc("DeleteDevice", isRecordPar: false, localizerType: typeof(Device))]
    public async Task DeleteByChannelIdAsync(IEnumerable<long> ids, SqlSugarClient db)
    {
        var IdhashSet = ids.ToHashSet();
        var variableService = App.RootServices.GetRequiredService<IVariableService>();
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        //事务
        var result = await db.UseTranAsync(async () =>
        {
            var data = (await GetAllAsync(db).ConfigureAwait(false))
                          .WhereIf(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIf(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
             .Where(a => IdhashSet.Contains(a.ChannelId))
            .Select(a => a.Id).ToList();
            await db.Deleteable<Device>(data).ExecuteCommandAsync().ConfigureAwait(false);
            await variableService.DeleteByDeviceIdAsync(data, db).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteDeviceFromCache();
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    [OperDesc("DeleteDevice", isRecordPar: false, localizerType: typeof(Device))]
    public async Task<bool> DeleteDeviceAsync(IEnumerable<long> ids)
    {
        var IdhashSet = ids.ToHashSet();
        var variableService = App.RootServices.GetRequiredService<IVariableService>();
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

        using var db = GetDB();
        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await db.Deleteable<Device>().Where(a => IdhashSet.Contains(a.Id))
                          .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
             .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
            .ExecuteCommandAsync().ConfigureAwait(false);
            await variableService.DeleteByDeviceIdAsync(ids, db).ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            DeleteDeviceFromCache();
            return true;
        }
        else
        {
            //写日志
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <inheritdoc />
    public void DeleteDeviceFromCache()
    {
        App.CacheService.Remove(ThingsGatewayCacheConst.Cache_Device);//删除设备缓存
    }

    /// <summary>
    /// 从缓存/数据库获取全部信息
    /// </summary>
    /// <returns>列表</returns>
    public async Task<List<Device>> GetAllAsync(SqlSugarClient db = null)
    {
        var key = ThingsGatewayCacheConst.Cache_Device;
        var devices = App.CacheService.Get<List<Device>>(key);
        if (devices == null)
        {
            db ??= GetDB();
            devices = await db.Queryable<Device>().OrderBy(a => a.Id).ToListAsync().ConfigureAwait(false);
            App.CacheService.Set(key, devices);
        }
        return devices;
    }

    public async Task<Device?> GetDeviceByIdAsync(long id)
    {
        var data = await GetAllAsync().ConfigureAwait(false);
        return data?.FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    /// 报表查询
    /// </summary>
    /// <param name="exportFilter">查询条件</param>
    public async Task<QueryData<Device>> PageAsync(ExportFilter exportFilter)
    {
        HashSet<long>? channel = null;
        if (!exportFilter.PluginName.IsNullOrWhiteSpace())
        {
            channel = (await _channelService.GetAllAsync().ConfigureAwait(false)).Where(a => a.PluginName == exportFilter.PluginName).Select(a => a.Id).ToHashSet();
        }
        if (exportFilter.PluginType != null)
        {
            var pluginInfo = GlobalData.PluginService.GetList(exportFilter.PluginType).Select(a => a.FullName).ToHashSet();
            channel = (await _channelService.GetAllAsync().ConfigureAwait(false)).Where(a => pluginInfo.Contains(a.PluginName)).Select(a => a.Id).ToHashSet();
        }
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        return await QueryAsync(exportFilter.QueryPageOptions, a => a
     .WhereIF(channel != null, a => channel.Contains(a.ChannelId))
     .WhereIF(exportFilter.DeviceId != null, a => a.Id == exportFilter.DeviceId)
     .WhereIF(!exportFilter.QueryPageOptions.SearchText.IsNullOrWhiteSpace(), a => a.Name.Contains(exportFilter.QueryPageOptions.SearchText!))
              .WhereIF(dataScope != null && dataScope?.Count > 0, u => dataScope.Contains(u.CreateOrgId))//在指定机构列表查询
         .WhereIF(dataScope?.Count == 0, u => u.CreateUserId == UserManager.UserId)
   , exportFilter.FilterKeyValueAction).ConfigureAwait(false);

    }

    /// <summary>
    /// 保存设备
    /// </summary>
    /// <param name="input">设备</param>
    /// <param name="type">保存类型</param>
    [OperDesc("SaveDevice", localizerType: typeof(Device))]
    public async Task<bool> SaveDeviceAsync(Device input, ItemChangedType type)
    {
        if ((await GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name).TryGetValue(input.Name, out var device))
        {
            if (device.Id != input.Id)
            {
                throw Oops.Bah(Localizer["NameDump", device.Name]);
            }
        }
        if (type == ItemChangedType.Update)
            await GlobalData.SysUserService.CheckApiDataScopeAsync(input.CreateOrgId, input.CreateUserId).ConfigureAwait(false);
        else
            ManageHelper.CheckDeviceCount(1);

        if (await base.SaveAsync(input, type).ConfigureAwait(false))
        {
            DeleteDeviceFromCache();
            return true;
        }
        return false;
    }

    [OperDesc("SaveDevice", localizerType: typeof(Device), isRecordPar: false)]
    public async Task<bool> BatchSaveDeviceAsync(List<Device> input, ItemChangedType type)
    {

        if (type == ItemChangedType.Update)
            await GlobalData.SysUserService.CheckApiDataScopeAsync(input.Select(a => a.CreateOrgId), input.Select(a => a.CreateUserId)).ConfigureAwait(false);
        else
            ManageHelper.CheckDeviceCount(input.Count);

        if (await base.SaveAsync(input, type).ConfigureAwait(false))
        {
            DeleteDeviceFromCache();
            return true;
        }
        return false;

    }



    #region 导出

    /// <summary>
    /// 导出文件
    /// </summary>
    /// <returns></returns>
    [OperDesc("ExportDevice", isRecordPar: false, localizerType: typeof(Device))]
    public async Task<Dictionary<string, object>> ExportDeviceAsync(ExportFilter exportFilter)
    {
        //导出
        var data = await PageAsync(exportFilter).ConfigureAwait(false);
        var sheets = await DeviceServiceHelpers.ExportCoreAsync(data.Items).ConfigureAwait(false);
        return sheets;
    }

    /// <summary>
    /// 导出文件
    /// </summary>
    [OperDesc("ExportDevice", isRecordPar: false, localizerType: typeof(Device))]
    public async Task<MemoryStream> ExportMemoryStream(IEnumerable<Device>? data, string channelName = null, string plugin = null)
    {
        var sheets = await DeviceServiceHelpers.ExportCoreAsync(data, channelName, plugin).ConfigureAwait(false);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(sheets).ConfigureAwait(false);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }




    #endregion 导出

    #region 导入

    /// <inheritdoc/>
    [OperDesc("ImportDevice", isRecordPar: false, localizerType: typeof(Device))]
    public async Task<HashSet<long>> ImportDeviceAsync(Dictionary<string, ImportPreviewOutputBase> input)
    {
        var devices = new List<Device>();
        foreach (var item in input)
        {
            if (item.Key == ExportString.DeviceName)
            {
                var deviceImports = ((ImportPreviewOutput<Device>)item.Value).Data;
                devices = deviceImports.Select(a => a.Value).ToList();
                break;
            }
        }
        var upData = devices.Where(a => a.IsUp).ToList();
        var insertData = devices.Where(a => !a.IsUp).ToList();

        ManageHelper.CheckDeviceCount(insertData.Count);

        using var db = GetDB();
        await db.BulkCopyAsync(insertData, 100000).ConfigureAwait(false);
        await db.BulkUpdateAsync(upData, 100000).ConfigureAwait(false);
        DeleteDeviceFromCache();
        return devices.Select(a => a.Id).ToHashSet();
    }

    public async Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile)
    {
        var path = await browserFile.StorageLocal().ConfigureAwait(false); // 上传文件并获取文件路径
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

        try
        {
            // 获取 Excel 文件中所有工作表的名称
            var sheetNames = MiniExcel.GetSheetNames(path);

            // 获取所有设备，并将设备名称作为键构建设备字典
            var deviceDicts = (await GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 获取所有通道，并将通道名称作为键构建通道字典
            var channelDicts = (await _channelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 导入检验结果的预览字典，键为名称，值为导入预览对象
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();

            // 设备页的导入预览对象
            ImportPreviewOutput<Device> deviceImportPreview = new();

            // 获取所有驱动程序，并将驱动程序名称作为键构建字典
            var driverPluginNameDict = _pluginService.GetList().DistinctBy(a => a.Name).ToDictionary(a => a.Name);
            ConcurrentDictionary<string, (Type, Dictionary<string, PropertyInfo>, Dictionary<string, PropertyInfo>)> propertysDict = new();
            foreach (var sheetName in sheetNames)
            {
                var rows = MiniExcel.Query(path, useHeaderRow: true, sheetName: sheetName).Cast<IDictionary<string, object>>();

                SetDeviceData(dataScope, deviceDicts, channelDicts, ImportPreviews, ref deviceImportPreview, driverPluginNameDict, propertysDict, sheetName, rows);
            }

            return ImportPreviews;
        }
        finally
        {
            FileUtility.Delete(path);
        }
    }

    public void SetDeviceData(HashSet<long>? dataScope, Dictionary<string, Device> deviceDicts, Dictionary<string, Channel> channelDicts, Dictionary<string, ImportPreviewOutputBase> ImportPreviews, ref ImportPreviewOutput<Device> deviceImportPreview, Dictionary<string, PluginInfo> driverPluginNameDict, ConcurrentDictionary<string, (Type, Dictionary<string, PropertyInfo>, Dictionary<string, PropertyInfo>)> propertysDict, string sheetName, IEnumerable<IDictionary<string, object>> rows)
    {
        #region 采集设备sheet

        if (sheetName == ExportString.DeviceName)
        {
            // 初始化行数
            int row = 1;

            // 创建导入预览输出对象，并将其添加到导入预览集合中
            ImportPreviewOutput<Device> importPreviewOutput = new();
            ImportPreviews.Add(sheetName, importPreviewOutput);

            // 为设备导入预览对象赋值
            deviceImportPreview = importPreviewOutput;

            // 创建设备列表
            List<Device> devices = new();
            var type = typeof(Device);
            // 获取目标类型的所有属性，并根据是否需要过滤 IgnoreExcelAttribute 进行筛选
            var deviceProperties = type.GetRuntimeProperties().Where(a => (a.GetCustomAttribute<IgnoreExcelAttribute>() == null) && a.CanWrite)
                                        .ToDictionary(a => type.GetPropertyDisplayName(a.Name), a => (a, a.IsNullableType()));

            // 遍历每一行数据
            rows.ForEach(item =>
            {
                try
                {
                    // 尝试将导入的项转换为 Device 对象
                    var device = item.ConvertToEntity<Device>(deviceProperties);

                    // 如果转换失败，则添加错误信息到导入预览结果并返回
                    if (device == null)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["ImportNullError"]));
                        return;
                    }

                    // 转换冗余设备名称
                    var hasRedundant = item.TryGetValue(ExportString.RedundantDeviceName, out var redundantObj);
                    var hasChannel = item.TryGetValue(ExportString.ChannelName, out var channelObj);

                    // 设备ID、冗余设备ID都需要手动补录
                    if (hasRedundant && redundantObj != null)
                    {
                        if (deviceDicts.TryGetValue(redundantObj.ToString(), out var redundantDevice))
                            device.RedundantDeviceId = redundantDevice.Id;
                        else
                        {
                            // 如果找不到对应的冗余设备，则添加错误信息到导入预览结果并返回
                            importPreviewOutput.HasError = true;
                            importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["RedundantDeviceError"]));
                            return;
                        }
                    }
                    else
                    {
                        // 如果冗余设备未启用，则添加错误信息到导入预览结果并返回
                        if (device.RedundantEnable)
                        {
                            importPreviewOutput.HasError = true;
                            importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["RedundantDeviceError"]));
                            return;
                        }
                    }

                    // 检查是否提供了通道信息，如果是，则尝试将其转换为通道对象并关联到设备
                    if (hasChannel && channelObj != null)
                    {
                        if (channelDicts.TryGetValue(channelObj.ToString(), out var channel))
                            device.ChannelId = channel.Id;
                        else
                        {
                            // 如果找不到对应的通道信息，则添加错误信息到导入预览结果并返回
                            importPreviewOutput.HasError = true;
                            importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["ChannelError"]));
                            return;
                        }
                    }
                    else
                    {
                        // 如果未提供通道信息，则添加错误信息到导入预览结果并返回
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["ChannelError"]));
                        return;
                    }

                    // 进行设备对象属性的验证
                    var validationContext = new ValidationContext(device);
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
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, stringBuilder.ToString()));
                        return;
                    }

                    // 检查设备名称是否已存在于设备字典中，如果存在，则更新设备ID；否则，生成新的设备ID
                    if (deviceDicts.TryGetValue(device.Name, out var existingDevice))
                    {
                        device.Id = existingDevice.Id;
                        device.CreateOrgId = existingDevice.CreateOrgId;
                        device.CreateUserId = existingDevice.CreateUserId;
                        device.IsUp = true;
                    }
                    else
                    {
                        device.Id = CommonUtils.GetSingleId();
                        device.IsUp = false;
                        device.CreateOrgId = UserManager.OrgId;
                        device.CreateUserId = UserManager.UserId;
                    }

                    // 将设备添加到设备列表中，并添加成功信息到导入预览结果
                    if (device.IsUp && ((dataScope != null && dataScope?.Count > 0 && !dataScope.Contains(device.CreateOrgId)) || dataScope?.Count == 0 && device.CreateUserId != UserManager.UserId))
                    {
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, "Operation not permitted"));
                    }
                    else
                    {
                        devices.Add(device);
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), true, null));
                    }
                    return;
                }
                catch (Exception ex)
                {
                    // 捕获异常并添加错误信息到导入预览结果
                    importPreviewOutput.HasError = true;
                    importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, ex.Message));
                    return;
                }
            });

            // 将设备列表转换为字典，并赋值给导入预览输出对象的 Data 属性
            importPreviewOutput.Data = devices.ToDictionary(a => a.Name);
        }

        #endregion 采集设备sheet

        else
        {
            // 如果设备导入预览数据为空或者数量为0，则直接返回导入预览集合
            if (deviceImportPreview.Data == null || deviceImportPreview.Data.Count == 0)
                return;

            // 初始化行数
            int row = 1;

            // 创建导入预览输出对象
            ImportPreviewOutput<string> importPreviewOutput = new();

            // 将导入预览输出对象添加到导入预览集合中
            ImportPreviews.Add(sheetName, importPreviewOutput);

            // 插件属性需加上前置名称
            _ = driverPluginNameDict.TryGetValue(sheetName, out var driverPluginType);

            // 如果未找到驱动插件类型，则添加错误信息到导入预览结果并继续下一轮循环
            if (driverPluginType == null)
            {
                importPreviewOutput.HasError = true;
                importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["NotNull", sheetName]));
                return;
            }

            if (propertysDict.TryGetValue(driverPluginType.FullName, out var propertys))
            {
            }
            else
            {
                try
                {


                    // 获取驱动插件实例
                    var driver = _pluginService.GetDriver(driverPluginType.FullName);
                    var type = driver.DriverProperties.GetType();

                    propertys.Item1 = type;

                    propertys.Item2 = type.GetRuntimeProperties()
                        .Where(a => a.GetCustomAttribute<DynamicPropertyAttribute>() != null && a.CanWrite)
                        .ToDictionary(a => type.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description));

                    // 获取目标类型的所有属性，并根据是否需要过滤 IgnoreExcelAttribute 进行筛选
                    var properties = propertys.Item1.GetRuntimeProperties().Where(a => (a.GetCustomAttribute<IgnoreExcelAttribute>() == null) && a.CanWrite)
                                    .ToDictionary(a => propertys.Item1.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description));

                    propertys.Item3 = properties;
                    propertysDict.TryAdd(driverPluginType.FullName, propertys);
                }
                catch
                {

                }
            }

            // 遍历每一行数据
            foreach (var item in rows)
            {
                try
                {
                    if (propertys.Item1 == null)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["PluginNotNull"]));
                        continue;
                    }

                    // 获取设备名称
                    if (!item.TryGetValue(ExportString.DeviceName, out var deviceName))
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["DeviceNotNull"]));
                        continue;
                    }

                    // 转化插件名称
                    var value = item[ExportString.DeviceName]?.ToString();

                    // 检查设备名称是否存在于设备导入预览数据中，如果不存在，则添加错误信息到导入预览结果并继续下一轮循环
                    var hasDevice = deviceImportPreview.Data.ContainsKey(value);
                    if (!hasDevice)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["NotNull", value]));
                        continue;
                    }

                    // 尝试将导入的项转换为对象
                    var pluginProp = item.ConvertToEntity(propertys.Item1, propertys.Item3);

                    // 如果转换失败，则添加错误信息到导入预览结果并返回
                    if (pluginProp == null)
                    {
                        importPreviewOutput.HasError = true;
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, Localizer["ImportNullError"]));
                        return;
                    }

                    // 检查属性的验证结果
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
                        importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, stringBuilder.ToString()));
                        return;
                    }

                    // 将动态属性映射到设备属性字典中
                    Dictionary<string, string> devices = new();
                    foreach (var keyValuePair in item)
                    {
                        if (propertys.Item2.TryGetValue(keyValuePair.Key, out var propertyInfo))
                        {
                            devices.Add(propertyInfo.Name, keyValuePair.Value?.ToString());
                        }
                    }

                    // 更新设备导入预览数据中对应设备的属性信息，并添加成功信息到导入预览结果
                    deviceImportPreview.Data[value].DevicePropertys = devices;
                    importPreviewOutput.Results.Add((Interlocked.Increment(ref row), true, null));
                    continue;
                }
                catch (Exception ex)
                {
                    // 捕获异常并添加错误信息到导入预览结果并返回
                    importPreviewOutput.HasError = true;
                    importPreviewOutput.Results.Add((Interlocked.Increment(ref row), false, ex.Message));
                    return;
                }
            }
        }
    }

    #endregion 导入
}
