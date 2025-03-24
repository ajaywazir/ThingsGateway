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

using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Collections;

namespace ThingsGateway.Gateway.Application;

public class DeviceRuntimeService : IDeviceRuntimeService
{
    private ILogger _logger;
    public DeviceRuntimeService(ILogger<DeviceRuntimeService> logger)
    {
        _logger = logger;
    }

    private WaitLock WaitLock { get; set; } = new WaitLock();


    public async Task<bool> CopyAsync(Dictionary<Device, List<Variable>> devices, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.CopyAsync(devices).ConfigureAwait(false);

            var deviceids = devices.Select(a => a.Key.Id).ToHashSet();
            var newDeviceRuntimes = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).Where(a => deviceids.Contains(a.Id)).Adapt<List<DeviceRuntime>>();

            foreach (var newDeviceRuntime in newDeviceRuntimes)
            {
                if (GlobalData.Channels.TryGetValue(newDeviceRuntime.ChannelId, out var newChannelRuntime))
                {
                    newDeviceRuntime.Init(newChannelRuntime);

                    var newVariableRuntimes = (await GlobalData.VariableService.GetAllAsync(newDeviceRuntime.Id).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

                    newVariableRuntimes.ParallelForEach(item =>
                    {
                        item.Init(newDeviceRuntime);
                    });
                }
                else
                {
                    throw new("Channel not found");
                }
            }

            //根据条件重启通道线程
            if (restart)
            {
                foreach (var group in newDeviceRuntimes.Where(a => a.ChannelRuntime?.DeviceThreadManage != null).GroupBy(a => a.ChannelRuntime))
                {
                    if (group.Key?.DeviceThreadManage != null)
                        await group.Key.DeviceThreadManage.RestartDeviceAsync(group, false).ConfigureAwait(false);
                }

                var channelDevice = GlobalData.IdDevices.Where(a => a.Value.Driver?.DriverProperties is IBusinessPropertyAllVariableBase property && property.IsAllVariable);

                foreach (var item in channelDevice)
                {
                    await item.Value.Driver.AfterVariablesChangedAsync().ConfigureAwait(false);
                }

            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }


    public async Task<bool> BatchEditAsync(IEnumerable<Device> models, Device oldModel, Device model, bool restart = true)
    {
        try
        {
            models = models.Adapt<List<Device>>();
            oldModel = oldModel.Adapt<Device>();
            model = model.Adapt<Device>();
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.BatchEditAsync(models, oldModel, model).ConfigureAwait(false);
            var ids = models.Select(a => a.Id).ToHashSet();
            var newDeviceRuntimes = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).Where(a => ids.Contains(a.Id)).Adapt<List<DeviceRuntime>>();

            if (restart)
            {
                var newDeciceIds = newDeviceRuntimes.Select(a => a.Id).ToHashSet();

                //先找出线程管理器，停止
                var data = GlobalData.IdDevices.Where(a => newDeciceIds.Contains(a.Key)).GroupBy(a => a.Value.ChannelRuntime?.DeviceThreadManage);
                foreach (var group in data)
                {
                    if (group.Key != null)
                        await group.Key.RemoveDeviceAsync(group.Select(a => a.Value.Id)).ConfigureAwait(false);
                }
            }

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

            //根据条件重启通道线程

            if (restart)
            {
                foreach (var group in newDeviceRuntimes.Where(a => a.ChannelRuntime?.DeviceThreadManage != null).GroupBy(a => a.ChannelRuntime))
                {
                    if (group.Key?.DeviceThreadManage != null)
                        await group.Key.DeviceThreadManage.RestartDeviceAsync(group, false).ConfigureAwait(false);
                }
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }

    public async Task<bool> DeleteDeviceAsync(IEnumerable<long> ids, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);


            ids = ids.ToHashSet();

            var result = await GlobalData.DeviceService.DeleteDeviceAsync(ids).ConfigureAwait(false);

            //根据条件重启通道线程
            var deviceRuntimes = GlobalData.IdDevices.Where(a => ids.Contains(a.Key)).Select(a => a.Value).ToList();


            ConcurrentHashSet<IDriver> changedDriver = new();

            foreach (var deviceRuntime in deviceRuntimes)
            {
                //也需要删除变量
                deviceRuntime.VariableRuntimes.ParallelForEach(v =>
                {

                    //需要重启业务线程
                    var deviceRuntimes = GlobalData.IdDevices.Where(a => GlobalData.ContainsVariable(a.Key, v.Value)).Select(a => a.Value);
                    foreach (var deviceRuntime in deviceRuntimes)
                    {
                        if (deviceRuntime.Driver != null)
                        {
                            changedDriver.TryAdd(deviceRuntime.Driver);
                        }
                    }


                    v.Value.Dispose();
                });
                deviceRuntime.Dispose();
            }

            if (restart)
            {
                var groups = GlobalData.GetDeviceThreadManages(deviceRuntimes);
                foreach (var group in groups)
                {
                    if (group.Key != null)
                        await group.Key.RemoveDeviceAsync(group.Value.Select(a => a.Id)).ConfigureAwait(false);
                }

                var changedDrivers = changedDriver.Where(a => a.DisposedValue == false && a.IsCollectDevice == false).ToHashSet();
                foreach (var driver in changedDrivers)
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
            WaitLock.Release();
        }
    }

    public Task<Dictionary<string, object>> ExportDeviceAsync(ExportFilter exportFilter) => GlobalData.DeviceService.ExportDeviceAsync(exportFilter);
    public Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile) => GlobalData.DeviceService.PreviewAsync(browserFile);
    public Task<MemoryStream> ExportMemoryStream(List<Device> data, string channelName) =>
          GlobalData.DeviceService.ExportMemoryStream(data, channelName);


    public async Task ImportDeviceAsync(Dictionary<string, ImportPreviewOutputBase> input, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.ImportDeviceAsync(input).ConfigureAwait(false);

            var newDeviceRuntimes = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).Where(a => result.Contains(a.Id)).Adapt<List<DeviceRuntime>>();

            if (restart)
            {
                var newDeciceIds = newDeviceRuntimes.Select(a => a.Id).ToHashSet();

                //先找出线程管理器，停止
                var data = GlobalData.IdDevices.Where(a => newDeciceIds.Contains(a.Key)).GroupBy(a => a.Value.ChannelRuntime?.DeviceThreadManage);
                foreach (var group in data)
                {
                    if (group.Key != null)
                        await group.Key.RemoveDeviceAsync(group.Select(a => a.Value.Id)).ConfigureAwait(false);
                }
            }

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

            //根据条件重启通道线程
            if (restart)
            {
                foreach (var group in newDeviceRuntimes.Where(a => a.ChannelRuntime?.DeviceThreadManage != null).GroupBy(a => a.ChannelRuntime))
                {
                    if (group.Key?.DeviceThreadManage != null)
                        await group.Key.DeviceThreadManage.RestartDeviceAsync(group, false).ConfigureAwait(false);
                }
            }


        }
        finally
        {
            WaitLock.Release();
        }

    }

    public async Task<bool> SaveDeviceAsync(Device input, ItemChangedType type, bool restart = true)
    {
        try
        {
            input = input.Adapt<Device>();

            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.SaveDeviceAsync(input, type).ConfigureAwait(false);

            var newDeviceRuntime = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).FirstOrDefault(a => a.Id == input.Id)?.Adapt<DeviceRuntime>();

            if (newDeviceRuntime == null) return false;


            //批量修改之后，需要重新加载通道
            if (GlobalData.IdDevices.TryGetValue(newDeviceRuntime.Id, out var deviceRuntime))
            {
                if (restart)
                {

                    if (GlobalData.TryGetDeviceThreadManage(deviceRuntime, out var deviceThreadManage))
                        await deviceThreadManage.RemoveDeviceAsync(deviceRuntime.Id).ConfigureAwait(false);
                }
                deviceRuntime.Dispose();
                deviceRuntime.VariableRuntimes.ParallelForEach(a => a.Value.Init(newDeviceRuntime));
            }

            if (GlobalData.Channels.TryGetValue(newDeviceRuntime.ChannelId, out var channelRuntime))
            {
                newDeviceRuntime.Init(channelRuntime);
            }
            if (restart && channelRuntime.DeviceThreadManage != null)
            {
                //根据条件重启通道线程
                await channelRuntime.DeviceThreadManage.RestartDeviceAsync(newDeviceRuntime, false).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }
}