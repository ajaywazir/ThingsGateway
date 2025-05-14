//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 插件配置项
/// <br></br>
/// 使用<see cref="DynamicPropertyAttribute"/> 标识所需的配置属性
/// </summary>
public abstract class CollectFoundationPackPropertyBase : CollectFoundationPropertyBase
{
    /// <summary>
    /// 最大打包长度
    /// </summary>
    [DynamicProperty]
    public ushort MaxPack { get; set; } = 100;


}
public abstract class CollectFoundationPropertyBase : CollectPropertyRetryBase
{
    /// <summary>
    /// 读写超时时间
    /// </summary>
    [DynamicProperty]
    public virtual ushort Timeout { get; set; } = 3000;

    /// <summary>
    /// 帧前时间ms
    /// </summary>
    [DynamicProperty]
    public virtual int SendDelayTime { get; set; } = 0;


    [DynamicProperty]
    public bool IsStringReverseByteWord { get; set; }

    /// <summary>
    /// 默认解析顺序
    /// </summary>
    [DynamicProperty]
    public virtual DataFormatEnum DataFormat { get; set; }


}

/// <summary>
/// 插件配置项
/// <br></br>
/// 使用<see cref="DynamicPropertyAttribute"/> 标识所需的配置属性
/// </summary>
public abstract class CollectFoundationDtuPropertyBase : CollectFoundationPropertyBase
{

    /// <summary>
    /// 默认DtuId
    /// </summary>
    [DynamicProperty]
    public string? DtuId { get; set; }


}

/// <summary>
/// 插件配置项
/// <br></br>
/// 使用<see cref="DynamicPropertyAttribute"/> 标识所需的配置属性
/// </summary>
public abstract class CollectFoundationDtuPackPropertyBase : CollectFoundationPackPropertyBase
{

    /// <summary>
    /// 默认DtuId
    /// </summary>
    [DynamicProperty]
    public string? DtuId { get; set; }


}