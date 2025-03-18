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

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

public static class VariableServiceHelpers
{

    public static async Task<USheetDatas> ExportVariableAsync(IEnumerable<Variable> models)
    {
        var data = await ExportCoreAsync(models).ConfigureAwait(false);

        return USheetDataHelpers.GetUSheetDatas(data);
    }

    public static async Task<Dictionary<string, IList<Dictionary<string, object>>>> ExportCoreAsync(IEnumerable<Variable> data, string deviceName = null)
    {
        if (data == null || !data.Any())
        {
            data = new List<Variable>();
        }
        var deviceDicts = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        var channelDicts = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Id);
        var driverPluginDicts = GlobalData.PluginService.GetList(PluginTypeEnum.Business).ToDictionary(a => a.FullName);
        //总数据
        Dictionary<string, IList<Dictionary<string, object>>> sheets = new();
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
                    if (!(deviceDicts.TryGetValue(item.Key, out var businessDevice) && deviceDicts.TryGetValue(variable.DeviceId, out var collectDevice)))
                        continue;

                    channelDicts.TryGetValue(businessDevice.ChannelId, out var channel);

                    //没有包含设备名称，手动插入
                    driverInfo.TryAdd(ExportString.DeviceName, collectDevice.Name);
                    driverInfo.TryAdd(ExportString.BusinessDeviceName, businessDevice.Name);
                    driverInfo.TryAdd(ExportString.VariableName, variable.Name);

                    var propDict = item.Value;

                    if (propertysDict.TryGetValue(channel.PluginName, out var propertys))
                    {
                    }
                    else
                    {
                        try
                        {

                            var variableProperty = ((BusinessBase)GlobalData.PluginService.GetDriver(channel.PluginName))?.VariablePropertys;
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

    /// <inheritdoc/>
    public static async Task<Dictionary<string, ImportPreviewOutputBase>> ImportAsync(USheetDatas uSheetDatas)
    {
        var dataScope = await GlobalData.SysUserService.GetCurrentUserDataScopeAsync().ConfigureAwait(false);

        try
        {

            // 获取所有设备的字典，以设备名称作为键
            var deviceDicts = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).ToDictionary(a => a.Name);

            // 存储导入检验结果的字典
            Dictionary<string, ImportPreviewOutputBase> ImportPreviews = new();

            // 设备页导入预览输出
            ImportPreviewOutput<Dictionary<string, Variable>> deviceImportPreview = new();

            // 获取驱动插件的全名和名称的字典
            var driverPluginFullNameDict = GlobalData.PluginService.GetList().ToDictionary(a => a.FullName);
            var driverPluginNameDict = GlobalData.PluginService.GetList().ToDictionary(a => a.Name);
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

                deviceImportPreview = await GlobalData.VariableService.SetVariableData(dataScope, deviceDicts, ImportPreviews, deviceImportPreview, driverPluginNameDict, propertysDict, sheetName, rows).ConfigureAwait(false);
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