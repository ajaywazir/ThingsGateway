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

using System.IO.Compression;
using System.Text;

namespace ThingsGateway.DataEncryption;

/// <summary>
/// GZip 压缩解压
/// </summary>
[SuppressSniffer]
public static class GzipEncryption
{
    /// <summary>
    /// 压缩字符串并返回字节数组
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static byte[] Compress(string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text);

        using var ms = new MemoryStream();
        using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
        {
            zip.Write(buffer, 0, buffer.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 从字节数组解压
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string Decompress(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var zip = new GZipStream(ms, CompressionMode.Decompress);
        using var outStream = new MemoryStream();

        zip.CopyTo(outStream);

        return Encoding.UTF8.GetString(outStream.ToArray());
    }

    /// <summary>
    /// 压缩字符串并返回 Base64 字符串
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string CompressToBase64(string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text);

        using var ms = new MemoryStream();
        using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
        {
            zip.Write(buffer, 0, buffer.Length);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>
    /// 从 Base64 字符串解压
    /// </summary>
    /// <param name="base64String"></param>
    /// <returns></returns>
    public static string DecompressFromBase64(string base64String)
    {
        var compressedData = Convert.FromBase64String(base64String);

        using var ms = new MemoryStream(compressedData);
        using var zip = new GZipStream(ms, CompressionMode.Decompress);
        using var outStream = new MemoryStream();

        zip.CopyTo(outStream);

        return Encoding.UTF8.GetString(outStream.ToArray());
    }
}
