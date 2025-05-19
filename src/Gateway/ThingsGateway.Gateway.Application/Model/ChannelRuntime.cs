﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using Mapster;

using System.Collections.Concurrent;

using ThingsGateway.NewLife;

using TouchSocket.Core;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 业务设备运行状态
/// </summary>
public class ChannelRuntime : Channel, IChannelOptions, IDisposable
{
    /// <summary>
    /// 插件信息
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public PluginInfo? PluginInfo { get; set; }

    /// <summary>
    /// 是否采集
    /// </summary>
    public PluginTypeEnum? PluginType => PluginInfo?.PluginType;

    /// <summary>
    /// 是否采集
    /// </summary>
    [AutoGenerateColumn(Ignore = true)]
    public bool? IsCollect => PluginInfo == null ? null : PluginInfo?.PluginType == PluginTypeEnum.Collect;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public WaitLock WaitLock { get; private set; } = new WaitLock();

    /// <inheritdoc/>
    [MinValue(1)]
    public override int MaxConcurrentCount
    {
        get
        {
            return _maxConcurrentCount;
        }
        set
        {
            if (value > 0)
            {
                _maxConcurrentCount = value;

                if (WaitLock?.MaxCount != MaxConcurrentCount)
                {
                    var _lock = WaitLock;
                    WaitLock = new WaitLock(_maxConcurrentCount);
                    _lock?.SafeDispose();
                }
            }
        }
    }

    private volatile int _maxConcurrentCount = 1;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public TouchSocketConfig Config { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public IReadOnlyDictionary<long, DeviceRuntime>? ReadDeviceRuntimes => DeviceRuntimes;

    /// <summary>
    /// 设备变量
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    internal ConcurrentDictionary<long, DeviceRuntime>? DeviceRuntimes { get; } = new(Environment.ProcessorCount, 1000);

    /// <summary>
    /// 设备数量
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public int? DeviceRuntimeCount => DeviceRuntimes?.Count;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AdaptIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public IDeviceThreadManage? DeviceThreadManage { get; internal set; }

    [AutoGenerateColumn(Ignore = true)]
    public string LogPath => Name.GetChannelLogPath();


    public void Init()
    {
        // 通过插件名称获取插件信息
        PluginInfo = GlobalData.PluginService.GetList().FirstOrDefault(A => A.FullName == PluginName);

        GlobalData.Channels.TryRemove(Id, out _);

        GlobalData.Channels.TryAdd(Id, this);

    }

    public void Dispose()
    {
        //Config?.SafeDispose();

        GlobalData.Channels.TryRemove(Id, out _);
        DeviceThreadManage = null;
        GC.SuppressFinalize(this);
    }
    public override string ToString()
    {
        if (ChannelType == ChannelTypeEnum.Other)
        {
            return Name;
        }
        return $"{Name}[{base.ToString()}]";
    }


    public IChannel GetChannel(TouchSocketConfig config)
    {
        lock (GlobalData.Channels)
        {

            if (DeviceThreadManage?.Channel?.DisposedValue == false)
                return DeviceThreadManage?.Channel;


            if (ChannelType == ChannelTypeEnum.TcpService
                || ChannelType == ChannelTypeEnum.SerialPort
                || ChannelType == ChannelTypeEnum.UdpSession
                )
            {
                //获取相同配置的Tcp服务或Udp服务或COM
                var same = GlobalData.Channels.FirstOrDefault(a =>
                 {
                     if (a.Value == this)
                         return false;
                     if (a.Value.DeviceThreadManage?.Channel?.DisposedValue == true || a.Value.DeviceThreadManage?.Channel?.DisposedValue == null)
                         return false;

                     if (a.Value.ChannelType == ChannelType)
                     {
                         if (a.Value.ChannelType == ChannelTypeEnum.TcpService)
                             if (a.Value.BindUrl == BindUrl)
                                 return true;
                         if (a.Value.ChannelType == ChannelTypeEnum.UdpSession)
                             if ((!BindUrl.IsNullOrWhiteSpace()) && a.Value.BindUrl == BindUrl)
                                 return true;
                         if (a.Value.ChannelType == ChannelTypeEnum.SerialPort)
                             if (a.Value.PortName == PortName)
                                 return true;
                     }
                     return false;
                 }).Value;

                if (same != null)
                {
                    return same.GetChannel(config);
                }
            }

            if (DeviceThreadManage?.Channel?.DisposedValue == false)
                return DeviceThreadManage?.Channel;

            var ichannel = config.GetChannel(this);
            return ichannel;
        }

    }

}
