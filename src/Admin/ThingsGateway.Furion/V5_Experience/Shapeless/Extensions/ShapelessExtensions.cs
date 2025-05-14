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

namespace ThingsGateway.Shapeless.Extensions;

/// <summary>
///     流变对象模块拓展类
/// </summary>
public static class ShapelessExtensions
{
    /// <summary>
    ///     将对象转换为 <see cref="Clay" /> 实例
    /// </summary>
    /// <param name="obj">
    ///     <see cref="object" />
    /// </param>
    /// <param name="options">
    ///     <see cref="ClayOptions" />
    /// </param>
    /// <returns>
    ///     <see cref="Clay" />
    /// </returns>
    public static Clay ToClay(this object? obj, ClayOptions? options = null) => Clay.Parse(obj, options);

    /// <summary>
    ///     将对象转换为 <see cref="Clay" /> 实例
    /// </summary>
    /// <param name="obj">
    ///     <see cref="object" />
    /// </param>
    /// <param name="configure">自定义配置委托</param>
    /// <returns>
    ///     <see cref="Clay" />
    /// </returns>
    public static Clay ToClay(this object? obj, Action<ClayOptions> configure) => Clay.Parse(obj, configure);

    /// <summary>
    ///     将 <see cref="Clay" /> 实例通过转换管道传递并返回新的 <see cref="Clay" />（失败时抛出异常）
    /// </summary>
    /// <param name="clayTask">
    ///     <see cref="Task{TResult}" />
    /// </param>
    /// <param name="transformer">转换函数</param>
    /// <returns>
    ///     <see cref="Clay" />
    /// </returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<Clay?> PipeAsync(this Task<Clay?> clayTask, Func<dynamic, dynamic?> transformer)
    {
        var clay = await clayTask.ConfigureAwait(false);
        return clay?.Pipe(transformer);
    }

    /// <summary>
    ///     尝试将 <see cref="Clay" /> 实例通过转换管道传递，失败时返回原始对象
    /// </summary>
    /// <param name="clayTask">
    ///     <see cref="Task{TResult}" />
    /// </param>
    /// <param name="transformer">returns</param>
    /// <returns>
    ///     <see cref="Clay" />
    /// </returns>
    public static async Task<Clay?> PipeTryAsync(this Task<Clay?> clayTask, Func<dynamic, dynamic?> transformer)
    {
        var clay = await clayTask.ConfigureAwait(false);
        return clay?.PipeTry(transformer);
    }
}