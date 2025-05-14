//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using ThingsGateway.Gateway.Application;

namespace ThingsGateway.Plugin.Dlt645;

/// <summary>
/// <inheritdoc/>
/// </summary>
public class Dlt645_2007MasterProperty : CollectFoundationDtuPropertyBase
{
    /// <summary>
    /// 默认地址
    /// </summary>
    [DynamicProperty]
    public string Station { get; set; } = "111111111111";

    /// <summary>
    /// 密码
    /// </summary>
    [DynamicProperty]
    public string Password { get; set; }

    /// <summary>
    /// 操作员代码
    /// </summary>
    [DynamicProperty]
    public string OperCode { get; set; }

    /// <summary>
    /// 前导符报文头
    /// </summary>
    [DynamicProperty]
    public string FEHead { get; set; } = "FEFEFEFE";

}
