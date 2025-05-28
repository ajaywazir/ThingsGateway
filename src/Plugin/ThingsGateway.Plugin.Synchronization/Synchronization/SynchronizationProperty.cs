//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace ThingsGateway.Plugin.Synchronization;

/// <summary>
/// <inheritdoc/>
/// </summary>
public class SynchronizationProperty : BusinessPropertyBase, IBusinessPropertyAllVariableBase
{
    [DynamicProperty]
    public bool IsServer { get; set; } = true;
    [DynamicProperty]
    public bool IsMaster { get; set; }

    [DynamicProperty]
    public bool IsAllVariable { get; set; } = true;

    /// <summary>
    /// 获取或设置远程 URI，用于通信。
    /// </summary>
    [DynamicProperty]
    public string ServerUri { get; set; }

    /// <summary>
    /// 获取或设置用于验证的令牌。
    /// </summary>
    [Required]
    [DynamicProperty]
    public string VerifyToken { get; set; } = "ThingsGateway";

    /// <summary>
    /// 获取或设置心跳间隔。
    /// </summary>
    [MinValue(3000)]
    [DynamicProperty]
    public int HeartbeatInterval { get; set; } = 5000;

    /// <summary>
    /// 获取或设置冗余数据同步间隔(ms)。
    /// </summary>
    [MinValue(1000)]
    [DynamicProperty]
    public int SyncInterval { get; set; } = 3000;
}
