// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using Mapster;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

using ThingsGateway.Extension.Generic;
using ThingsGateway.NewLife.Collections;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

public class VariableRuntimeService : IVariableRuntimeService
{
    //private WaitLock WaitLock { get; set; } = new WaitLock();
    private ILogger _logger;
    public VariableRuntimeService(ILogger<VariableRuntimeService> logger)
    {
        _logger = logger;
    }



    public async Task AddBatchAsync(List<Variable> input, bool restart = true)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            await GlobalData.VariableService.AddBatchAsync(input).ConfigureAwait(false);

            var newVariableRuntimes = input.Adapt<List<VariableRuntime>>();
            var ids = newVariableRuntimes.Select(a => a.Id).ToHashSet();
            //获取变量，先找到原插件线程，然后修改插件线程内的字典，再改动全局字典，最后刷新插件
            var data = GlobalData.IdVariables.Where(a => ids.Contains(a.Key)).GroupBy(a => a.Value.DeviceRuntime);

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a =>

                 GlobalData.ContainsVariable(a.Key, item.Value)

).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Value.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }

            //批量修改之后，需要重新加载
            foreach (var newVariableRuntime in newVariableRuntimes)
            {
                if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
                {
                    newVariableRuntime.Init(deviceRuntime);

                    if (deviceRuntime.Driver != null && !changedDriver.Contains(deviceRuntime.Driver))
                    {
                        changedDriver.TryAdd(deviceRuntime.Driver);
                    }
                }
            }
            if (restart)
            {
                //根据条件重启通道线程
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }

        }
        finally
        {
            //WaitLock.Release();
        }
    }

    public async Task<bool> BatchEditAsync(IEnumerable<Variable> models, Variable oldModel, Variable model, bool restart = true)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            models = models.Adapt<List<Variable>>();
            oldModel = oldModel.Adapt<Variable>();
            model = model.Adapt<Variable>();

            var result = await GlobalData.VariableService.BatchEditAsync(models, oldModel, model).ConfigureAwait(false);

            using var db = DbContext.GetDB<Variable>();
            var ids = models.Select(a => a.Id).ToHashSet();

            var newVariableRuntimes = (await db.Queryable<Variable>().Where(a => ids.Contains(a.Id)).ToListAsync().ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

            var newVarIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();

            //获取变量，先找到原插件线程，然后修改插件线程内的字典，再改动全局字典，最后刷新插件
            var data = GlobalData.IdVariables.Where(a => newVarIds.Contains(a.Key)).GroupBy(a => a.Value.DeviceRuntime);

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, item.Value)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Value.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }

            //批量修改之后，需要重新加载
            foreach (var newVariableRuntime in newVariableRuntimes)
            {
                if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
                {
                    newVariableRuntime.Init(deviceRuntime);

                    if (deviceRuntime.Driver != null && !changedDriver.Contains(deviceRuntime.Driver))
                    {
                        changedDriver.TryAdd(deviceRuntime.Driver);
                    }
                }
            }
            if (restart)
            {
                //根据条件重启通道线程
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }

            return true;

        }
        finally
        {
            //WaitLock.Release();
        }
    }

    public async Task<bool> DeleteVariableAsync(IEnumerable<long> ids, bool restart = true)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);


            ids = ids.ToHashSet();

            var result = await GlobalData.VariableService.DeleteVariableAsync(ids).ConfigureAwait(false);

            var variableRuntimes = GlobalData.IdVariables.Where(a => ids.Contains(a.Key)).Select(a => a.Value);


            var data = variableRuntimes.Where(a => a.DeviceRuntime?.Driver != null).GroupBy(a => a.DeviceRuntime).ToDictionary(a => a.Key, a => a.ToList());

            foreach (var variableRuntime in variableRuntimes)
            {
                variableRuntime.Dispose();
            }

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group.Value)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, item)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }
            if (restart)
            {
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }



            return true;
        }
        finally
        {
            //WaitLock.Release();
        }


    }
    public Task<Dictionary<string, IList<Dictionary<string, object>>>> ExportVariableAsync(ExportFilter exportFilter) => GlobalData.VariableService.ExportVariableAsync(exportFilter);

    public async Task ImportVariableAsync(Dictionary<string, ImportPreviewOutputBase> input, bool restart = true)
    {

        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.VariableService.ImportVariableAsync(input).ConfigureAwait(false);


            using var db = DbContext.GetDB<Variable>();
            var newVariableRuntimes = (await db.Queryable<Variable>().Where(a => result.Contains(a.Id)).ToListAsync().ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

            var newVarIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();
            //先找出线程管理器，停止
            var data = GlobalData.IdVariables.Where(a => newVarIds.Contains(a.Key)).GroupBy(a => a.Value.DeviceRuntime);

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, item.Value)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Value.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }

            //批量修改之后，需要重新加载
            foreach (var newVariableRuntime in newVariableRuntimes)
            {
                if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
                {
                    newVariableRuntime.Init(deviceRuntime);
                    //添加新变量所在任务
                    if (deviceRuntime.Driver != null && !changedDriver.Contains(deviceRuntime.Driver))
                    {
                        changedDriver.TryAdd(deviceRuntime.Driver);
                    }
                }
            }
            if (restart)
            {
                //根据条件重启通道线程

                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }



        }
        finally
        {
            //WaitLock.Release();
        }

    }

    public async Task InsertTestDataAsync(int testVariableCount, int testDeviceCount, string slaveUrl, bool restart = true)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);



            var datas = await GlobalData.VariableService.InsertTestDataAsync(testVariableCount, testDeviceCount, slaveUrl).ConfigureAwait(false);

            {
                var newChannelRuntimes = (datas.Item1).Adapt<List<ChannelRuntime>>();

                //批量修改之后，需要重新加载通道
                foreach (var newChannelRuntime in newChannelRuntimes)
                {
                    if (GlobalData.Channels.TryGetValue(newChannelRuntime.Id, out var channelRuntime))
                    {
                        channelRuntime.Dispose();
                        newChannelRuntime.Init();
                        channelRuntime.DeviceRuntimes.ForEach(a => a.Value.Init(newChannelRuntime));

                        newChannelRuntime.DeviceRuntimes.AddRange(channelRuntime.DeviceRuntimes);
                    }
                    else
                    {
                        newChannelRuntime.Init();

                    }
                }

                {

                    var newDeviceRuntimes = (datas.Item2).Adapt<List<DeviceRuntime>>();

                    //批量修改之后，需要重新加载通道
                    foreach (var newDeviceRuntime in newDeviceRuntimes)
                    {
                        if (GlobalData.IdDevices.TryGetValue(newDeviceRuntime.Id, out var deviceRuntime))
                        {
                            deviceRuntime.Dispose();
                        }
                        if (GlobalData.Channels.TryGetValue(newDeviceRuntime.ChannelId, out var channelRuntime))
                        {
                            newDeviceRuntime.Init(channelRuntime);
                        }
                        if (deviceRuntime != null)
                        {
                            deviceRuntime.VariableRuntimes.ParallelForEach(a => a.Value.Init(newDeviceRuntime));
                        }
                    }


                }
                {
                    var newVariableRuntimes = (datas.Item3).Adapt<List<VariableRuntime>>();
                    //获取变量，先找到原插件线程，然后修改插件线程内的字典，再改动全局字典，最后刷新插件
                    var newVarIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();
                    var data = GlobalData.IdVariables.Where(a => newVarIds.Contains(a.Key)).GroupBy(a => a.Value.DeviceRuntime);

                    foreach (var group in data)
                    {
                        //这里改动的可能是旧绑定设备
                        //需要改动DeviceRuntim的变量字典
                        foreach (var item in group)
                        {
                            item.Value.Dispose();
                        }
                    }

                    //批量修改之后，需要重新加载
                    foreach (var newVariableRuntime in newVariableRuntimes)
                    {
                        if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
                        {
                            newVariableRuntime.Init(deviceRuntime);
                        }
                    }

                }
                //根据条件重启通道线程

                if (restart)
                {
                    await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntimes).ConfigureAwait(false);

                    var channelDevice = GlobalData.IdDevices.Where(a => a.Value.Driver?.DriverProperties is IBusinessPropertyAllVariableBase property && property.IsAllVariable);

                    foreach (var item in channelDevice)
                    {
                        await item.Value.Driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                }


                App.GetService<IDispatchService<DeviceRuntime>>().Dispatch(null);
            }
        }
        finally
        {
            //WaitLock.Release();
        }

    }

    public Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile)
    {
        return GlobalData.VariableService.PreviewAsync(browserFile);
    }

    public async Task<bool> SaveVariableAsync(Variable input, ItemChangedType type, bool restart = true)
    {
        try
        {
            input = input.Adapt<Variable>();
            // await WaitLock.WaitAsync().ConfigureAwait(false);



            var result = await GlobalData.VariableService.SaveVariableAsync(input, type).ConfigureAwait(false);


            using var db = DbContext.GetDB<Variable>();
            var newVariableRuntime = (await db.Queryable<Variable>().Where(a => input.Id == a.Id).FirstAsync().ConfigureAwait(false)).Adapt<VariableRuntime>();

            if (newVariableRuntime == null) return false;

            ConcurrentHashSet<IDriver> changedDriver = new();



            //这里改动的可能是旧绑定设备
            //需要改动DeviceRuntim的变量字典

            if (GlobalData.IdVariables.TryGetValue(newVariableRuntime.Id, out var variableRuntime))
            {
                if (variableRuntime.DeviceRuntime?.Driver != null)
                {
                    changedDriver.TryAdd(variableRuntime.DeviceRuntime.Driver);
                }
                variableRuntime.Dispose();
            }

            //需要重启业务线程
            var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, newVariableRuntime)).Select(a => a.Value);
            foreach (var businessDeviceRuntime in deviceRuntimes)
            {
                if (businessDeviceRuntime.Driver != null)
                {
                    changedDriver.TryAdd(businessDeviceRuntime.Driver);
                }
            }

            //批量修改之后，需要重新加载

            if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
            {
                newVariableRuntime.Init(deviceRuntime);

                if (deviceRuntime.Driver != null && !changedDriver.Contains(deviceRuntime.Driver))
                {
                    changedDriver.TryAdd(deviceRuntime.Driver);
                }

            }

            //根据条件重启通道线程
            if (restart)
            {
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }


            return true;
        }
        finally
        {
            //WaitLock.Release();
        }
    }

    public void PreheatCache() => GlobalData.VariableService.PreheatCache();


    public Task<MemoryStream> ExportMemoryStream(List<Variable> data, string deviceName) => GlobalData.VariableService.ExportMemoryStream(data, deviceName);



    public async Task AddDynamicVariable(IEnumerable<VariableRuntime> newVariableRuntimes, bool restart = true)
    {
        //动态变量不入配置数据库
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            var newVarIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();
            //获取变量，先找到原插件线程，然后修改插件线程内的字典，再改动全局字典，最后刷新插件
            var data = GlobalData.IdVariables.Where(a => newVarIds.Contains(a.Key)).GroupBy(a => a.Value.DeviceRuntime);

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, item.Value)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Value.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }

            //批量修改之后，需要重新加载
            foreach (var newVariableRuntime in newVariableRuntimes)
            {
                newVariableRuntime.DynamicVariable = true;
                if (GlobalData.IdDevices.TryGetValue(newVariableRuntime.DeviceId, out var deviceRuntime))
                {
                    newVariableRuntime.Init(deviceRuntime);

                    if (deviceRuntime.Driver != null && !changedDriver.Contains(deviceRuntime.Driver))
                    {
                        changedDriver.TryAdd(deviceRuntime.Driver);
                    }
                }
            }
            if (restart)
            {
                //根据条件重启通道线程
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }

            }
        }
        finally
        {
            //WaitLock.Release();
        }

    }


    public async Task DeleteDynamicVariable(IEnumerable<long> variableIds, bool restart = true)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);
            var ids = variableIds.ToHashSet();

            var variableRuntimes = GlobalData.IdVariables.Where(a => ids.Contains(a.Key)).Select(a => a.Value).Where(a => a.DynamicVariable);


            var data = variableRuntimes.Where(a => a.DeviceRuntime?.Driver != null).GroupBy(a => a.DeviceRuntime).ToDictionary(a => a.Key, a => a.ToList());

            foreach (var variableRuntime in variableRuntimes)
            {
                variableRuntime.Dispose();
            }

            ConcurrentHashSet<IDriver> changedDriver = new();
            foreach (var group in data)
            {
                //这里改动的可能是旧绑定设备
                //需要改动DeviceRuntim的变量字典
                foreach (var item in group.Value)
                {
                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, item)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }

                    item.Dispose();
                }
                if (group.Key != null)
                {
                    if (group.Key.Driver != null)
                    {
                        changedDriver.TryAdd(group.Key.Driver);
                    }
                }
            }
            if (restart)
            {
                foreach (var driver in changedDriver)
                {
                    try
                    {
                        await driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VariablesChanged");
                    }
                }
            }



        }
        finally
        {
            //WaitLock.Release();
        }


    }

}