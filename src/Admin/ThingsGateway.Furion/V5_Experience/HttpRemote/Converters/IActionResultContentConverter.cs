﻿// ------------------------------------------------------------------------
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

using System.Net;
using System.Net.Mime;

namespace ThingsGateway.HttpRemote;

/// <summary>
///     <see cref="IActionResult" /> 内容转换器
/// </summary>
public class IActionResultContentConverter : HttpContentConverterBase<IActionResult>
{
    /// <inheritdoc />
    public override IActionResult? Read(HttpResponseMessage httpResponseMessage,
        CancellationToken cancellationToken = default)
    {
        // 处理特定状态码结果
        if (TryGetStatusCodeResult(httpResponseMessage, out var statusCode, out var statusCodeResult))
        {
            return statusCodeResult;
        }

        // 获取响应内容标头
        var contentHeaders = httpResponseMessage.Content.Headers;

        // 获取内容类型
        var contentType = contentHeaders.ContentType;
        var mediaType = contentType?.MediaType;

        // 空检查
        ArgumentNullException.ThrowIfNull(mediaType);

        switch (mediaType)
        {
            case MediaTypeNames.Application.Json:
            case MediaTypeNames.Application.JsonPatch:
            case MediaTypeNames.Application.Xml:
            case MediaTypeNames.Application.XmlPatch:
            case MediaTypeNames.Text.Xml:
            case MediaTypeNames.Text.Html:
            case MediaTypeNames.Text.Plain:
                // 读取字符串内容
                var stringContent = httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).GetAwaiter()
                    .GetResult();

                return new ContentResult
                {
                    Content = stringContent,
                    StatusCode = (int)statusCode,
                    ContentType = contentType?.ToString()
                };
            default:
                // 获取 ContentDisposition 实例
                var contentDisposition = contentHeaders.ContentDisposition;

                // 获取文件下载名
                var fileDownloadName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;

                // 读取流内容
                var streamContent = httpResponseMessage.Content.ReadAsStream(cancellationToken);

                return new FileStreamResult(streamContent, contentType!.ToString())
                {
                    FileDownloadName =
                        string.IsNullOrWhiteSpace(fileDownloadName)
                            ? fileDownloadName
                            // 使用 UTF-8 解码文件的名称
                            : Uri.UnescapeDataString(fileDownloadName),
                    LastModified = contentHeaders.LastModified?.UtcDateTime
                };
        }
    }

    /// <inheritdoc />
    public override async Task<IActionResult?> ReadAsync(HttpResponseMessage httpResponseMessage,
        CancellationToken cancellationToken = default)
    {
        // 处理特定状态码结果
        if (TryGetStatusCodeResult(httpResponseMessage, out var statusCode, out var statusCodeResult))
        {
            return statusCodeResult;
        }

        // 获取响应内容标头
        var contentHeaders = httpResponseMessage.Content.Headers;

        // 获取内容类型
        var contentType = contentHeaders.ContentType;
        var mediaType = contentType?.MediaType;

        // 空检查
        ArgumentNullException.ThrowIfNull(mediaType);

        switch (mediaType)
        {
            case MediaTypeNames.Application.Json:
            case MediaTypeNames.Application.JsonPatch:
            case MediaTypeNames.Application.Xml:
            case MediaTypeNames.Application.XmlPatch:
            case MediaTypeNames.Text.Xml:
            case MediaTypeNames.Text.Html:
            case MediaTypeNames.Text.Plain:
                // 读取字符串内容
                var stringContent = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                return new ContentResult
                {
                    Content = stringContent,
                    StatusCode = (int)statusCode,
                    ContentType = contentType?.ToString()
                };
            default:
                // 获取 ContentDisposition 实例
                var contentDisposition = contentHeaders.ContentDisposition;

                // 获取文件下载名
                var fileDownloadName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;

                // 读取流内容
                var streamContent = await httpResponseMessage.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                return new FileStreamResult(streamContent, contentType!.ToString())
                {
                    FileDownloadName =
                        string.IsNullOrWhiteSpace(fileDownloadName)
                            ? fileDownloadName
                            // 使用 UTF-8 解码文件的名称
                            : Uri.UnescapeDataString(fileDownloadName),
                    LastModified = contentHeaders.LastModified?.UtcDateTime
                };
        }
    }

    /// <summary>
    ///     处理特定状态码结果
    /// </summary>
    /// <param name="httpResponseMessage">
    ///     <see cref="HttpResponseMessage" />
    /// </param>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <param name="statusCodeResult">
    ///     <see cref="IActionResult" />
    /// </param>
    /// <returns>
    ///     <see cref="bool" />
    /// </returns>
    internal static bool TryGetStatusCodeResult(HttpResponseMessage httpResponseMessage, out HttpStatusCode statusCode,
        out IActionResult? statusCodeResult)
    {
        // 获取状态码
        statusCode = httpResponseMessage.StatusCode;

        statusCodeResult = statusCode switch
        {
            HttpStatusCode.NoContent => new NoContentResult(),
            HttpStatusCode.BadRequest => new BadRequestResult(),
            HttpStatusCode.Unauthorized => new UnauthorizedResult(),
            HttpStatusCode.NotFound => new NotFoundResult(),
            HttpStatusCode.Conflict => new ConflictResult(),
            HttpStatusCode.UnsupportedMediaType => new UnsupportedMediaTypeResult(),
            HttpStatusCode.UnprocessableEntity => new UnprocessableEntityResult(),
            _ => null
        };

        return statusCodeResult is not null;
    }
}