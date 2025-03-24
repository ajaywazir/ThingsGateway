//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using ThingsGateway.ConfigurableOptions;

namespace ThingsGateway.Gateway.Application;

public sealed class ChannelThreadOptions : IConfigurableOptions
{

    public int MinCycleInterval { get; set; } = 10;
    public int MaxCycleInterval { get; set; } = 200;
    public int CheckInterval { get; set; } = 1800000;


    public int MaxChannelCount { get; set; } = 1000;
    public int MaxDeviceCount { get; set; } = 1000;
    public int MaxVariableCount { get; set; } = 1000000;

}