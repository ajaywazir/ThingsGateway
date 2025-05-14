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

using Microsoft.Extensions.Options;

using System.Text.Json;

namespace ThingsGateway.HttpRemote;

/// <summary>
///     <see cref="HttpClient" /> 配置选项
/// </summary>
public sealed class HttpClientOptions
{
    /// <summary>
    ///     JSON 序列化配置
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new(HttpRemoteOptions.JsonSerializerOptionsDefault);

    /// <summary>
    ///     标识选项是否配置为默认值（未配置）
    /// </summary>
    /// <remarks>用于避免通过 <see cref="IOptionsSnapshot{TOptions}" /> 获取选项时无法确定是否已配置该选项。默认值为：<c>true</c>。</remarks>
    internal bool IsDefault { get; set; } = true;
}