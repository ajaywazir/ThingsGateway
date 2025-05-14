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

using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

using System.Runtime.CompilerServices;

namespace ThingsGateway.Shapeless;

/// <summary>
///     <see cref="Clay" /> 模型绑定提供器
/// </summary>
internal sealed class ClayBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        // 空检查
        ArgumentNullException.ThrowIfNull(context);

        // 获取模型类型和参数特性列表
        var modelType = context.Metadata.ModelType;
        var parameterAttributes = (context.Metadata as DefaultModelMetadata)?.Attributes.ParameterAttributes;

        return modelType == typeof(Clay) ||
               // 确保参数类型为 dynamic 且贴有 [Clay] 特性
               (modelType == typeof(object) && parameterAttributes?.OfType<ClayAttribute>().Any() == true &&
                parameterAttributes.OfType<DynamicAttribute>().Any())
            ? new BinderTypeModelBinder(typeof(ClayBinder))
            : null;
    }
}