//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Foundation;

/// <inheritdoc/>
public static class PluginUtil
{
    /// <summary>
    /// 作为DTU终端
    /// </summary>
    public static Action<IPluginManager> GetDtuClientPlugin(IChannelOptions channelOptions)
    {
        if (!channelOptions.DtuId.IsNullOrWhiteSpace())
        {
            Action<IPluginManager> action = a => { };

            action += a =>
            {
                var plugin = a.Add<HeartbeatAndReceivePlugin>();
                plugin.Heartbeat = channelOptions.Heartbeat;
                plugin.DtuId = channelOptions.DtuId;
                plugin.HeartbeatTime = channelOptions.HeartbeatTime;
            };

            if (channelOptions.ChannelType == ChannelTypeEnum.TcpClient)
            {
                action += a => a.UseTcpReconnection();
            }
            return action;
        }
        return a => { };
    }

    /// <summary>
    /// 作为DTU服务
    /// </summary>
    public static Action<IPluginManager> GetDtuPlugin(IChannelOptions channelOptions)
    {
        Action<IPluginManager> action = a => { };

        action += GetTcpServicePlugin(channelOptions);
        if (!channelOptions.Heartbeat.IsNullOrWhiteSpace())
        {
            action += a =>
            {
                var plugin = a.Add<DtuPlugin>();
                plugin.Heartbeat = channelOptions.Heartbeat;
            };
        }
        return action;
    }

    /// <summary>
    /// 作为TCP服务
    /// </summary>
    /// <param name="channelOptions"></param>
    /// <returns></returns>
    public static Action<IPluginManager> GetTcpServicePlugin(IChannelOptions channelOptions)
    {
        Action<IPluginManager> action = a => { };
        if (channelOptions.CheckClearTime > 0)
        {
            action += a =>
            {
                a.UseCheckClear()
        .SetCheckClearType(CheckClearType.All)
        .SetTick(TimeSpan.FromMilliseconds(channelOptions.CheckClearTime))
        .SetOnClose(async (c, t) =>
        {
            await c.ShutdownAsync(System.Net.Sockets.SocketShutdown.Both).ConfigureAwait(false);
            await c.CloseAsync($"{channelOptions.CheckClearTime}ms Timeout").ConfigureAwait(false);
        });
            };

        }
        return action;
    }


}
