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

using System.Text;

namespace ThingsGateway.HttpRemote;

/// <summary>
///     HTTP 远程请求分析类
/// </summary>
public sealed class HttpRemoteAnalyzer
{
    /// <summary>
    ///     分析数据构建器
    /// </summary>
    internal readonly StringBuilder _dataBuffer;

    /// <summary>
    ///     分析数据缓存字段
    /// </summary>
    internal string? _cachedData;

    /// <summary>
    ///     <inheritdoc cref="HttpRemoteAnalyzer" />
    /// </summary>
    internal HttpRemoteAnalyzer() => _dataBuffer = new StringBuilder();

    /// <summary>
    ///     分析数据
    /// </summary>
    public string Data => _cachedData ??= _dataBuffer.ToString().TrimEnd(Environment.NewLine.ToCharArray());

    /// <summary>
    ///     追加分析数据
    /// </summary>
    /// <param name="value">分析数据</param>
    internal void AppendData(string? value)
    {
        _dataBuffer.AppendLine(value);
        _cachedData = null;
    }

    /// <inheritdoc />
    public override string ToString() => Data;
}