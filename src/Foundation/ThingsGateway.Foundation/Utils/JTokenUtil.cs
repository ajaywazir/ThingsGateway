//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;

namespace ThingsGateway.Foundation;

/// <summary>
/// JTokenUtil
/// </summary>
public static class JTokenUtil
{
    /// <summary>
    /// 根据字符串解析对应JToken<br></br>
    /// 字符串可以不包含转义双引号，如果解析失败会直接转成String类型的JValue
    /// true/false可忽略大小写
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public static JToken GetJTokenFromString(this string item)
    {
        try
        {
            if (item.IsNullOrWhiteSpace())
                return JValue.CreateNull();

            if (bool.TryParse(item, out bool parseBool))
                return new JValue(parseBool);

            // 尝试解析字符串为 JToken 对象
            return JToken.Parse(item);
        }
        catch
        {
            // 解析失败时，将其转为 String 类型的 JValue
            return new JValue(item);
        }
    }

    /// <summary>
    /// 根据JToken获取Object类型值<br></br>
    /// 对应返回 对象字典 或 类型数组 或 类型值
    /// </summary>
    public static object? GetObjectFromJToken(this JToken token)
    {
        if (token == null)
            return null;

        switch (token.Type)
        {
            case JTokenType.Object:
                var obj = new Dictionary<string, object>();
                foreach (var prop in (JObject)token)
                    obj[prop.Key] = GetObjectFromJToken(prop.Value);
                return obj;

            case JTokenType.Array:
                var array = (JArray)token;

                if (array.All(x => x.Type == JTokenType.Integer))
                    return array.Select(x => x.Value<long>()).ToList();

                if (array.All(x => x.Type == JTokenType.Float))
                    return array.Select(x => x.Value<double>()).ToList();

                if (array.All(x => x.Type == JTokenType.String))
                    return array.Select(x => x.Value<string>()).ToList();

                if (array.All(x => x.Type == JTokenType.Boolean))
                    return array.Select(x => x.Value<bool>()).ToList();

                if (array.All(x => x.Type == JTokenType.Date))
                    return array.Select(x => x.Value<DateTime>()).ToList();

                if (array.All(x => x.Type == JTokenType.TimeSpan))
                    return array.Select(x => x.Value<TimeSpan>()).ToList();

                if (array.All(x => x.Type == JTokenType.Guid))
                    return array.Select(x => x.Value<Guid>()).ToList();

                if (array.All(x => x.Type == JTokenType.Uri))
                    return array.Select(x => x.Value<Uri>()).ToList();

                // 否则递归
                return array.Select(x => GetObjectFromJToken(x)).ToList();

            case JTokenType.Integer:
                return token.ToObject<long>();

            case JTokenType.Float:
                return token.ToObject<double>();

            case JTokenType.String:
                return token.ToObject<string>();

            case JTokenType.Boolean:
                return token.ToObject<bool>();

            case JTokenType.Null:
            case JTokenType.Undefined:
                return null;

            case JTokenType.Date:
                return token.ToObject<DateTime>();

            case JTokenType.TimeSpan:
                return token.ToObject<TimeSpan>();

            case JTokenType.Guid:
                return token.ToObject<Guid>();

            case JTokenType.Uri:
                return token.ToObject<Uri>();

            case JTokenType.Bytes:
                return token.ToObject<byte[]>();

            case JTokenType.Comment:
            case JTokenType.Raw:
            case JTokenType.Property:
            case JTokenType.Constructor:
            default:
                return token.ToString();
        }

    }

    #region json

    /// <summary>
    /// 维度
    /// </summary>
    /// <param name="jToken"></param>
    /// <returns></returns>
    public static int CalculateActualValueRank(this JToken jToken)
    {
        if (jToken.Type != JTokenType.Array)
            return -1;

        var jArray = jToken.ToArray();
        int numDimensions = 1;

        while (jArray.GetElementsType() == JTokenType.Array)
        {
            jArray = jArray.Children().ToArray();
            numDimensions++;
        }
        return numDimensions;
    }

    private static JTokenType GetElementsType(this JToken[] jTokens)
    {
        return jTokens.First().Type;
    }

    #endregion json
}
