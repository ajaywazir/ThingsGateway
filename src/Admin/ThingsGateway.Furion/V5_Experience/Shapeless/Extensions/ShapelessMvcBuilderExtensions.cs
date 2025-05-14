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

using Microsoft.AspNetCore.Mvc;

using ThingsGateway.Shapeless;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     流变对象模块 <see cref="IMvcBuilder" /> 拓展类
/// </summary>
public static class ShapelessMvcBuilderExtensions
{
    /// <summary>
    ///     添加 <see cref="Clay" /> 配置
    /// </summary>
    /// <param name="builder">
    ///     <see cref="IMvcBuilder" />
    /// </param>
    /// <returns>
    ///     <see cref="IMvcBuilder" />
    /// </returns>
    public static IMvcBuilder AddClayOptions(this IMvcBuilder builder) => builder.AddClayOptions(_ => { });

    /// <summary>
    ///     添加 <see cref="Clay" /> 配置
    /// </summary>
    /// <param name="builder">
    ///     <see cref="IMvcBuilder" />
    /// </param>
    /// <param name="configure">自定义配置委托</param>
    /// <returns>
    ///     <see cref="IMvcBuilder" />
    /// </returns>
    public static IMvcBuilder AddClayOptions(this IMvcBuilder builder, Action<ClayOptions> configure)
    {
        // 空检查
        ArgumentNullException.ThrowIfNull(configure);

        // 配置 JsonOptions 选项，添加 ClayJsonConverter 转换器
        builder.Services.Configure<JsonOptions>(options =>
        {
            if (!options.JsonSerializerOptions.Converters.OfType<ClayJsonConverter>().Any())
            {
                options.JsonSerializerOptions.Converters.Add(new ClayJsonConverter());
            }
        });

        // 配置 ClayOptions 选项服务
        builder.Services.Configure(configure);

        // 添加 Clay 模型绑定提供器
        builder.Services.Configure<MvcOptions>(options =>
        {
            if (!options.ModelBinderProviders.OfType<ClayBinderProvider>().Any())
            {
                options.ModelBinderProviders.Insert(0, new ClayBinderProvider());
            }
        });

        return builder;
    }
}