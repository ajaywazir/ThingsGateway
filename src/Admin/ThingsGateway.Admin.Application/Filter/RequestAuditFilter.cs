//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Logging;

using ThingsGateway.FriendlyException;
using ThingsGateway.Logging;
using ThingsGateway.NewLife.Json.Extension;

namespace ThingsGateway.Admin.Application;

public class RequestAuditFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {

        var controllerActionDescriptor = (context.ActionDescriptor as ControllerActionDescriptor);
        // 获取动作方法描述器
        var actionMethod = controllerActionDescriptor?.MethodInfo;

        // 处理 Blazor Server
        if (actionMethod == null)
        {
            _ = await next.Invoke().ConfigureAwait(false);
            return;
        }

        // 排除 WebSocket 请求处理
        if (context.HttpContext.IsWebSocketRequest())
        {
            _ = await next().ConfigureAwait(false);
            return;
        }

        // 如果贴了 [SuppressMonitor] 特性则跳过
        if (actionMethod.IsDefined(typeof(SuppressRequestAuditAttribute), true)
            || actionMethod.DeclaringType.IsDefined(typeof(SuppressRequestAuditAttribute), true))
        {
            _ = await next().ConfigureAwait(false);
            return;
        }


        // 只有方法贴有特性才进行审计
        if (
            !actionMethod.DeclaringType.IsDefined(typeof(RequestAuditAttribute), true)
            &&
            !actionMethod.IsDefined(typeof(RequestAuditAttribute), true))
        {
            return;
        }

        // 计算接口执行时间
        var timeOperation = Stopwatch.StartNew();
        var resultContext = await next().ConfigureAwait(false);
        timeOperation.Stop();


        var logData = new RequestAuditData();


        logData.TimeOperationElapsedMilliseconds = timeOperation.ElapsedMilliseconds;

        var resultHttpContext = (resultContext as FilterContext).HttpContext;

        // 获取 HttpContext 和 HttpRequest 对象
        var httpContext = context.HttpContext;
        var httpRequest = httpContext.Request;

        // 获取客户端 Ipv4 地址
        var remoteIPv4 = httpContext.GetRemoteIpAddressToIPv4();
        logData.RemoteIPv4 = remoteIPv4;
        var requestUrl = Uri.UnescapeDataString(httpRequest.GetRequestUrlAddress());
        logData.RequestUrl = requestUrl;


        var result = resultContext.Result as IActionResult;
        var data = result switch
        {
            // 处理内容结果
            ContentResult content => content.Content,
            // 处理对象结果
            ObjectResult obj => obj.Value,
            // 处理 JSON 对象
            JsonResult json => json.Value,
            _ => null,
        };
        logData.ReturnInformation = data;



        //获取客户端信息
        var client = App.GetService<IAppService>().UserAgent;
        //操作名称默认是控制器名加方法名,自定义操作名称要在action上加Description特性
        var option = $"{controllerActionDescriptor.ControllerName}/{controllerActionDescriptor.ActionName}";

        var desc = App.CreateLocalizerByType(controllerActionDescriptor.ControllerTypeInfo.AsType())[actionMethod.Name];
        //获取特性


        logData.CateGory = desc.Value;//传操作名称
        logData.Operation = desc.Value;//传操作名称
        logData.Client = client;
        logData.Path = httpContext.Request.Path.Value;//请求地址
        logData.Method = httpContext.Request.Method;//请求方法

        logData.ControllerName = controllerActionDescriptor.ControllerName;
        logData.ActionName = controllerActionDescriptor.ActionName;

        logData.AuthorizationClaims = new();
        // 获取授权用户
        var user = httpContext.User;
        foreach (var claim in user.Claims)
        {
            logData.AuthorizationClaims.Add(new AuthorizationClaims
            {
                Type = claim.Type,
                Value = claim.Value,
            });
        }



        logData.LocalIPv4 = httpContext.GetLocalIpAddressToIPv4();
        logData.LogDateTime = DateTimeOffset.Now;
        var parameterValues = context.ActionArguments;

        logData.Parameters = new();
        var parameters = actionMethod.GetParameters();

        foreach (var parameter in parameters)
        {
            // 判断是否禁用记录特定参数
            if (parameter.IsDefined(typeof(SuppressRequestAuditAttribute), false)) continue;

            // 排除标记 [FromServices] 的解析
            if (parameter.IsDefined(typeof(FromServicesAttribute), false)) continue;

            var name = parameter.Name;
            var parameterType = parameter.ParameterType;

            _ = parameterValues.TryGetValue(name, out var value);


            var par = new Parameters()
            {
                Name = name,
            };
            logData.Parameters.Add(par);

            object rawValue = default;

            // 文件类型参数
            if (value is IFormFile || value is List<IFormFile>)
            {
                // 单文件
                if (value is IFormFile formFile)
                {
                    var fileSize = Math.Round(formFile.Length / 1024D);
                    rawValue = new
                    {
                        name = formFile.Name,
                        fileName = formFile.FileName,
                        length = formFile.Length,
                        contentType = formFile.ContentType
                    };
                }
                // 多文件
                else if (value is List<IFormFile> formFiles)
                {
                    var rawValues1 = new List<object>();
                    for (var i = 0; i < formFiles.Count; i++)
                    {
                        var file = formFiles[i];
                        var size = Math.Round(file.Length / 1024D);
                        var rawValue1 = new
                        {
                            name = file.Name,
                            fileName = file.FileName,
                            length = file.Length,
                            contentType = file.ContentType
                        };
                        rawValues1.Add(rawValue1);
                    }
                    rawValue = rawValues1;
                }
            }
            // 处理 byte[] 参数类型
            else if (value is byte[] byteArray)
            {
                rawValue = new
                {
                    length = byteArray.Length,
                };
            }
            // 处理基元类型，字符串类型和空值
            else if (parameterType.IsPrimitive || value is string || value == null)
            {
                rawValue = value;
            }
            // 其他类型统一进行序列化
            else
            {
                rawValue = value;
            }

            par.Value = rawValue;
        }


        // 获取异常对象情况
        Exception exception = resultContext.Exception;
        if (exception is AppFriendlyException friendlyException)
        {
            logData.Validation = new();
            logData.Validation.Message = friendlyException.Message;
        }
        else if (exception != null)
        {
            logData.Exception = new();
            logData.Exception.Message = exception.Message;
            logData.Exception.StackTrace = exception.StackTrace;
            logData.Exception.Type = HandleGenericType(exception.GetType());
        }



        // 创建日志记录器
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequestAudit>>();

        var logContext = new LogContext();

        logContext.Set(nameof(RequestAuditData), logData);

        // 设置日志上下文
        using var scope = logger.ScopeContext(logContext);

        if (exception == null)
        {
            logger.Log(LogLevel.Information, $"{logData.Method}:{logData.Path}-{logData.Operation}");
        }
        else
        {
            logger.Log(LogLevel.Warning, $"{logData.Method}:{logData.Path}-{logData.Operation}{Environment.NewLine}{logData.Exception.ToSystemTextJsonString()}");
        }
    }

    /// <summary>
    /// 处理泛型类型转字符串打印问题
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string HandleGenericType(Type type)
    {
        if (type == null) return string.Empty;

        var typeName = type.FullName ?? (!string.IsNullOrEmpty(type.Namespace) ? type.Namespace + "." : string.Empty) + type.Name;

        // 处理泛型类型问题
        if (type.IsConstructedGenericType)
        {
            var prefix = type.GetGenericArguments()
                .Select(genericArg => HandleGenericType(genericArg))
                .Aggregate((previous, current) => previous + ", " + current);

            typeName = typeName.Split('`').First() + "<" + prefix + ">";
        }

        return typeName;
    }




}