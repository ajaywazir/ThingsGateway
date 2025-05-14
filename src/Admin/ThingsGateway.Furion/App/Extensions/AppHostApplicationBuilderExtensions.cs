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

using Microsoft.Extensions.DependencyInjection;

using ThingsGateway;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// HostApplication 拓展
/// </summary>
public static class AppHostApplicationBuilderExtensions
{
    /// <summary>
    /// Host 应用注入
    /// </summary>
    /// <param name="hostApplicationBuilder">Host 应用构建器</param>
    /// <param name="autoRegisterBackgroundService"></param>
    /// <returns>HostApplicationBuilder</returns>
    public static HostApplicationBuilder Inject(this HostApplicationBuilder hostApplicationBuilder, bool autoRegisterBackgroundService = true)
    {
        // 初始化配置
        InternalApp.ConfigureApplication(hostApplicationBuilder, autoRegisterBackgroundService);

        return hostApplicationBuilder;
    }

    /// <summary>
    /// 注册依赖组件
    /// </summary>
    /// <typeparam name="TComponent">派生自 <see cref="IServiceComponent"/></typeparam>
    /// <param name="hostApplicationBuilder">Host 应用构建器</param>
    /// <param name="options">组件参数</param>
    /// <returns></returns>
    public static HostApplicationBuilder AddComponent<TComponent>(this HostApplicationBuilder hostApplicationBuilder, object options = default)
        where TComponent : class, IServiceComponent, new()
    {
        hostApplicationBuilder.Services.AddComponent<TComponent>(options);

        return hostApplicationBuilder;
    }

    /// <summary>
    /// 注册依赖组件
    /// </summary>
    /// <typeparam name="TComponent">派生自 <see cref="IServiceComponent"/></typeparam>
    /// <typeparam name="TComponentOptions">组件参数</typeparam>
    /// <param name="hostApplicationBuilder">Host 应用构建器</param>
    /// <param name="options">组件参数</param>
    /// <returns><see cref="HostApplicationBuilder"/></returns>
    public static HostApplicationBuilder AddComponent<TComponent, TComponentOptions>(this HostApplicationBuilder hostApplicationBuilder, TComponentOptions options = default)
        where TComponent : class, IServiceComponent, new()
    {
        hostApplicationBuilder.Services.AddComponent<TComponent, TComponentOptions>(options);

        return hostApplicationBuilder;
    }

    /// <summary>
    /// 注册依赖组件
    /// </summary>
    /// <param name="hostApplicationBuilder">Host 应用构建器</param>
    /// <param name="componentType">组件类型</param>
    /// <param name="options">组件参数</param>
    /// <returns><see cref="HostApplicationBuilder"/></returns>
    public static HostApplicationBuilder AddComponent(this HostApplicationBuilder hostApplicationBuilder, Type componentType, object options = default)
    {
        hostApplicationBuilder.Services.AddComponent(componentType, options);

        return hostApplicationBuilder;
    }
}