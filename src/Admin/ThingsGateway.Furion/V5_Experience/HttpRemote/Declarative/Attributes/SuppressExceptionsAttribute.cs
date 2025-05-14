// ------------------------------------------------------------------------
// 版权信息
// 版权归百小僧及百签科技（广东）有限公司所有。
// 所有权利保留。
// 官方网站：https://baiqian.com
//
// 许可证信息
// 项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。
// 许可证的完整文本可以在源代码树根目录中的 LICENSE-APACHE 和 LICENSE-MIT 文件中找到。
// ------------------------------------------------------------------------

namespace ThingsGateway.HttpRemote;

/// <summary>
///     HTTP 声明式异常抑制特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface)]
public sealed class SuppressExceptionsAttribute : Attribute
{
    /// <summary>
    ///     <inheritdoc cref="SuppressExceptionsAttribute" />
    /// </summary>
    /// <remarks>抑制所有异常。</remarks>
    public SuppressExceptionsAttribute()
        : this(true)
    {
    }

    /// <summary>
    ///     <inheritdoc cref="SuppressExceptionsAttribute" />
    /// </summary>
    /// <param name="enabled">是否启用异常抑制。当设置为 <c>false</c> 时，将禁用异常抑制机制。</param>
    public SuppressExceptionsAttribute(bool enabled) => Types = enabled ? [typeof(Exception)] : [];

    /// <summary>
    ///     <inheritdoc cref="SuppressExceptionsAttribute" />
    /// </summary>
    /// <param name="types">异常抑制类型集合</param>
    public SuppressExceptionsAttribute(params Type[] types) => Types = types;

    /// <summary>
    ///     异常抑制类型集合
    /// </summary>
    public Type[] Types { get; set; }
}