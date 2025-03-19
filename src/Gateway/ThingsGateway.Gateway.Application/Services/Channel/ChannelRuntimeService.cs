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
using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Collections;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

public class ChannelRuntimeService : IChannelRuntimeService
{
    private ILogger _logger;
    public ChannelRuntimeService(ILogger<ChannelRuntimeService> logger)
    {
        _logger = logger;
    }
    private WaitLock WaitLock { get; set; } = new WaitLock();

    public async Task<bool> CopyAsync(List<Channel> models, Dictionary<Device, List<Variable>> devices, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.ChannelService.CopyAsync(models, devices).ConfigureAwait(false);

            var ids = models.Select(a => a.Id).ToHashSet();
            var newChannelRuntimes = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).Where(a => ids.Contains(a.Id)).Adapt<List<ChannelRuntime>>();

            var deviceids = devices.Select(a => a.Key.Id).ToHashSet();
            var newDeviceRuntimes = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).Where(a => deviceids.Contains(a.Id)).Adapt<List<DeviceRuntime>>();


            //批量修改之后，需要重新加载通道
            foreach (var newChannelRuntime in newChannelRuntimes)
            {
                newChannelRuntime.Init();
                foreach (var newDeviceRuntime in newDeviceRuntimes.Where(a => a.ChannelId == newChannelRuntime.Id))
                {
                    newDeviceRuntime.Init(newChannelRuntime);

                    var newVariableRuntimes = (await GlobalData.VariableService.GetAllAsync(newDeviceRuntime.Id).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

                    newVariableRuntimes.ParallelForEach(item =>
                    {
                        item.Init(newDeviceRuntime);
                    });
                }
            }

            //根据条件重启通道线程
            if (restart)
            {
                await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntimes).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }


    public async Task<bool> BatchEditAsync(IEnumerable<Channel> models, Channel oldModel, Channel model, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            models = models.Adapt<List<Channel>>();
            oldModel = oldModel.Adapt<Channel>();
            model = model.Adapt<Channel>();
            var result = await GlobalData.ChannelService.BatchEditAsync(models, oldModel, model).ConfigureAwait(false);
            var ids = models.Select(a => a.Id).ToHashSet();
            var newChannelRuntimes = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).Where(a => ids.Contains(a.Id)).Adapt<List<ChannelRuntime>>();

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

            //根据条件重启通道线程
            if (restart)
            {
                await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntimes).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }

    public async Task<bool> DeleteChannelAsync(IEnumerable<long> ids, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            ids = ids.ToHashSet();
            var result = await GlobalData.ChannelService.DeleteChannelAsync(ids).ConfigureAwait(false);
            ConcurrentHashSet<IDriver> changedDriver = new();

            //批量修改之后，需要重新加载通道
            foreach (var id in ids)
            {
                if (GlobalData.Channels.TryGetValue(id, out var channelRuntime))
                {
                    channelRuntime.Dispose();

                    //也需要删除设备和变量
                    channelRuntime.DeviceRuntimes.ParallelForEach((a =>
                    {

                        ParallelExtensions.ParallelForEach(a.Value.VariableRuntimes, (v =>
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


                        }
                        ));
                        a.Value.Dispose();

                    }));
                }

            }

            //根据条件重启通道线程
            if (restart)
            {
                await GlobalData.ChannelThreadManage.RemoveChannelAsync(ids).ConfigureAwait(false);

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
    public Task<Dictionary<string, ImportPreviewOutputBase>> PreviewAsync(IBrowserFile browserFile) => GlobalData.ChannelService.PreviewAsync(browserFile);

    public Task<Dictionary<string, object>> ExportChannelAsync(ExportFilter exportFilter) => GlobalData.ChannelService.ExportChannelAsync(exportFilter);
    public Task<MemoryStream> ExportMemoryStream(List<Channel> data) =>
      GlobalData.ChannelService.ExportMemoryStream(data);

    public async Task ImportChannelAsync(Dictionary<string, ImportPreviewOutputBase> input, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.ChannelService.ImportChannelAsync(input).ConfigureAwait(false);

            var newChannelRuntimes = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).Where(a => result.Contains(a.Id)).Adapt<List<ChannelRuntime>>();

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

            //根据条件重启通道线程
            if (restart)
                await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntimes).ConfigureAwait(false);

        }

        finally
        {
            WaitLock.Release();
        }
    }
    public async Task<bool> SaveChannelAsync(Channel input, ItemChangedType type, bool restart = true)
    {
        try
        {
            input = input.Adapt<Channel>();
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.ChannelService.SaveChannelAsync(input, type).ConfigureAwait(false);

            var newChannelRuntime = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).FirstOrDefault(a => a.Id == input.Id)?.Adapt<ChannelRuntime>();

            if (newChannelRuntime == null) return false;
            //批量修改之后，需要重新加载通道
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


            //根据条件重启通道线程
            if (restart)
                await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntime).ConfigureAwait(false);

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }


    public async Task RestartChannelAsync(IEnumerable<ChannelRuntime> oldChannelRuntimes)
    {
        oldChannelRuntimes.SelectMany(a => a.DeviceRuntimes.SelectMany(a => a.Value.VariableRuntimes)).ParallelForEach(a => a.Value.SafeDispose());
        oldChannelRuntimes.SelectMany(a => a.DeviceRuntimes).ParallelForEach(a => a.Value.SafeDispose());
        oldChannelRuntimes.ParallelForEach(a => a.SafeDispose());
        var ids = oldChannelRuntimes.Select(a => a.Id).ToHashSet();
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            //网关启动时，获取所有通道
            var channelRuntimes = (await GlobalData.ChannelService.GetAllAsync().ConfigureAwait(false)).Where(a => ids.Contains(a.Id) || !GlobalData.Channels.ContainsKey(a.Id)).Adapt<List<ChannelRuntime>>();

            var chanelIds = channelRuntimes.Select(a => a.Id).ToHashSet();
            var deviceRuntimes = (await GlobalData.DeviceService.GetAllAsync().ConfigureAwait(false)).Where(a => chanelIds.Contains(a.ChannelId)).Adapt<List<DeviceRuntime>>();

            var variableRuntimes = (await GlobalData.VariableService.GetByDeviceIdAsync(deviceRuntimes.Select(a => a.Id).ToList()).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();
            foreach (var channelRuntime in channelRuntimes)
            {
                try
                {
                    channelRuntime.Init();
                    var devRuntimes = deviceRuntimes.Where(x => x.ChannelId == channelRuntime.Id);
                    foreach (var item in devRuntimes)
                    {
                        item.Init(channelRuntime);

                        var varRuntimes = variableRuntimes.Where(x => x.DeviceId == item.Id);

                        varRuntimes.ParallelForEach(varItem =>
                        {
                            varItem.Init(item);
                        });

                    }

                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Init Channel");
                }
            }

            var startCollectChannelEnable = GlobalData.StartCollectChannelEnable;
            var startBusinessChannelEnable = GlobalData.StartBusinessChannelEnable;

            var collectChannelRuntimes = channelRuntimes.Where(x => (x.Enable && x.IsCollect == true && startCollectChannelEnable));

            var businessChannelRuntimes = channelRuntimes.Where(x => (x.Enable && x.IsCollect == false && startBusinessChannelEnable));

            //根据初始冗余属性，筛选启动
            await GlobalData.ChannelThreadManage.RestartChannelAsync(businessChannelRuntimes).ConfigureAwait(false);
            await GlobalData.ChannelThreadManage.RestartChannelAsync(collectChannelRuntimes).ConfigureAwait(false);


        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Start error");
        }
        finally
        {
            WaitLock.Release();
        }
    }

}