﻿// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

using ThingsGateway.NewLife.Extension;
using ThingsGateway.Schedule;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 清理日志作业任务
/// </summary>
[JobDetail("gatewayjob_log", Description = "清理网关日志", GroupName = "Log", Concurrent = false)]
[Daily(TriggerId = "trigger_gatewaylog", Description = "清理网关日志", RunOnStart = true)]
public class LogJob : IJob
{
    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        var gatewayLogOptions = App.GetOptions<GatewayLogOptions>();
        var rpcLogDaysdaysAgo = gatewayLogOptions?.RpcLogDaysAgo ?? 30;
        var backendLogdaysAgo = gatewayLogOptions?.BackendLogDaysAgo ?? 30;
        await DeleteRpcLog(rpcLogDaysdaysAgo, stoppingToken).ConfigureAwait(false);
        await DeleteBackendLog(backendLogdaysAgo, stoppingToken).ConfigureAwait(false);
        await DeleteTextLog(stoppingToken).ConfigureAwait(false);
        await DeleteLocalDB(stoppingToken).ConfigureAwait(false);
    }


    private static async Task DeleteRpcLog(int daysAgo, CancellationToken stoppingToken)
    {
        using var db = DbContext.Db.GetConnectionScopeWithAttr<RpcLog>().CopyNew();
        var time = DateTime.Now.AddDays(-daysAgo);
        await db.DeleteableWithAttr<RpcLog>().Where(u => u.LogTime < time).ExecuteCommandAsync(stoppingToken).ConfigureAwait(false); // 删除操作日志
    }

    private static async Task DeleteBackendLog(int daysAgo, CancellationToken stoppingToken)
    {
        using var db = DbContext.Db.GetConnectionScopeWithAttr<BackendLog>().CopyNew();
        var time = DateTime.Now.AddDays(-daysAgo);
        await db.DeleteableWithAttr<BackendLog>().Where(u => u.LogTime < time).ExecuteCommandAsync(stoppingToken).ConfigureAwait(false); // 删除操作日志
    }



    private static async Task DeleteTextLog(CancellationToken stoppingToken)
    {
        //网关通道日志以通道id命名
        var channelService = App.RootServices.GetService<IChannelService>();
        var channelIds = (await channelService.GetAllAsync().ConfigureAwait(false)).Select(a => a.Id.ToString()).ToHashSet();

        var baseDir = LoggerExtensions.GetLogBasePath();
        Directory.CreateDirectory(baseDir);
        string[] dirs = Directory.GetDirectories(baseDir)
.Select(a => Path.GetFileName(a))
.ToArray();
        foreach (var dir in dirs)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            //删除文件夹
            try
            {
                if (!channelIds.Contains(dir))
                {
                    Directory.Delete(baseDir.CombinePathWithOs(dir), true);
                }
            }
            catch { }
        }

        //底层调试
        var debugDir = LoggerExtensions.GetDebugLogBasePath();
        Directory.CreateDirectory(debugDir);
        string[] debugDirs = Directory.GetDirectories(debugDir)
    .Select(a => Path.GetFileName(a))
    .ToArray();

        foreach (var item in debugDirs)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            //删除文件夹
            try
            {
                Directory.Delete(debugDir.CombinePathWithOs(item), true);
            }
            catch { }
        }

    }

    public async Task DeleteLocalDB(CancellationToken stoppingToken)
    {
        var deviceService = App.RootServices.GetService<IDeviceService>();
        var data = (await deviceService.GetAllAsync().ConfigureAwait(false)).Select(a => a.Id).ToHashSet();
        var dir = CacheDBUtil.GetFileBasePath();
        string[] dirs = Directory.GetDirectories(dir);
        foreach (var item in dirs)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            //删除文件夹
            try
            {
                var id = Path.GetFileName(item).ToLong();
                if (id > 0) //非ID文件夹不删除
                {
                    if (!data.Contains(id))
                    {
                        Directory.Delete(item, true);
                    }
                }
            }
            catch { }
        }

    }

}
