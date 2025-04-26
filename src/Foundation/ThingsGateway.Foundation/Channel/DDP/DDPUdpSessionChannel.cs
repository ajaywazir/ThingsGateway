//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;

using ThingsGateway.NewLife;

using TouchSocket.Resources;

namespace ThingsGateway.Foundation;

/// <summary>
/// Udp通道
/// </summary>
public class DDPUdpSessionChannel : UdpSessionChannel, IClientChannel, IDtuUdpSessionChannel
{
    public DDPUdpSessionChannel(IChannelOptions channelOptions) : base(channelOptions)
    {

    }
    protected override void LoadConfig(TouchSocketConfig config)
    {
        base.LoadConfig(config);

        // 如果当前实例的配置不为空，则将配置应用到适配器
        if (Config != null)
        {
            DDPAdapter.Config(Config);
        }

        // 将当前实例的日志记录器和加载回调设置到适配器中
        DDPAdapter.Logger = Logger;

        if (DDPAdapter.Owner != null)
        {
            DDPAdapter.OnLoaded(this);
        }

        DDPAdapter.SendCallBackAsync = DDPSendAsync;
        DDPAdapter.ReceivedCallBack = DDPHandleReceivedData;
        DataHandlingAdapter.SendCallBackAsync = DefaultSendAsync;

    }


    protected Task DefaultSendAsync(EndPoint endPoint, ReadOnlyMemory<byte> memory)
    {
        if (TryGetId(endPoint, out var id))
        {
            return DDPAdapter.SendInputAsync(endPoint, new DDPSend(memory, id, false));
        }
        else
        {
            throw new ClientNotFindException();
        }
    }

    protected Task DDPSendAsync(EndPoint endPoint, ReadOnlyMemory<byte> memory)
    {
        //获取endpoint
        return base.ProtectedDefaultSendAsync(endPoint, memory);
    }

    private ConcurrentDictionary<EndPoint, DDPMessage> DDPMessageDict { get; set; } = new();
    private Task DDPHandleReceivedData(EndPoint endPoint, ByteBlock byteBlock, IRequestInfo requestInfo)
    {
        if (requestInfo is DDPMessage dDPMessage)
        {
            DDPMessageDict.AddOrUpdate(endPoint, dDPMessage);
        }

        return EasyTask.CompletedTask;
    }

    private DeviceUdpDataHandleAdapter<DDPUdpMessage> DDPAdapter = new();


    public EndPoint DefaultEndpoint => RemoteIPHost?.EndPoint;

    ConcurrentDictionary<string, WaitLock> WaitLocks { get; } = new();

    public override WaitLock GetLock(string key)
    {
        if (key.IsNullOrEmpty()) return WaitLock;
        return WaitLocks.GetOrAdd(key, (a) => new WaitLock(WaitLock.MaxCount));
    }

    public override Task<Result> StopAsync(CancellationToken token)
    {
        WaitLocks.ForEach(a => a.Value.SafeDispose());
        WaitLocks.Clear();
        return base.StopAsync(token);
    }

    private ConcurrentDictionary<EndPoint, WaitLock> _waitLocks = new();

    protected override async ValueTask<bool> OnUdpReceiving(UdpReceiveingEventArgs e)
    {
        var byteBlock = e.ByteBlock;
        var endPoint = e.EndPoint;
        DDPMessage? message = null;
        var waitLock = _waitLocks.GetOrAdd(endPoint, new WaitLock());
        try
        {
            await waitLock.WaitAsync().ConfigureAwait(false);
            await DDPAdapter.ReceivedInput(endPoint, byteBlock).ConfigureAwait(EasyTask.ContinueOnCapturedContext);

            if (DDPMessageDict.TryGetValue(endPoint, out var dDPMessage))
                message = dDPMessage;
            DDPMessageDict.TryRemove(endPoint, out _);
        }
        finally
        {
            waitLock.Release();
        }

        if (message != null)
        {
            if (message.IsSuccess)
            {
                var id = $"ID={message.Id}";
                if (message.Type == 0x09)
                {
                    byteBlock.Reset();
                    byteBlock.Write(message.Content);
                    return false;
                }
                else
                {
                    if (message.Type == 0x01)
                    {
                        //注册ID
                        if (!IdDict.TryAdd(endPoint, id))
                        {
                            IdDict[endPoint] = id;
                        }
                        if (!EndPointDcit.TryAdd(id, endPoint))
                        {
                            EndPointDcit[id] = endPoint;
                        }

                        //发送成功
                        await DDPAdapter.SendInputAsync(endPoint, new DDPSend(ReadOnlyMemory<byte>.Empty, id, false, 0x81)).ConfigureAwait(false);
                        Logger?.Info(DefaultResource.Localizer["DtuConnected", id]);
                    }
                    else if (message.Type == 0x02)
                    {
                        await DDPAdapter.SendInputAsync(endPoint, new DDPSend(ReadOnlyMemory<byte>.Empty, id, false, 0x82)).ConfigureAwait(false);
                        Logger?.Info(DefaultResource.Localizer["DtuDisconnecting", id]);
                        await Task.Delay(100).ConfigureAwait(false);
                        IdDict.TryRemove(endPoint, out _);
                        EndPointDcit.TryRemove(id, out _);

                    }
                }

            }
        }
        return true;
    }


    #region Throw

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfCannotSendRequestInfo()
    {
        if (DataHandlingAdapter == null || !DataHandlingAdapter.CanSendRequestInfo)
        {
            throw new NotSupportedException(TouchSocketResource.CannotSendRequestInfo);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfClientNotConnected()
    {
        if (Online)
        {
            return;
        }
        throw new ClientNotConnectedException();
    }

    #endregion Throw





    InternalConcurrentDictionary<EndPoint, string> IdDict { get; set; } = new();
    InternalConcurrentDictionary<string, EndPoint> EndPointDcit { get; set; } = new();
    public bool TryGetId(EndPoint endPoint, out string id)
    {
        return IdDict.TryGetValue(endPoint, out id);
    }

    public bool TryGetEndPoint(string id, out EndPoint endPoint)
    {
        return EndPointDcit.TryGetValue(id, out endPoint);
    }
}
