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
///     HTTP 声明式请求来源地址特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface)]
public sealed class RefererAttribute : Attribute
{
    /// <summary>
    ///     <inheritdoc cref="RefererAttribute" />
    /// </summary>
    /// <param name="referer">请求来源地址，当设置为 <c>"{BASE_ADDRESS}"</c> 时将替换为基地址</param>
    public RefererAttribute(string? referer) => Referer = referer;

    /// <summary>
    ///     请求来源地址，当设置为 <c>"{BASE_ADDRESS}"</c> 时将替换为基地址
    /// </summary>
    public string? Referer { get; set; }
}