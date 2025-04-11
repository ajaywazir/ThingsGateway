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


    public async Task<bool> CopyAsync(Dictionary<Device, List<Variable>> devices, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            await WaitLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            var result = await GlobalData.DeviceService.CopyAsync(devices).ConfigureAwait(false);

            var deviceids = devices.Select(a => a.Key.Id).ToHashSet();
            var newDeviceRuntimes = await RuntimeServiceHelper.GetNewDeviceRuntimesAsync(deviceids).ConfigureAwait(false);

            await RuntimeServiceHelper.InitAsync(newDeviceRuntimes, _logger).ConfigureAwait(false);


            //根据条件重启通道线程
            if (restart)
            {
                await RuntimeServiceHelper.RestartDeviceAsync(newDeviceRuntimes).ConfigureAwait(false);
                await RuntimeServiceHelper.ChangedDriverAsync(_logger, cancellationToken).ConfigureAwait(false);

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
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.BatchEditAsync(models, oldModel, model).ConfigureAwait(false);
            var deviceids = models.Select(a => a.Id).ToHashSet();

            var newDeviceRuntimes = await RuntimeServiceHelper.GetNewDeviceRuntimesAsync(deviceids).ConfigureAwait(false);

            if (restart)
            {
                var newDeciceIds = newDeviceRuntimes.Select(a => a.Id).ToHashSet();

                await RuntimeServiceHelper.RemoveDeviceAsync(newDeciceIds).ConfigureAwait(false);
            }

            RuntimeServiceHelper.Init(newDeviceRuntimes);

            //根据条件重启通道线程

            if (restart)
            {
                await RuntimeServiceHelper.RestartDeviceAsync(newDeviceRuntimes).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }

    public async Task<bool> DeleteDeviceAsync(IEnumerable<long> ids, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            await WaitLock.WaitAsync(cancellationToken).ConfigureAwait(false);


            ids = ids.ToHashSet();

            var result = await GlobalData.DeviceService.DeleteDeviceAsync(ids).ConfigureAwait(false);

            //根据条件重启通道线程
            var deviceRuntimes = GlobalData.IdDevices.Where(a => ids.Contains(a.Key)).Select(a => a.Value).ToList();

            ConcurrentHashSet<IDriver> changedDriver = RuntimeServiceHelper.DeleteDeviceRuntime(deviceRuntimes);

            if (restart)
            {
                await RuntimeServiceHelper.RemoveDeviceAsync(deviceRuntimes).ConfigureAwait(false);

                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);

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
    public Task<MemoryStream> ExportMemoryStream(List<Device> data, string channelName, string plugin) =>
          GlobalData.DeviceService.ExportMemoryStream(data, channelName, plugin);


    public async Task ImportDeviceAsync(Dictionary<string, ImportPreviewOutputBase> input, bool restart = true)
    {
        try
        {
            await WaitLock.WaitAsync().ConfigureAwait(false);

            var deviceids = await GlobalData.DeviceService.ImportDeviceAsync(input).ConfigureAwait(false);

            var newDeviceRuntimes = await RuntimeServiceHelper.GetNewDeviceRuntimesAsync(deviceids).ConfigureAwait(false);

            if (restart)
            {

                var newDeciceIds = newDeviceRuntimes.Select(a => a.Id).ToHashSet();
                await RuntimeServiceHelper.RemoveDeviceAsync(newDeciceIds).ConfigureAwait(false);

            }

            //批量修改之后，需要重新加载通道
            RuntimeServiceHelper.Init(newDeviceRuntimes);

            //根据条件重启通道线程
            if (restart)
            {

                await RuntimeServiceHelper.RestartDeviceAsync(newDeviceRuntimes).ConfigureAwait(false);

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

            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.SaveDeviceAsync(input, type).ConfigureAwait(false);

            var newDeviceRuntimes = await RuntimeServiceHelper.GetNewDeviceRuntimesAsync(new HashSet<long>() { input.Id }).ConfigureAwait(false);



            RuntimeServiceHelper.Init(newDeviceRuntimes);


            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.RestartDeviceAsync(newDeviceRuntimes).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }


    public async Task<bool> BatchSaveDeviceAsync(List<Device> input, ItemChangedType type, bool restart)
    {

        try
        {

            await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.DeviceService.BatchSaveDeviceAsync(input, type).ConfigureAwait(false);

            var newDeviceRuntimes = await RuntimeServiceHelper.GetNewDeviceRuntimesAsync(input.Select(a => a.Id).ToHashSet()).ConfigureAwait(false);

            RuntimeServiceHelper.Init(newDeviceRuntimes);

            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.RestartDeviceAsync(newDeviceRuntimes).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            WaitLock.Release();
        }
    }

}