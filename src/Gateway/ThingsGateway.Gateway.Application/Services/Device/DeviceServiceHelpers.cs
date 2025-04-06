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

using System.Collections.Concurrent;
using System.Reflection;

namespace ThingsGateway.Gateway.Application;

public static class DeviceServiceHelpers
{

    public static async Task<USheetDatas> ExportDeviceAsync(IEnumerable<Device> models)
    {

        var data = await ExportCoreAsync(models).ConfigureAwait(false);
        return USheetDataHelpers.GetUSheetDatas(data);

    }


    public static async Task<Dictionary<string, object>> ExportCoreAsync(IEnumerable<Device>? data, string channelName = null, string plugin = null)
    {
        if (data == null || !data.Any())
        {
            data = new List<Device>();
        }
        var deviceDicts = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        var channelDicts = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        //总数据
        Dictionary<string, object> sheets = new();
        //设备页
        List<Dictionary<string, object>> deviceExports = new();
        //设备附加属性，转成Dict<表名,List<Dict<列名，列数据>>>的形式
        Dictionary<string, List<Dictionary<string, object>>> devicePropertys = new();
        ConcurrentDictionary<string, (object, Dictionary<string, PropertyInfo>)> propertysDict = new();

        #region 列名称

        var type = typeof(Device);
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

        foreach (var device in data)
        {
            Dictionary<string, object> devExport = new();
            deviceDicts.TryGetValue(device.RedundantDeviceId ?? 0, out var redundantDevice);
            channelDicts.TryGetValue(device.ChannelId, out var channel);

            devExport.Add(ExportString.ChannelName, channel?.Name ?? channelName);

            foreach (var item in propertyInfos)
            {
                //描述
                var desc = type.GetPropertyDisplayName(item.Name);
                //数据源增加
                devExport.Add(desc ?? item.Name, item.GetValue(device)?.ToString());
            }

            //设备实体没有包含冗余设备名称，手动插入
            devExport.Add(ExportString.RedundantDeviceName, redundantDevice?.Name);

            //添加完整设备信息
            deviceExports.Add(devExport);

            #region 插件sheet

            //插件属性
            //单个设备的行数据
            Dictionary<string, object> driverInfo = new();

            var propDict = device.DevicePropertys;
            if (propertysDict.TryGetValue(channel?.PluginName ?? plugin, out var propertys))
            {
            }
            else
            {
                try
                {
                    var driverProperties = GlobalData.PluginService.GetDriver(channel?.PluginName ?? plugin).DriverProperties;
                    propertys.Item1 = driverProperties;
                    var driverPropertyType = driverProperties.GetType();
                    propertys.Item2 = driverPropertyType.GetRuntimeProperties()
    .Where(a => a.GetCustomAttribute<DynamicPropertyAttribute>() != null)
    .ToDictionary(a => driverPropertyType.GetPropertyDisplayName(a.Name, a => a.GetCustomAttribute<DynamicPropertyAttribute>(true)?.Description), a => a);
                    propertysDict.TryAdd(channel?.PluginName ?? plugin, propertys);

                }
                catch (Exception)
                {

                }

            }

            if (propertys.Item2 == null)
                continue;

            if (propertys.Item2.Count > 0)
            {
                //没有包含设备名称，手动插入
                driverInfo.Add(ExportString.DeviceName, device.Name);
            }
            //根据插件的配置属性项生成列，从数据库中获取值或者获取属性默认值
            foreach (var item in propertys.Item2)
            {
                if (propDict.TryGetValue(item.Value.Name, out var dependencyProperty))
                {
                    driverInfo.Add(item.Key, dependencyProperty);
                }
                else
                {
                    //添加对应属性数据
                    driverInfo.Add(item.Key, ThingsGatewayStringConverter.Default.Serialize(null, item.Value.GetValue(propertys.Item1)));
                }
            }

            var pluginName = PluginServiceUtil.GetFileNameAndTypeName(channel?.PluginName ?? plugin);
            if (devicePropertys.ContainsKey(pluginName.TypeName))
            {
                if (driverInfo.Count > 0)
                    devicePropertys[pluginName.TypeName].Add(driverInfo);
            }
            else
            {
                if (driverInfo.Count > 0)
                    devicePropertys.Add(pluginName.TypeName, new() { driverInfo });
            }

            #endregion 插件sheet
        }
        //添加设备页
        sheets.Add(ExportString.DeviceName, deviceExports);

        //HASH
        foreach (var item in devicePropertys)
        {
            HashSet<string> allKeys = new();

            foreach (var dict in item.Value)
            {
                foreach (var key in dict.Keys)
                {
                    allKeys.Add(key);
                }
            }
            foreach (var dict in item.Value)
            {
                foreach (var key in allKeys)
                {
                    if (!dict.ContainsKey(key))
                    {
                        // 添加缺失的键，并设置默认值
                        dict.Add(key, null);
                    }
                }
            }

            sheets.Add(item.Key, item.Value);
        }

        return sheets;
    }

    public static async Task<Dictionary<string, ImportPreviewOutputBase>> ImportAsync(USheetDatas uSheetDatas)
    {
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);
        try
        {
            // 获取所有设备，并将设备名称作为键构建设备字典
            var deviceDicts = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 获取所有通道，并将通道名称作为键构建通道字典
            var channelDicts = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 导入检验结果的预览字典，键为名称，值为导入预览对象
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();

            // 设备页的导入预览对象
            ImportPreviewOutput<Device> deviceImportPreview = new();

            // 获取所有驱动程序，并将驱动程序的完整名称作为键构建字典
            var driverPluginFullNameDict = GlobalData.PluginService.GetList().ToDictionary(a => a.FullName);

            // 获取所有驱动程序，并将驱动程序名称作为键构建字典
            var driverPluginNameDict = GlobalData.PluginService.GetList().DistinctBy(a => a.Name).ToDictionary(a => a.Name);
            ConcurrentDictionary<string, (Type, Dictionary<string, PropertyInfo>, Dictionary<string, PropertyInfo>)> propertysDict = new();


            var sheetNames = uSheetDatas.sheets.Keys.ToList();
            foreach (var sheetName in sheetNames)
            {

                List<IDictionary<string, object>> rows = new();
                var first = uSheetDatas.sheets[sheetName].cellData[0];

                foreach (var item in uSheetDatas.sheets[sheetName].cellData)
                {
                    if (item.Key == 0)
                    {
                        continue;
                    }
                    var expando = new Dictionary<string, object>();
                    foreach (var keyValue in item.Value)
                    {
                        expando.Add(first[keyValue.Key].v?.ToString(), keyValue.Value.v);
                    }
                    rows.Add(expando);
                }

                GlobalData.DeviceService.SetDeviceData(dataScope, deviceDicts, channelDicts, ImportPreviews, ref deviceImportPreview, driverPluginNameDict, propertysDict, sheetName, rows);
                if (ImportPreviews.Any(a => a.Value.HasError))
                {
                    throw new(ImportPreviews.FirstOrDefault(a => a.Value.HasError).Value.Results.FirstOrDefault(a => !a.Success).ErrorMessage ?? "error");
                }
            }
            return ImportPreviews;
        }
        finally
        {
        }


    }

}