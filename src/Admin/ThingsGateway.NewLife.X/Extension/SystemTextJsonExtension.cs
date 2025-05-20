//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------
#if NET6_0_OR_GREATER
using Newtonsoft.Json.Linq;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThingsGateway.NewLife.Json.Extension;

/// <summary>
/// System.Text.Json 扩展
/// </summary>
public static class SystemTextJsonExtension
{
    /// <summary>
    /// 默认Json规则（带缩进）
    /// </summary>
    public static JsonSerializerOptions IndentedOptions;

    /// <summary>
    /// 默认Json规则（无缩进）
    /// </summary>
    public static JsonSerializerOptions NoneIndentedOptions;

    static SystemTextJsonExtension()
    {
        IndentedOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true, // 缩进
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // 忽略 null
        };
        // 如有自定义Converter，这里添加
        // IndentedOptions.Converters.Add(new ByteArrayJsonConverter());
        IndentedOptions.Converters.Add(new ByteArrayToNumberArrayConverterSystemTextJson());
        IndentedOptions.Converters.Add(new JTokenSystemTextJsonConverter());
        IndentedOptions.Converters.Add(new JValueSystemTextJsonConverter());
        IndentedOptions.Converters.Add(new JObjectSystemTextJsonConverter());
        IndentedOptions.Converters.Add(new JArraySystemTextJsonConverter());
        NoneIndentedOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false, // 不缩进
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        NoneIndentedOptions.Converters.Add(new ByteArrayToNumberArrayConverterSystemTextJson());
        NoneIndentedOptions.Converters.Add(new JTokenSystemTextJsonConverter());
        NoneIndentedOptions.Converters.Add(new JValueSystemTextJsonConverter());
        NoneIndentedOptions.Converters.Add(new JObjectSystemTextJsonConverter());
        NoneIndentedOptions.Converters.Add(new JArraySystemTextJsonConverter());
        // NoneIndentedOptions.Converters.Add(new ByteArrayJsonConverter());
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="json"></param>
    /// <param name="type"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static object? FromSystemTextJsonString(this string json, Type type, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize(json, type, options ?? IndentedOptions);
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    public static T? FromSystemTextJsonString<T>(this string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? IndentedOptions);
    }

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="item"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string ToSystemTextJsonString(this object item, JsonSerializerOptions? options)
    {
        return JsonSerializer.Serialize(item, item?.GetType() ?? typeof(object), options ?? IndentedOptions);
    }

    /// <summary>
    /// 序列化
    /// </summary>
    public static string ToSystemTextJsonString(this object item, bool indented = true)
    {
        return JsonSerializer.Serialize(item, item?.GetType() ?? typeof(object), indented ? IndentedOptions : NoneIndentedOptions);
    }

    /// <summary>
    /// 序列化
    /// </summary>
    public static byte[] ToSystemTextJsonUtf8Bytes(this object item, bool indented = true)
    {
        return JsonSerializer.SerializeToUtf8Bytes(item, item.GetType(), indented ? IndentedOptions : NoneIndentedOptions);
    }


}

/// <summary>
/// 将 byte[] 序列化为数值数组，反序列化数值数组为 byte[]
/// </summary>
public class ByteArrayToNumberArrayConverterSystemTextJson : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray token.");
        }

        var bytes = new List<byte>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetByte(out byte value))
                {
                    bytes.Add(value);
                }
                else
                {
                    throw new JsonException("Invalid number value for byte array.");
                }
            }
            else
            {
                throw new JsonException($"Unexpected token {reader.TokenType} in byte array.");
            }
        }

        return bytes.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var b in value)
        {
            writer.WriteNumberValue(b);
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// System.Text.Json → JToken / JObject / JArray 转换器
/// </summary>
public class JTokenSystemTextJsonConverter : JsonConverter<JToken>
{
    public override JToken? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadToken(ref reader);
    }

    private static JToken ReadToken(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var obj = new JObject();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return obj;

                    var propertyName = reader.GetString();
                    reader.Read();
                    var value = ReadToken(ref reader);
                    obj[propertyName!] = value;
                }
                throw new JsonException();

            case JsonTokenType.StartArray:
                var array = new JArray();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        return array;

                    array.Add(ReadToken(ref reader));
                }
                throw new JsonException();

            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var date))
                    return new JValue(date);
                return new JValue(reader.GetString());

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l))
                    return new JValue(l);
                return new JValue(reader.GetDouble());

            case JsonTokenType.True:
                return new JValue(true);

            case JsonTokenType.False:
                return new JValue(false);

            case JsonTokenType.Null:
                return JValue.CreateNull();

            default:
                throw new JsonException($"Unsupported token type {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, JToken value, JsonSerializerOptions options)
    {
        switch (value.Type)
        {
            case JTokenType.Object:
                writer.WriteStartObject();
                foreach (var prop in (JObject)value)
                {
                    writer.WritePropertyName(prop.Key);
                    Write(writer, prop.Value!, options);
                }
                writer.WriteEndObject();
                break;

            case JTokenType.Array:
                writer.WriteStartArray();
                foreach (var item in (JArray)value)
                {
                    Write(writer, item!, options);
                }
                writer.WriteEndArray();
                break;

            case JTokenType.Null:
                writer.WriteNullValue();
                break;

            case JTokenType.Boolean:
                writer.WriteBooleanValue(value.Value<bool>());
                break;

            case JTokenType.Integer:
                writer.WriteNumberValue(value.Value<long>());
                break;

            case JTokenType.Float:
                writer.WriteNumberValue(value.Value<double>());
                break;

            case JTokenType.String:
                writer.WriteStringValue(value.Value<string>());
                break;

            case JTokenType.Date:
                writer.WriteStringValue(value.Value<DateTime>());
                break;

            case JTokenType.Guid:
                writer.WriteStringValue(value.Value<Guid>().ToString());
                break;

            case JTokenType.Uri:
                writer.WriteStringValue(value.Value<Uri>().ToString());
                break;

            case JTokenType.TimeSpan:
                writer.WriteStringValue(value.Value<TimeSpan>().ToString());
                break;

            default:
                // fallback — 转字符串
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}

/// <summary>
/// System.Text.Json → JToken / JObject / JArray 转换器
/// </summary>
public class JObjectSystemTextJsonConverter : JsonConverter<JObject>
{
    public override JObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var obj = new JObject();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return obj;

            var propertyName = reader.GetString();
            reader.Read();
            var value = ReadJToken(ref reader);
            obj[propertyName!] = value;
        }
        throw new JsonException();
    }

    private static JToken ReadJToken(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var obj = new JObject();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return obj;

                    var propertyName = reader.GetString();
                    reader.Read();
                    var value = ReadJToken(ref reader);
                    obj[propertyName!] = value;
                }
                throw new JsonException();

            case JsonTokenType.StartArray:
                var array = new JArray();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        return array;

                    array.Add(ReadJToken(ref reader));
                }
                throw new JsonException();

            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var date))
                    return new JValue(date);
                return new JValue(reader.GetString());

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l))
                    return new JValue(l);
                return new JValue(reader.GetDouble());

            case JsonTokenType.True:
                return new JValue(true);

            case JsonTokenType.False:
                return new JValue(false);

            case JsonTokenType.Null:
                return JValue.CreateNull();

            default:
                throw new JsonException($"Unsupported token type {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, JObject value, JsonSerializerOptions options)
    {

        writer.WriteStartObject();
        foreach (var prop in (JObject)value)
        {
            writer.WritePropertyName(prop.Key);
            Write(writer, prop.Value!, options);
        }
        writer.WriteEndObject();

    }


    private static void Write(Utf8JsonWriter writer, JToken value, JsonSerializerOptions options)
    {
        switch (value.Type)
        {
            case JTokenType.Object:
                writer.WriteStartObject();
                foreach (var prop in (JObject)value)
                {
                    writer.WritePropertyName(prop.Key);
                    Write(writer, prop.Value!, options);
                }
                writer.WriteEndObject();
                break;

            case JTokenType.Array:
                writer.WriteStartArray();
                foreach (var item in (JArray)value)
                {
                    Write(writer, item!, options);
                }
                writer.WriteEndArray();
                break;

            case JTokenType.Null:
                writer.WriteNullValue();
                break;

            case JTokenType.Boolean:
                writer.WriteBooleanValue(value.Value<bool>());
                break;

            case JTokenType.Integer:
                writer.WriteNumberValue(value.Value<long>());
                break;

            case JTokenType.Float:
                writer.WriteNumberValue(value.Value<double>());
                break;

            case JTokenType.String:
                writer.WriteStringValue(value.Value<string>());
                break;

            case JTokenType.Date:
                writer.WriteStringValue(value.Value<DateTime>());
                break;

            case JTokenType.Guid:
                writer.WriteStringValue(value.Value<Guid>().ToString());
                break;

            case JTokenType.Uri:
                writer.WriteStringValue(value.Value<Uri>().ToString());
                break;

            case JTokenType.TimeSpan:
                writer.WriteStringValue(value.Value<TimeSpan>().ToString());
                break;

            default:
                // fallback — 转字符串
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

}

/// <summary>
/// System.Text.Json → JToken / JObject / JArray 转换器
/// </summary>
public class JArraySystemTextJsonConverter : JsonConverter<JArray>
{
    public override JArray? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var array = new JArray();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return array;

            array.Add(ReadToken(ref reader));
        }
        throw new JsonException();
    }

    private static JToken ReadToken(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                var obj = new JObject();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return obj;

                    var propertyName = reader.GetString();
                    reader.Read();
                    var value = ReadToken(ref reader);
                    obj[propertyName!] = value;
                }
                throw new JsonException();

            case JsonTokenType.StartArray:
                var array = new JArray();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        return array;

                    array.Add(ReadToken(ref reader));
                }
                throw new JsonException();

            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var date))
                    return new JValue(date);
                return new JValue(reader.GetString());

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l))
                    return new JValue(l);
                return new JValue(reader.GetDouble());

            case JsonTokenType.True:
                return new JValue(true);

            case JsonTokenType.False:
                return new JValue(false);

            case JsonTokenType.Null:
                return JValue.CreateNull();

            default:
                throw new JsonException($"Unsupported token type {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, JArray value, JsonSerializerOptions options)
    {

        writer.WriteStartArray();
        foreach (var item in (JArray)value)
        {
            Write(writer, item!, options);
        }
        writer.WriteEndArray();

    }

    private static void Write(Utf8JsonWriter writer, JToken value, JsonSerializerOptions options)
    {
        switch (value.Type)
        {
            case JTokenType.Object:
                writer.WriteStartObject();
                foreach (var prop in (JObject)value)
                {
                    writer.WritePropertyName(prop.Key);
                    Write(writer, prop.Value!, options);
                }
                writer.WriteEndObject();
                break;

            case JTokenType.Array:
                writer.WriteStartArray();
                foreach (var item in (JArray)value)
                {
                    Write(writer, item!, options);
                }
                writer.WriteEndArray();
                break;

            case JTokenType.Null:
                writer.WriteNullValue();
                break;

            case JTokenType.Boolean:
                writer.WriteBooleanValue(value.Value<bool>());
                break;

            case JTokenType.Integer:
                writer.WriteNumberValue(value.Value<long>());
                break;

            case JTokenType.Float:
                writer.WriteNumberValue(value.Value<double>());
                break;

            case JTokenType.String:
                writer.WriteStringValue(value.Value<string>());
                break;

            case JTokenType.Date:
                writer.WriteStringValue(value.Value<DateTime>());
                break;

            case JTokenType.Guid:
                writer.WriteStringValue(value.Value<Guid>().ToString());
                break;

            case JTokenType.Uri:
                writer.WriteStringValue(value.Value<Uri>().ToString());
                break;

            case JTokenType.TimeSpan:
                writer.WriteStringValue(value.Value<TimeSpan>().ToString());
                break;

            default:
                // fallback — 转字符串
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}


/// <summary>
/// System.Text.Json → JToken / JObject / JArray 转换器
/// </summary>
public class JValueSystemTextJsonConverter : JsonConverter<JValue>
{
    public override JValue? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return ReadJValue(ref reader);
    }

    private static JValue ReadJValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {

            case JsonTokenType.String:
                if (reader.TryGetDateTime(out var date))
                    return new JValue(date);
                return new JValue(reader.GetString());

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l))
                    return new JValue(l);
                return new JValue(reader.GetDouble());

            case JsonTokenType.True:
                return new JValue(true);

            case JsonTokenType.False:
                return new JValue(false);

            case JsonTokenType.Null:
                return JValue.CreateNull();

            default:
                throw new JsonException($"Unsupported token type {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, JValue value, JsonSerializerOptions options)
    {
        switch (value.Type)
        {

            case JTokenType.Null:
                writer.WriteNullValue();
                break;

            case JTokenType.Boolean:
                writer.WriteBooleanValue(value.Value<bool>());
                break;

            case JTokenType.Integer:
                writer.WriteNumberValue(value.Value<long>());
                break;

            case JTokenType.Float:
                writer.WriteNumberValue(value.Value<double>());
                break;

            case JTokenType.String:
                writer.WriteStringValue(value.Value<string>());
                break;

            case JTokenType.Date:
                writer.WriteStringValue(value.Value<DateTime>());
                break;

            case JTokenType.Guid:
                writer.WriteStringValue(value.Value<Guid>().ToString());
                break;

            case JTokenType.Uri:
                writer.WriteStringValue(value.Value<Uri>().ToString());
                break;

            case JTokenType.TimeSpan:
                writer.WriteStringValue(value.Value<TimeSpan>().ToString());
                break;

            default:
                // fallback — 转字符串
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
#endif