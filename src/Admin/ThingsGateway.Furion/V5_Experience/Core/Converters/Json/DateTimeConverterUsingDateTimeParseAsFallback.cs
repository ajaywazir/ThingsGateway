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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThingsGateway.Converters.Json;

/// <summary>
///     <see cref="DateTime" /> JSON 序列化转换器
/// </summary>
/// <remarks>在不符合 <c>ISO 8601-1:2019</c> 格式的 <see cref="DateTime" /> 时间使用 <c>DateTime.Parse</c> 作为回退。</remarks>
public sealed class DateTimeConverterUsingDateTimeParseAsFallback : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 尝试获取 ISO 8601-1:2019 格式时间
        if (!reader.TryGetDateTime(out var value))
        {
            value = DateTime.Parse(reader.GetString()!);
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value);
}