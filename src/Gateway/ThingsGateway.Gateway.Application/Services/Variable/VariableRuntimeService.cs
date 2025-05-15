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

using ThingsGateway.NewLife.Collections;

namespace ThingsGateway.Gateway.Application;

public class VariableRuntimeService : IVariableRuntimeService
{
    //private WaitLock WaitLock { get; set; } = new WaitLock();
    private ILogger _logger;
    public VariableRuntimeService(ILogger<VariableRuntimeService> logger)
    {
        _logger = logger;
    }



    public async Task<bool> BatchSaveVariableAsync(List<Variable> input, ItemChangedType type, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.VariableService.BatchSaveVariableAsync(input.Where(a => !a.DynamicVariable).ToList(), type).ConfigureAwait(false);

            var newVariableRuntimes = input.Adapt<List<VariableRuntime>>();
            var variableIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();
            //获取变量，先找到原插件线程，然后修改插件线程内的字典，再改动全局字典，最后刷新插件

            ConcurrentHashSet<IDriver> changedDriver = new();
            RuntimeServiceHelper.VariableRuntimesDispose(variableIds);
            RuntimeServiceHelper.AddCollectChangedDriver(newVariableRuntimes, changedDriver);
            RuntimeServiceHelper.AddBusinessChangedDriver(variableIds, changedDriver);


            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        finally
        {
            //WaitLock.Release();
        }
    }

    public async Task<bool> BatchEditAsync(IEnumerable<Variable> models, Variable oldModel, Variable model, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);


            var result = await GlobalData.VariableService.BatchEditAsync(models, oldModel, model).ConfigureAwait(false);

            using var db = DbContext.GetDB<Variable>();
            var ids = models.Select(a => a.Id).ToHashSet();

            var newVariableRuntimes = (await db.Queryable<Variable>().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

            var variableIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();

            ConcurrentHashSet<IDriver> changedDriver = new();

            RuntimeServiceHelper.VariableRuntimesDispose(variableIds);
            RuntimeServiceHelper.AddCollectChangedDriver(newVariableRuntimes, changedDriver);
            RuntimeServiceHelper.AddBusinessChangedDriver(variableIds, changedDriver);

            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);
            }

            return true;

        }
        finally
        {
            //WaitLock.Release();
        }
    }

    public async Task<bool> DeleteVariableAsync(IEnumerable<long> ids, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            var variableIds = ids.ToHashSet();

            var result = await GlobalData.VariableService.DeleteVariableAsync(variableIds).ConfigureAwait(false);

            ConcurrentHashSet<IDriver> changedDriver = new();

            RuntimeServiceHelper.AddBusinessChangedDriver(variableIds, changedDriver);
            RuntimeServiceHelper.VariableRuntimesDispose(variableIds);

            if (restart)
            {
                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        finally
        {
            //WaitLock.Release();
        }


    }
    public Task<Dictionary<string, object>> ExportVariableAsync(ExportFilter exportFilter) => GlobalData.VariableService.ExportVariableAsync(exportFilter);

    public async Task ImportVariableAsync(Dictionary<string, ImportPreviewOutputBase> input, bool restart, CancellationToken cancellationToken)
    {

        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);

            var result = await GlobalData.VariableService.ImportVariableAsync(input).ConfigureAwait(false);


            using var db = DbContext.GetDB<Variable>();
            var newVariableRuntimes = (await db.Queryable<Variable>().Where(a => result.Contains(a.Id)).ToListAsync(cancellationToken).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

            var variableIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();

            ConcurrentHashSet<IDriver> changedDriver = new();
            RuntimeServiceHelper.VariableRuntimesDispose(variableIds);
            RuntimeServiceHelper.AddCollectChangedDriver(newVariableRuntimes, changedDriver);
            RuntimeServiceHelper.AddBusinessChangedDriver(variableIds, changedDriver);

            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);
            }



        }
        finally
        {
            //WaitLock.Release();
        }

    }

    public async Task InsertTestDataAsync(int testVariableCount, int testDeviceCount, string slaveUrl, bool businessEnable, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);



            var datas = await GlobalData.VariableService.InsertTestDataAsync(testVariableCount, testDeviceCount, slaveUrl, businessEnable).ConfigureAwait(false);

            {
                var newChannelRuntimes = (datas.Item1).Adapt<List<ChannelRuntime>>();

                //批量修改之后，需要重新加载通道
                RuntimeServiceHelper.Init(newChannelRuntimes);

                {

                    var newDeviceRuntimes = (datas.Item2).Adapt<List<DeviceRuntime>>();

                    RuntimeServiceHelper.Init(newDeviceRuntimes);

                }
                {
                    var newVariableRuntimes = (datas.Item3).Adapt<List<VariableRuntime>>();
                    RuntimeServiceHelper.Init(newVariableRuntimes);

                }
                //根据条件重启通道线程

                if (restart)
                {
                    await GlobalData.ChannelThreadManage.RestartChannelAsync(newChannelRuntimes).ConfigureAwait(false);

                    await RuntimeServiceHelper.ChangedDriverAsync(_logger, cancellationToken).ConfigureAwait(false);
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

    public async Task<bool> SaveVariableAsync(Variable input, ItemChangedType type, bool restart, CancellationToken cancellationToken)
    {
        try
        {
            // await WaitLock.WaitAsync().ConfigureAwait(false);



            var result = await GlobalData.VariableService.SaveVariableAsync(input, type).ConfigureAwait(false);


            using var db = DbContext.GetDB<Variable>();
            var newVariableRuntimes = (await db.Queryable<Variable>().Where(a => a.Id == input.Id).ToListAsync(cancellationToken).ConfigureAwait(false)).Adapt<List<VariableRuntime>>();

            var variableIds = newVariableRuntimes.Select(a => a.Id).ToHashSet();

            ConcurrentHashSet<IDriver> changedDriver = new();

            RuntimeServiceHelper.VariableRuntimesDispose(variableIds);
            RuntimeServiceHelper.AddCollectChangedDriver(newVariableRuntimes, changedDriver);
            RuntimeServiceHelper.AddBusinessChangedDriver(variableIds, changedDriver);

            if (restart)
            {
                //根据条件重启通道线程
                await RuntimeServiceHelper.ChangedDriverAsync(changedDriver, _logger, cancellationToken).ConfigureAwait(false);
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



}