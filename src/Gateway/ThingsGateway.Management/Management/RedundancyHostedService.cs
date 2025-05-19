//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Mapster;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Gateway.Application;
using ThingsGateway.NewLife;

using TouchSocket.Core;
using TouchSocket.Dmtp;
using TouchSocket.Dmtp.Rpc;
using TouchSocket.Rpc;
using TouchSocket.Sockets;

namespace ThingsGateway.Management;

internal sealed class RedundancyHostedService : BackgroundService, IRedundancyHostedService, IRpcDriver
{
    private readonly ILogger _logger;
    private readonly IRedundancyService _redundancyService;
    private readonly GatewayRedundantSerivce _gatewayRedundantSerivce;
    /// <inheritdoc cref="RedundancyHostedService"/>
    public RedundancyHostedService(ILogger<RedundancyHostedService> logger, IStringLocalizer<RedundancyHostedService> localizer, IRedundancyService redundancyService, GatewayRedundantSerivce gatewayRedundantSerivce)
    {
        _logger = logger;
        Localizer = localizer;
        _gatewayRedundantSerivce = gatewayRedundantSerivce;
        // 创建新的文件日志记录器，并设置日志级别为 Trace
        LogPath = "Logs/RedundancyLog";
        TextLogger = TextFileLogger.GetMultipleFileLogger(LogPath);
        TextLogger.LogLevel = TouchSocket.Core.LogLevel.Trace;

        _redundancyService = redundancyService;
    }
    public override void Dispose()
    {
        TextLogger.Dispose();
        base.Dispose();
    }
    private IStringLocalizer Localizer { get; }
    private DoTask RedundancyTask { get; set; }
    private WaitLock RedundancyRestartLock { get; } = new();
    public ILog LogMessage { get; set; }
    public TextFileLogger TextLogger { get; }
    public string LogPath { get; }
    private TcpDmtpClient TcpDmtpClient;
    private TcpDmtpService TcpDmtpService;
    private async Task<TcpDmtpClient> GetTcpDmtpClient(RedundancyOptions redundancy)
    {
        var log = new LoggerGroup() { LogLevel = TouchSocket.Core.LogLevel.Trace };
        log?.AddLogger(new EasyLogger(Log_Out) { LogLevel = TouchSocket.Core.LogLevel.Trace });
        log?.AddLogger(TextLogger);
        LogMessage = log;
        var tcpDmtpClient = new TcpDmtpClient();
        var config = new TouchSocketConfig()
               .SetRemoteIPHost(redundancy.MasterUri)
               .SetAdapterOption(new AdapterOption() { MaxPackageSize = 1024 * 1024 * 1024 })
               .SetDmtpOption(new DmtpOption() { VerifyToken = redundancy.VerifyToken })
               .ConfigureContainer(a =>
               {
                   a.AddLogger(LogMessage);
                   a.AddRpcStore(store =>
                   {
                       store.RegisterServer(new ReverseCallbackServer(this));
                   });
               })
               .ConfigurePlugins(a =>
               {
                   a.UseDmtpRpc();
                   a.UseDmtpHeartbeat()//使用Dmtp心跳
                   .SetTick(TimeSpan.FromMilliseconds(redundancy.HeartbeatInterval))
                   .SetMaxFailCount(redundancy.MaxErrorCount);
               });

        await tcpDmtpClient.SetupAsync(config).ConfigureAwait(false);
        return tcpDmtpClient;
    }

    private async Task<TcpDmtpService> GetTcpDmtpService(RedundancyOptions redundancy)
    {
        var log = new LoggerGroup() { LogLevel = TouchSocket.Core.LogLevel.Trace };
        log?.AddLogger(new EasyLogger(Log_Out) { LogLevel = TouchSocket.Core.LogLevel.Trace });
        log?.AddLogger(TextLogger);
        LogMessage = log;
        var tcpDmtpService = new TcpDmtpService();
        var config = new TouchSocketConfig()
               .SetListenIPHosts(redundancy.MasterUri)
               .SetAdapterOption(new AdapterOption() { MaxPackageSize = 1024 * 1024 * 1024 })
               .SetDmtpOption(new DmtpOption() { VerifyToken = redundancy.VerifyToken })
               .ConfigureContainer(a =>
               {
                   a.AddLogger(LogMessage);
                   a.AddRpcStore(store =>
                   {
                       store.RegisterServer(new ReverseCallbackServer(this));
                   });
               })
               .ConfigurePlugins(a =>
               {
                   a.UseDmtpRpc();
                   a.UseDmtpHeartbeat()//使用Dmtp心跳
                   .SetTick(TimeSpan.FromMilliseconds(redundancy.HeartbeatInterval))
                   .SetMaxFailCount(redundancy.MaxErrorCount);
               });

        await tcpDmtpService.SetupAsync(config).ConfigureAwait(false);
        return tcpDmtpService;
    }

    private void Log_Out(TouchSocket.Core.LogLevel logLevel, object source, string message, Exception exception)
    {
        _logger?.Log_Out(logLevel, source, message, exception);
    }

    private static Task RestartAsync()
    {
        return GlobalData.ChannelRuntimeService.RestartChannelAsync(GlobalData.ReadOnlyChannels.Values);
    }

    /// <summary>
    /// 主站
    /// </summary>
    /// <param name="tcpDmtpService">服务</param>
    /// <param name="syncInterval">同步间隔</param>
    /// <param name="stoppingToken">取消任务的 CancellationToken</param>
    private async ValueTask DoMasterWork(TcpDmtpService tcpDmtpService, int syncInterval, CancellationToken stoppingToken)
    {
        // 延迟一段时间，避免过于频繁地执行任务
        await Task.Delay(500, stoppingToken).ConfigureAwait(false);
        try
        {
            bool online = false;
            var waitInvoke = new DmtpInvokeOption()
            {
                FeedbackType = FeedbackType.WaitInvoke,
                Token = stoppingToken,
                Timeout = 30000,
                SerializationType = SerializationType.Json,
            };

            try
            {
                if (tcpDmtpService.Clients.Count != 0)
                {
                    online = true;
                }
                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    var deviceRunTimes = GlobalData.ReadOnlyIdDevices.Where(a => a.Value.IsCollect == true).Select(a => a.Value).Adapt<List<DeviceDataWithValue>>();

                    foreach (var item in tcpDmtpService.Clients)
                    {
                        // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                        await item.GetDmtpRpcActor().InvokeAsync(
                                         nameof(ReverseCallbackServer.UpData), null, waitInvoke, deviceRunTimes).ConfigureAwait(false);
                        LogMessage?.LogTrace($"{item.GetIPPort()} Update StandbyStation data success");
                    }

                }
            }
            catch (Exception ex)
            {
                // 输出警告日志，指示同步数据到从站时发生错误
                LogMessage?.LogWarning(ex, Localizer["ErrorSynchronizingData"]);
            }
            await Task.Delay(syncInterval, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogMessage?.LogWarning(ex, "Execute");
        }
    }

    /// <summary>
    /// 从站
    /// </summary>
    /// <param name="tcpDmtpClient">服务</param>
    /// <param name="redundancy">冗余配置</param>
    /// <param name="stoppingToken">取消任务的 CancellationToken</param>
    private async ValueTask DoSlaveWork(TcpDmtpClient tcpDmtpClient, RedundancyOptions redundancy, CancellationToken stoppingToken)
    {
        // 延迟一段时间，避免过于频繁地执行任务
        await Task.Delay(5000, stoppingToken).ConfigureAwait(false);
        try
        {
            bool online = false;
            var waitInvoke = new DmtpInvokeOption()
            {
                FeedbackType = FeedbackType.WaitInvoke,
                Token = stoppingToken,
                Timeout = 30000,
                SerializationType = SerializationType.Json,
            };

            try
            {
                await tcpDmtpClient.TryConnectAsync().ConfigureAwait(false);

                {
                    // 初始化读取错误计数器
                    var readErrorCount = 0;
                    // 当读取错误次数小于最大错误计数时循环执行
                    while (readErrorCount < redundancy.MaxErrorCount)
                    {
                        try
                        {
                            // 发送 Ping 请求以检查设备是否在线，超时时间为 10000 毫秒
                            online = await tcpDmtpClient.PingAsync(10000).ConfigureAwait(false);
                            if (online)
                                break;
                            else
                            {
                                readErrorCount++;
                                await Task.Delay(redundancy.SyncInterval, stoppingToken).ConfigureAwait(false);
                            }
                        }
                        catch
                        {
                            // 捕获异常，增加读取错误计数器
                            readErrorCount++;
                            await Task.Delay(redundancy.SyncInterval, stoppingToken).ConfigureAwait(false);
                        }
                    }
                }


                // 如果设备不在线
                if (!online)
                {
                    // 无法获取状态，启动本机
                    await ActiveAsync().ConfigureAwait(false);
                }
                else
                {
                    // 如果设备在线
                    LogMessage?.LogTrace($"Ping ActiveStation {redundancy.MasterUri} success");
                    await StandbyAsync().ConfigureAwait(false);
                }
            }
            finally
            {
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogMessage?.LogWarning(ex, "Execute");
        }
    }

    public async ValueTask ForcedSync(CancellationToken cancellationToken = default)
    {
        try
        {
            bool online = false;
            var waitInvoke = new DmtpInvokeOption()
            {
                FeedbackType = FeedbackType.WaitInvoke,
                Token = cancellationToken,
                Timeout = 30000,
                SerializationType = SerializationType.Json,
            };

            try
            {
                online = (await TcpDmtpClient.TryConnectAsync().ConfigureAwait(false)).ResultCode == ResultCode.Success;

                // 如果 online 为 true，表示设备在线
                if (online)
                {


                    // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                    var data = await TcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<List<DataWithDatabase>>(
                                       nameof(ReverseCallbackServer.GetData), waitInvoke).ConfigureAwait(false);

                    await GlobalData.ChannelRuntimeService.CopyAsync(data.Select(a => a.Channel).ToList(), data.SelectMany(a => a.DeviceVariables).ToDictionary(a => a.Device, a => a.Variables), true, cancellationToken).ConfigureAwait(false);

                    LogMessage?.LogTrace($"ForcedSync data success");

                }
            }
            catch (Exception ex)
            {
                // 输出警告日志，指示同步数据到从站时发生错误
                LogMessage?.LogWarning(ex, "ForcedSync data error");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogMessage?.LogWarning(ex, "Execute");
        }
    }


    private WaitLock _switchLock = new();

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return StartRedundancyTaskAsync();
    }

    public async Task<OperResult> StartRedundancyTaskAsync()
    {
        try
        {
            await RedundancyRestartLock.WaitAsync().ConfigureAwait(false); // 等待获取锁，以确保只有一个线程可以执行以下代码

            if (RedundancyTask != null)
            {
                await RedundancyTask.StopAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false); // 停止现有任务，等待最多30秒钟
            }
            await BeforeStartAsync().ConfigureAwait(false);
            if (RedundancyOptions?.Enable == true)
            {
                if (RedundancyOptions.IsMaster)
                {
                    RedundancyTask = new DoTask(a => DoMasterWork(TcpDmtpService, RedundancyOptions.SyncInterval, a), LogMessage); // 创建新的任务
                }
                else
                {

                    RedundancyTask = new DoTask(a => DoSlaveWork(TcpDmtpClient, RedundancyOptions, a), LogMessage); // 创建新的任务
                }

                RedundancyTask?.Start(default); // 启动任务
            }

            return new();
        }
        catch (Exception ex)
        {
            LogMessage?.LogError(ex, "Start"); // 记录错误日志
            return new(ex);
        }
        finally
        {
            RedundancyRestartLock.Release(); // 释放锁
        }
    }

    private RedundancyOptions RedundancyOptions;

    private async Task BeforeStartAsync()
    {
        RedundancyOptions = (await _redundancyService.GetRedundancyAsync().ConfigureAwait(false)).Adapt<RedundancyOptions>();

        if (RedundancyOptions?.Enable == true)
        {
            if (RedundancyOptions.IsMaster)
            {
                TcpDmtpService = await GetTcpDmtpService(RedundancyOptions).ConfigureAwait(false);

                await TcpDmtpService.StartAsync().ConfigureAwait(false);//启动
                await ActiveAsync().ConfigureAwait(false);
            }
            else
            {
                TcpDmtpClient = await GetTcpDmtpClient(RedundancyOptions).ConfigureAwait(false);
                await StandbyAsync().ConfigureAwait(false);
            }
        }
        else
        {
            await ActiveAsync().ConfigureAwait(false);
        }
    }
    private bool first;
    private async Task StandbyAsync()
    {
        try
        {
            await _switchLock.WaitAsync().ConfigureAwait(false);
            if (_gatewayRedundantSerivce.StartCollectChannelEnable)
            {
                // 输出日志，指示主站已恢复，从站将切换到备用状态
                if (first)
                    LogMessage?.Warning(Localizer["SwitchSlaveState"]);

                // 将 IsStart 设置为 false，表示当前设备为从站，切换到备用状态
                _gatewayRedundantSerivce.StartCollectChannelEnable = false;
                _gatewayRedundantSerivce.StartBusinessChannelEnable = RedundancyOptions?.IsStartBusinessDevice ?? false;
                await RestartAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _switchLock.Release();
            first = true;
        }
    }

    private async Task ActiveAsync()
    {
        try
        {
            await _switchLock.WaitAsync().ConfigureAwait(false);

            _gatewayRedundantSerivce.StartBusinessChannelEnable = true;
            if (!_gatewayRedundantSerivce.StartCollectChannelEnable)
            {
                // 输出日志，指示无法连接冗余站点，本机将切换到正常状态
                if (first)
                    LogMessage?.Warning(Localizer["SwitchMasterState"]);
                _gatewayRedundantSerivce.StartCollectChannelEnable = true;
                await RestartAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _switchLock.Release();
            first = true;
        }
    }

    public async Task StopRedundancyTaskAsync()
    {
        try
        {
            await RedundancyRestartLock.WaitAsync().ConfigureAwait(false); // 等待获取锁，以确保只有一个线程可以执行以下代码

            if (RedundancyTask != null)
            {
                await RedundancyTask.StopAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); // 停止任务，等待最多10秒钟
            }
            if (TcpDmtpService != null)
            {
                try
                {
                    await TcpDmtpService.StopAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }
            if (TcpDmtpClient != null)
            {
                try
                {
                    await TcpDmtpClient.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }
            TcpDmtpService?.Dispose();
            TcpDmtpClient?.Dispose();
            RedundancyTask = null;
            TcpDmtpService = null;
            TcpDmtpClient = null;
        }
        catch (Exception ex)
        {
            LogMessage?.LogError(ex, "Stop"); // 记录错误日志
        }
        finally
        {
            first = false;
            RedundancyRestartLock.Release(); // 释放锁
        }
    }



    /// <summary>
    /// 异步写入方法
    /// </summary>
    /// <param name="writeInfoLists">要写入的变量及其对应的数据</param>
    /// <param name="cancellationToken">取消操作的通知</param>
    /// <returns>写入操作的结果字典</returns>
    public async ValueTask<Dictionary<string, Dictionary<string, IOperResult>>> InvokeMethodAsync(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {
        return (await Rpc(writeInfoLists, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.Key, a => a.Value.ToDictionary(b => b.Key, b => (IOperResult)b.Value));
    }

    private async ValueTask<Dictionary<string, Dictionary<string, OperResult<object>>>> Rpc(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {
        Dictionary<string, Dictionary<string, OperResult<object>>> dataResult = new();

        Dictionary<string, Dictionary<string, string>> deviceDatas = new();
        foreach (var item in writeInfoLists)
        {
            if (deviceDatas.TryGetValue(item.Key.DeviceName ?? string.Empty, out var variableDatas))
            {
                variableDatas.Add(item.Key.Name, item.Value?.ToString() ?? string.Empty);
            }
            else
            {
                deviceDatas.Add(item.Key.DeviceName ?? string.Empty, new());
                deviceDatas[item.Key.DeviceName ?? string.Empty].Add(item.Key.Name, item.Value?.ToString() ?? string.Empty);
            }
        }

        if (RedundancyOptions.IsMaster)
        {
            return NoOnline(dataResult, deviceDatas);
        }
        bool online = false;
        var waitInvoke = new DmtpInvokeOption()
        {
            FeedbackType = FeedbackType.WaitInvoke,
            Token = cancellationToken,
            Timeout = 30000,
            SerializationType = SerializationType.Json,
        };

        try
        {
            if (!RedundancyOptions.IsMaster)
            {
                online = (await TcpDmtpClient.TryConnectAsync().ConfigureAwait(false)).ResultCode == ResultCode.Success;

                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                    dataResult = await TcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<Dictionary<string, Dictionary<string, OperResult<object>>>>(
                                       nameof(ReverseCallbackServer.Rpc), waitInvoke, deviceDatas).ConfigureAwait(false);

                    LogMessage?.LogTrace($"Rpc success");

                    return dataResult;
                }
            }
            else
            {
                if (TcpDmtpService.Clients.Count != 0)
                {
                    online = true;
                }
                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    foreach (var item in deviceDatas)
                    {

                        if (GlobalData.ReadOnlyDevices.TryGetValue(item.Key, out var device))
                        {
                            var key = device.Tag;

                            if (TcpDmtpService.TryGetClient(key, out var client))
                            {
                                try
                                {

                                    var data = await TcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<Dictionary<string, Dictionary<string, OperResult<object>>>>(
                                                         nameof(ReverseCallbackServer.Rpc), waitInvoke, new Dictionary<string, Dictionary<string, string>>() { { item.Key, item.Value } }).ConfigureAwait(false);

                                    dataResult.AddRange(data);

                                    continue;
                                }
                                catch (Exception ex)
                                {
                                    dataResult.TryAdd(item.Key, new Dictionary<string, OperResult<object>>());

                                    foreach (var vItem in item.Value)
                                    {
                                        dataResult[item.Key].Add(vItem.Key, new OperResult<object>(ex));
                                    }
                                }
                            }

                        }

                        dataResult.TryAdd(item.Key, new Dictionary<string, OperResult<object>>());

                        foreach (var vItem in item.Value)
                        {
                            dataResult[item.Key].Add(vItem.Key, new OperResult<object>("No online"));
                        }
                    }

                    LogMessage?.LogTrace($"Rpc success");
                    return dataResult;
                }
                else
                {
                    LogMessage?.LogWarning("Rpc error, no client online");
                }
            }

            return NoOnline(dataResult, deviceDatas);

        }
        catch (OperationCanceledException)
        {

            return NoOnline(dataResult, deviceDatas);
        }
        catch (Exception ex)
        {
            // 输出警告日志，指示同步数据到从站时发生错误
            LogMessage?.LogWarning(ex, "Rpc error");
            return NoOnline(dataResult, deviceDatas);
        }
    }

    private static Dictionary<string, Dictionary<string, OperResult<object>>> NoOnline(Dictionary<string, Dictionary<string, OperResult<object>>> dataResult, Dictionary<string, Dictionary<string, string>> deviceDatas)
    {
        foreach (var item in deviceDatas)
        {
            dataResult.TryAdd(item.Key, new Dictionary<string, OperResult<object>>());

            foreach (var vItem in item.Value)
            {
                dataResult[item.Key].TryAdd(vItem.Key, new OperResult<object>("No online"));
            }
        }
        return dataResult;

    }
    /// <summary>
    /// 异步写入方法
    /// </summary>
    /// <param name="writeInfoLists">要写入的变量及其对应的数据</param>
    /// <param name="cancellationToken">取消操作的通知</param>
    /// <returns>写入操作的结果字典</returns>
    public async ValueTask<Dictionary<string, Dictionary<string, IOperResult>>> InVokeWriteAsync(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {
        return (await Rpc(writeInfoLists, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.Key, a => a.Value.ToDictionary(b => b.Key, b => (IOperResult)b.Value));
    }
}
