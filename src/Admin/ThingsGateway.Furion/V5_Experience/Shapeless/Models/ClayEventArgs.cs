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

namespace ThingsGateway.Shapeless;

/// <summary>
///     <see cref="Clay" /> 对象事件参数
/// </summary>
public sealed class ClayEventArgs : EventArgs
{
    /// <summary>
    ///     <inheritdoc cref="ClayEventArgs" />
    /// </summary>
    /// <param name="identifier">标识符，可以是键（字符串）或索引（整数）或索引运算符（Index）或范围运算符（Range）</param>
    /// <param name="isFound">指示标识符是否存在</param>
    internal ClayEventArgs(object identifier, bool isFound)
    {
        Identifier = identifier;
        IsFound = isFound;
    }

    /// <summary>
    ///     标识符，可以是键（字符串）或索引（整数）或索引运算符（Index）或范围运算符（Range）
    /// </summary>
    public object Identifier { get; }

    /// <summary>
    ///     指示标识符是否存在
    /// </summary>
    public bool IsFound { get; }
}