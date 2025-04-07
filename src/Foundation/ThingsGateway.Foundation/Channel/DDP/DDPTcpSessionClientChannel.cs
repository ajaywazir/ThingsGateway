//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------


using System.Runtime.CompilerServices;

using ThingsGateway.NewLife;

using TouchSocket.Resources;

namespace ThingsGateway.Foundation;

public class DDPTcpSessionClientChannel : TcpSessionClientChannel
{
    /// <summary>
    /// 当客户端完整建立Tcp连接时触发。
    /// <para>
    /// 覆盖父类方法，将不会触发<see cref="ITcpConnectedPlugin"/>插件。
    /// </para>
    /// </summary>
    /// <param name="e">包含连接信息的事件参数</param>
    protected override Task OnTcpConnected(ConnectedEventArgs e)
    {

        // 如果当前实例的配置不为空，则将配置应用到适配器
        if (Config != null)
        {
            DDPAdapter.Config(Config);
        }

        // 将当前实例的日志记录器和加载回调设置到适配器中
        DDPAdapter.Logger = Logger;
        DDPAdapter.OnLoaded(this);

        DDPAdapter.SendAsyncCallBack = DDPSendAsync;
        DDPAdapter.ReceivedAsyncCallBack = DDPHandleReceivedData;
        DataHandlingAdapter.SendAsyncCallBack = DefaultSendAsync;
        return base.OnTcpConnected(e);
    }
    protected Task DefaultSendAsync(ReadOnlyMemory<byte> memory)
    {
        return DDPAdapter.SendInputAsync(new DDPSend(memory, Id,true));
    }
    protected Task DDPSendAsync(ReadOnlyMemory<byte> memory)
    {
        return base.ProtectedDefaultSendAsync(memory);
    }

    private DDPMessage DDPMessage { get; set; }
    private Task DDPHandleReceivedData(ByteBlock byteBlock, IRequestInfo requestInfo)
    {
        if (requestInfo is DDPMessage dDPMessage)
            DDPMessage = dDPMessage;

        return EasyTask.CompletedTask;
    }



    private DeviceSingleStreamDataHandleAdapter<DDPTcpMessage> DDPAdapter = new();
    private WaitLock _waitLock = new();
    protected override async ValueTask<bool> OnTcpReceiving(ByteBlock byteBlock)
    {
        DDPMessage? message = null;
        try
        {
            await _waitLock.WaitAsync().ConfigureAwait(false);
            await DDPAdapter.ReceivedInputAsync(byteBlock).ConfigureAwait(EasyTask.ContinueOnCapturedContext);

            message = DDPMessage;
            DDPMessage = null;
        }
        finally
        {
            _waitLock.Release();
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
                        await ResetIdAsync(id).ConfigureAwait(false);

                        //发送成功
                        await DDPAdapter.SendInputAsync(new DDPSend(ReadOnlyMemory<byte>.Empty, id, true, 0x81)).ConfigureAwait(false);

                        Logger?.Info(DefaultResource.Localizer["DtuConnected", Id]);
                    }
                    else if (message.Type == 0x02)
                    {
                        await DDPAdapter.SendInputAsync(new DDPSend(ReadOnlyMemory<byte>.Empty, Id, true, 0x82)).ConfigureAwait(false);
                        Logger?.Info(DefaultResource.Localizer["DtuDisconnecting", Id]);
                        await Task.Delay(100).ConfigureAwait(false);
                        await this.CloseAsync().ConfigureAwait(false);
                        this.SafeDispose();

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

}
