// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using ThingsGateway.Schedule;

namespace ThingsGateway.RulesEngine;

/// <summary>
/// 清理日志作业任务
/// </summary>
[JobDetail("rulesenginejob_log", Description = "清理规则引擎日志", GroupName = "Log", Concurrent = false)]
[Daily(TriggerId = "trigger_rulesenginelog", Description = "清理规则引擎日志", RunOnStart = false)]
public class LogJob : IJob
{
    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        await DeleteTextLog(stoppingToken).ConfigureAwait(false);
    }



    private static async Task DeleteTextLog(CancellationToken stoppingToken)
    {
        //网关通道日志以通道id命名
        var rulesService = App.RootServices.GetService<IRulesService>();
        var ruleNames = (await rulesService.GetAllAsync().ConfigureAwait(false)).Select(a => a.Name.ToString()).ToHashSet();
        var ruleBaseDir = RulesEngineHostedService.LogDir;
        Directory.CreateDirectory(ruleBaseDir);

        Delete(ruleBaseDir, ruleNames, stoppingToken);

    }

    private static void Delete(string baseDir, HashSet<string> strings, CancellationToken stoppingToken)
    {
        var channelDir = Directory.GetDirectories(baseDir)
  .Select(a => Path.GetFileName(a))
  .ToArray();
        foreach (var dir in channelDir)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            //删除文件夹
            try
            {
                if (!strings.Contains(dir))
                {
                    Directory.Delete(baseDir.CombinePathWithOs(dir), true);
                }
            }
            catch { }
        }
    }


}
