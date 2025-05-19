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

using Newtonsoft.Json.Linq;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Foundation;

using TouchSocket.Core;
using TouchSocket.Dmtp;
using TouchSocket.Dmtp.Rpc;
using TouchSocket.Rpc;
using TouchSocket.Sockets;

namespace ThingsGateway.Plugin.Synchronization;


public partial class Synchronization : BusinessBase, IRpcDriver
{
    public override VariablePropertyBase VariablePropertys => new SynchronizationVariableProperty();
    internal SynchronizationProperty _driverPropertys = new();
    protected override BusinessPropertyBase _businessPropertyBase => _driverPropertys;

    public override Type DriverUIType => typeof(SynchronizationRuntimeRazor);
    public override async Task AfterVariablesChangedAsync(CancellationToken cancellationToken)
    {
        // 如果业务属性指定了全部变量，则设置当前设备的变量运行时列表和采集设备列表
        if (_driverPropertys.IsAllVariable)
        {
            LogMessage?.LogInformation("Refresh variable");
            IdVariableRuntimes.Clear();
            IdVariableRuntimes.AddRange(GlobalData.GetEnableVariables().ToDictionary(a => a.Id));
            CollectDevices = GlobalData.GetEnableDevices().Where(a => a.IsCollect == true).ToDictionary(a => a.Id);

            VariableRuntimeGroups = IdVariableRuntimes.GroupBy(a => a.Value.BusinessGroup ?? string.Empty).ToDictionary(a => a.Key, a => a.Select(a => a.Value).ToList());

        }
        else
        {
            await base.AfterVariablesChangedAsync(cancellationToken).ConfigureAwait(false);
        }
    }


    public override bool IsConnected()
    {
        return _driverPropertys.IsServer ? _tcpDmtpService?.ServerState == ServerState.Running : _tcpDmtpClient?.Online == true;
    }



    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {

        try
        {


            if (_driverPropertys.IsServer)
            {
                if (_tcpDmtpService.ServerState != ServerState.Running)
                {
                    if (_tcpDmtpService.ServerState != ServerState.Stopped)
                    {
                        await _tcpDmtpService.StopAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await _tcpDmtpService.StartAsync().ConfigureAwait(false);
                }

                if (_driverPropertys.IsMaster)
                {
                    bool online = false;
                    var waitInvoke = new DmtpInvokeOption()
                    {
                        FeedbackType = FeedbackType.WaitInvoke,
                        Token = cancellationToken,
                        Timeout = 30000,
                        SerializationType = SerializationType.Json,
                    };
                    if (_tcpDmtpService.Clients.Count != 0)
                    {
                        online = true;
                    }
                    // 如果 online 为 true，表示设备在线
                    if (online)
                    {
                        var deviceRunTimes = CollectDevices.Where(a => a.Value.IsCollect == true).Select(a => a.Value).Adapt<List<DeviceDataWithValue>>();

                        foreach (var item in _tcpDmtpService.Clients)
                        {
                            if (item.Online)
                            {
                                // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                                await item.GetDmtpRpcActor().InvokeAsync(
                                                 nameof(ReverseCallbackServer.UpData), null, waitInvoke, deviceRunTimes).ConfigureAwait(false);
                                LogMessage?.LogTrace($"{item.GetIPPort()} Update data success");
                            }
                        }

                    }

                }

            }
            else
            {
                bool online = true;
                var waitInvoke = new DmtpInvokeOption()
                {
                    FeedbackType = FeedbackType.WaitInvoke,
                    Token = cancellationToken,
                    Timeout = 30000,
                    SerializationType = SerializationType.Json,
                };
                if (!_tcpDmtpClient.Online)
                    online = (await _tcpDmtpClient.TryConnectAsync().ConfigureAwait(false)).ResultCode == ResultCode.Success;
                if (_driverPropertys.IsMaster)
                {


                    // 如果 online 为 true，表示设备在线
                    {
                        var deviceRunTimes = CollectDevices.Where(a => a.Value.IsCollect == true).Select(a => a.Value).Adapt<List<DeviceDataWithValue>>();

                        // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                        await _tcpDmtpClient.GetDmtpRpcActor().InvokeAsync(
                                         nameof(ReverseCallbackServer.UpData), null, waitInvoke, deviceRunTimes).ConfigureAwait(false);
                        LogMessage?.LogTrace($"Update data success");

                    }

                }


            }

        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogMessage.LogWarning(ex);
        }
    }
    private TcpDmtpClient? _tcpDmtpClient;
    private TcpDmtpService? _tcpDmtpService;
    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        if (_driverPropertys.IsServer)
        {
            _tcpDmtpService = await GetTcpDmtpService().ConfigureAwait(false);
        }
        else
        {
            _tcpDmtpClient = await GetTcpDmtpClient().ConfigureAwait(false);
        }

        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        _tcpDmtpClient?.SafeDispose();
        _tcpDmtpService?.SafeDispose();
        base.Dispose(disposing);
    }

    private async Task<TcpDmtpClient> GetTcpDmtpClient()
    {
        var tcpDmtpClient = new TcpDmtpClient();
        var config = new TouchSocketConfig()
               .SetRemoteIPHost(_driverPropertys.ServerUri)
               .SetAdapterOption(new AdapterOption() { MaxPackageSize = 1024 * 1024 * 1024 })
               .SetDmtpOption(new DmtpOption() { VerifyToken = _driverPropertys.VerifyToken })
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
                   .SetTick(TimeSpan.FromMilliseconds(_driverPropertys.HeartbeatInterval))
                   .SetMaxFailCount(3);
               });

        await tcpDmtpClient.SetupAsync(config).ConfigureAwait(false);
        return tcpDmtpClient;
    }

    private async Task<TcpDmtpService> GetTcpDmtpService()
    {
        var tcpDmtpService = new TcpDmtpService();
        var config = new TouchSocketConfig()
               .SetListenIPHosts(_driverPropertys.ServerUri)
               .SetAdapterOption(new AdapterOption() { MaxPackageSize = 1024 * 1024 * 1024 })
               .SetDmtpOption(new DmtpOption() { VerifyToken = _driverPropertys.VerifyToken })
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
                   .SetTick(TimeSpan.FromMilliseconds(_driverPropertys.HeartbeatInterval))
                   .SetMaxFailCount(3);
               });

        await tcpDmtpService.SetupAsync(config).ConfigureAwait(false);
        return tcpDmtpService;
    }


    public async ValueTask ForcedSync(CancellationToken cancellationToken = default)
    {
        if (_driverPropertys.IsMaster)
        {
            return;
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
            if (!_driverPropertys.IsServer)
            {
                online = (await _tcpDmtpClient.TryConnectAsync().ConfigureAwait(false)).ResultCode == ResultCode.Success;

                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                    var data = await _tcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<List<DataWithDatabase>>(
                                       nameof(ReverseCallbackServer.GetData), waitInvoke).ConfigureAwait(false);

                    data.ForEach(a => a.Channel.Enable = false);
                    await GlobalData.ChannelRuntimeService.CopyAsync(data.Select(a => a.Channel).ToList(), data.SelectMany(a => a.DeviceVariables).ToDictionary(a => a.Device, a => a.Variables), true, cancellationToken).ConfigureAwait(false);

                    LogMessage?.LogTrace($"ForcedSync data success");

                }
            }
            else
            {
                if (_tcpDmtpService.Clients.Count != 0)
                {
                    online = true;
                }
                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    foreach (var item in _tcpDmtpService.Clients)
                    {
                        var data = await item.GetDmtpRpcActor().InvokeTAsync<List<DataWithDatabase>>(nameof(ReverseCallbackServer.GetData), waitInvoke).ConfigureAwait(false);
                        data.ForEach(a => a.Channel.Enable = false);

                        await GlobalData.ChannelRuntimeService.CopyAsync(data.Select(a => a.Channel).ToList(), data.SelectMany(a => a.DeviceVariables).ToDictionary(a => a.Device, a => a.Variables), true, cancellationToken).ConfigureAwait(false);
                        LogMessage?.LogTrace($"{item.GetIPPort()}: ForcedSync data success");
                    }

                }
                else
                {
                    LogMessage?.LogWarning("ForcedSync data error, no client online");
                }
            }


        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 输出警告日志，指示同步数据到从站时发生错误
            LogMessage?.LogWarning(ex, "ForcedSync data error");
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

        if (_driverPropertys.IsMaster)
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
            if (!_driverPropertys.IsServer)
            {
                online = (await _tcpDmtpClient.TryConnectAsync().ConfigureAwait(false)).ResultCode == ResultCode.Success;

                // 如果 online 为 true，表示设备在线
                if (online)
                {
                    // 将 GlobalData.CollectDevices 和 GlobalData.Variables 同步到从站
                    dataResult = await _tcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<Dictionary<string, Dictionary<string, OperResult<object>>>>(
                                       nameof(ReverseCallbackServer.Rpc), waitInvoke, deviceDatas).ConfigureAwait(false);

                    LogMessage?.LogTrace($"Rpc success");

                    return dataResult;
                }
            }
            else
            {
                if (_tcpDmtpService.Clients.Count != 0)
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

                            if (_tcpDmtpService.TryGetClient(key, out var client))
                            {
                                try
                                {

                                    var data = await _tcpDmtpClient.GetDmtpRpcActor().InvokeTAsync<Dictionary<string, Dictionary<string, OperResult<object>>>>(
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
