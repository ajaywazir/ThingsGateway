//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace ThingsGateway.Foundation;

/// <summary>
/// <inheritdoc/>
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// 将文件名或文件夹名中非法字符替换为下划线，保留中文字符。
    /// 支持 Windows、Linux 和 macOS 合法文件名。
    /// </summary>
    public static string SanitizeFileName(this string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        // 定义允许的字符：中文、英文字母、数字、下划线、点、短横线和空格
        // 去除非法字符：Windows: \ / : * ? " < > | 和控制字符（0x00-0x1F）
        string pattern = @"[\\/:*?""<>|\x00-\x1F]";

        // 保留中文 (\u4e00-\u9fa5)、英文字母、数字、_、-、.、空格
        name = Regex.Replace(name, pattern, "_");

        // 去除结尾的点或空格（Windows 不允许）
        name = name.TrimEnd('.', ' ');

        // 如果结果为空，则返回默认名
        return string.IsNullOrWhiteSpace(name) ? "_" : name;
    }

    /// <summary>
    /// GetDebugLogBasePath
    /// </summary>
    public static string GetDebugLogBasePath()
    {
        return "Logs/DebugLog";
    }

    /// <summary>
    /// 获取DEBUG日志路径
    /// </summary>
    /// <param name="channelId"></param>
    /// <returns></returns>
    public static string GetDebugLogPath(this long channelId)
    {
        return GetDebugLogBasePath().CombinePath(channelId.ToString());
    }

    /// <summary>
    /// 获取DEBUG日志路径
    /// </summary>
    /// <param name="channelId"></param>
    /// <returns></returns>
    public static string GetDebugLogPath(this string channelId)
    {
        return GetDebugLogBasePath().CombinePath(channelId.SanitizeFileName());
    }

    /// <summary>
    /// GetLogBasePath
    /// </summary>
    public static string GetChannelLogBasePath()
    {
        return "Logs/ChannelLog";
    }

    /// <summary>
    /// 获取日志路径
    /// </summary>
    /// <param name="channelId"></param>
    /// <returns></returns>
    public static string GetChannelLogPath(this long channelId)
    {
        return GetChannelLogBasePath().CombinePath(channelId.ToString());
    }
    /// <summary>
    /// 获取日志路径
    /// </summary>
    /// <param name="channelId"></param>
    /// <returns></returns>
    public static string GetChannelLogPath(this string channelId)
    {
        return GetChannelLogBasePath().CombinePath(channelId.SanitizeFileName());
    }



    /// <summary>
    /// GetLogBasePath
    /// </summary>
    public static string GetDeviceLogBasePath()
    {
        return "Logs/DeviceLog";
    }

    /// <summary>
    /// 获取日志路径
    /// </summary>
    /// <param name="DeviceId"></param>
    /// <returns></returns>
    public static string GetDeviceLogPath(this long DeviceId)
    {
        return GetDeviceLogBasePath().CombinePath(DeviceId.ToString());
    }
    /// <summary>
    /// 获取日志路径
    /// </summary>
    /// <param name="DeviceId"></param>
    /// <returns></returns>
    public static string GetDeviceLogPath(this string DeviceId)
    {
        return GetDeviceLogBasePath().CombinePath(DeviceId.SanitizeFileName());
    }

    #region 日志

    /// <summary>
    /// 输出错误日志
    /// </summary>
    public static void LogError(this ILog logger, Exception ex, string msg)
    {
        logger.Log(TouchSocket.Core.LogLevel.Error, null, msg, ex);
    }

    /// <summary>
    /// 输出错误日志
    /// </summary>
    public static void LogError(this ILog logger, Exception ex)
    {
        logger.Log(TouchSocket.Core.LogLevel.Error, null, ex.Message, ex);
    }

    /// <summary>
    /// 输出提示日志
    /// </summary>
    public static void LogInformation(this ILog logger, string msg)
    {
        logger.Log(TouchSocket.Core.LogLevel.Info, null, msg, null);
    }

    /// <summary>
    /// 输出Trace日志
    /// </summary>
    public static void LogTrace(this ILog logger, string msg)
    {
        logger.Log(TouchSocket.Core.LogLevel.Trace, null, msg, null);
    }

    /// <summary>
    /// 输出警示日志
    /// </summary>
    public static void LogWarning(this ILog logger, Exception ex, string msg)
    {
        logger.Log(TouchSocket.Core.LogLevel.Warning, null, msg, ex);
    }

    /// <summary>
    /// 输出警示日志
    /// </summary>
    public static void LogWarning(this ILog logger, Exception ex)
    {
        logger.Log(TouchSocket.Core.LogLevel.Warning, null, ex.Message, ex);
    }

    /// <summary>
    /// 输出警示日志
    /// </summary>
    public static void LogWarning(this ILog logger, string msg)
    {
        logger.Log(TouchSocket.Core.LogLevel.Warning, null, msg, null);
    }

    #endregion 日志
}
