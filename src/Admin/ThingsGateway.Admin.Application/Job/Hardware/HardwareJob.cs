﻿// ------------------------------------------------------------------------------
// 此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
// 此代码版权（除特别声明外的代码）归作者本人Diego所有
// 源代码使用协议遵循本仓库的开源协议及附加协议
// Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
// Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
// 使用文档：https://thingsgateway.cn/
// QQ群：605534569
// ------------------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Runtime.InteropServices;

using ThingsGateway.Extension;
using ThingsGateway.NewLife;
using ThingsGateway.NewLife.Caching;
using ThingsGateway.NewLife.Threading;
using ThingsGateway.Schedule;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 获取硬件信息作业任务
/// </summary>
[JobDetail("hardware_log", Description = "获取硬件信息", GroupName = "Hardware", Concurrent = false)]
[PeriodSeconds(30, TriggerId = "trigger_hardware", Description = "获取硬件信息", RunOnStart = true)]
public class HardwareJob : IJob, IHardwareJob
{
    private readonly ILogger _logger;
    private readonly IStringLocalizer _localizer;

    /// <inheritdoc/>
    public HardwareJob(ILogger<HardwareJob> logger, IStringLocalizer<HardwareJob> localizer, IOptions<HardwareInfoOptions> options)
    {
        _logger = logger;
        _localizer = localizer;
        HardwareInfoOptions = options.Value;
    }
    #region 属性

    /// <summary>
    /// 运行信息获取
    /// </summary>
    public HardwareInfo HardwareInfo { get; } = new();

    /// <inheritdoc/>
    public HardwareInfoOptions HardwareInfoOptions { get; private set; }

    #endregion 属性

    private MemoryCache MemoryCache = new() { };
    private const string CacheKey = "HistoryHardwareInfo";
    /// <inheritdoc/>
    public async Task<List<HistoryHardwareInfo>> GetHistoryHardwareInfos()
    {
        var historyHardwareInfos = MemoryCache.Get<List<HistoryHardwareInfo>>(CacheKey);
        if (historyHardwareInfos == null)
        {
            using var db = DbContext.Db.GetConnectionScopeWithAttr<HistoryHardwareInfo>().CopyNew();
            historyHardwareInfos = await db.Queryable<HistoryHardwareInfo>().Where(a => a.Date > DateTime.Now.AddDays(-3)).ToListAsync().ConfigureAwait(false);

            MemoryCache.Set(CacheKey, historyHardwareInfos);
        }
        return historyHardwareInfos;
    }

    private bool error = false;
    private DateTime hisInsertTime = default;

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        if (HardwareInfoOptions.Enable)
        {
            try
            {
                if (HardwareInfo.MachineInfo == null)
                {
                    await MachineInfo.RegisterAsync().ConfigureAwait(false);
                    HardwareInfo.MachineInfo = MachineInfo.Current;

                    string currentPath = Directory.GetCurrentDirectory();
                    DriveInfo drive = new(Path.GetPathRoot(currentPath));

                    HardwareInfoOptions.DaysAgo = Math.Min(Math.Max(HardwareInfoOptions.DaysAgo, 1), 7);
                    if (HardwareInfoOptions.HistoryInterval < 60000) HardwareInfoOptions.HistoryInterval = 60000;
                    HardwareInfo.DriveInfo = drive;
                    HardwareInfo.OsArchitecture = Environment.OSVersion.Platform.ToString() + " " + RuntimeInformation.OSArchitecture.ToString(); // 系统架构
                    HardwareInfo.FrameworkDescription = RuntimeInformation.FrameworkDescription; // NET框架
                    HardwareInfo.Environment = App.HostEnvironment.IsDevelopment() ? "Development" : "Production";
                    HardwareInfo.UUID = HardwareInfo.MachineInfo.UUID;

                    HardwareInfo.UpdateTime = TimerX.Now.ToDefaultDateTimeFormat();

                }
            }
            catch
            {
            }
            try
            {
                HardwareInfo.MachineInfo.Refresh();
                HardwareInfo.UpdateTime = TimerX.Now.ToDefaultDateTimeFormat();
                HardwareInfo.WorkingSet = (Environment.WorkingSet / 1024.0 / 1024.0).ToInt();
                error = false;
            }
            catch (Exception ex)
            {
                if (!error)
                    _logger.LogWarning(ex, _localizer["GetHardwareInfoFail"]);
                error = true;
            }

            try
            {
                if (HardwareInfoOptions.Enable)
                {
                    if (DateTime.Now > hisInsertTime.Add(TimeSpan.FromMilliseconds(HardwareInfoOptions.HistoryInterval)))
                    {
                        hisInsertTime = DateTime.Now;
                        using var db = DbContext.Db.GetConnectionScopeWithAttr<HistoryHardwareInfo>().CopyNew();
                        {
                            var his = new HistoryHardwareInfo()
                            {
                                Date = TimerX.Now,
                                DriveUsage = (100 - (HardwareInfo.DriveInfo.TotalFreeSpace * 100.00 / HardwareInfo.DriveInfo.TotalSize)).ToInt(),
                                Battery = (HardwareInfo.MachineInfo.Battery * 100).ToInt(),
                                MemoryUsage = (HardwareInfo.WorkingSet),
                                CpuUsage = (HardwareInfo.MachineInfo.CpuRate * 100).ToInt(),
                                Temperature = (HardwareInfo.MachineInfo.Temperature).ToInt(),
                            };
                            await db.Insertable(his).ExecuteCommandAsync(stoppingToken).ConfigureAwait(false);
                            MemoryCache.Remove(CacheKey);
                        }
                        var sevenDaysAgo = TimerX.Now.AddDays(-HardwareInfoOptions.DaysAgo);
                        //删除特定信息
                        var result = await db.Deleteable<HistoryHardwareInfo>(a => a.Date <= sevenDaysAgo).ExecuteCommandAsync(stoppingToken).ConfigureAwait(false);
                        if (result > 0)
                        {
                            MemoryCache.Remove(CacheKey);
                        }
                    }
                }
                error = false;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!error)
                    _logger.LogWarning(ex, _localizer["GetHardwareInfoFail"]);
                error = true;
            }
        }
    }

}
