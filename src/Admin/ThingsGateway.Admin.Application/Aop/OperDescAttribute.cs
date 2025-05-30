﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Rougamo;
using Rougamo.Context;
using Rougamo.Metadatas;

using System.Collections.Concurrent;

using ThingsGateway.Extension;
using ThingsGateway.FriendlyException;
using ThingsGateway.NewLife.Json.Extension;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// Aop拦截器
/// </summary>
[Pointcut(AccessFlags.Public | AccessFlags.Method)]
[Advice(Feature.OnException | Feature.OnSuccess)]
public sealed class OperDescAttribute : MoAttribute
{
    /// <summary>
    /// 日志消息队列（线程安全）
    /// </summary>
    private static readonly ConcurrentQueue<SysOperateLog> _logMessageQueue = new();
    private static readonly IAppService AppService;

    static OperDescAttribute()
    {
        // 创建长时间运行的后台任务，并将日志消息队列中数据写入存储中
        Task.Factory.StartNew(ProcessQueue, TaskCreationOptions.LongRunning);
        AppService = App.RootServices.GetService<IAppService>();
    }

    public OperDescAttribute(string description, bool isRecordPar = true, object localizerType = null)
    {
        Description = description;
        IsRecordPar = isRecordPar;
        LocalizerType = (Type)localizerType;
    }

    /// <summary>
    /// 说明，需配置本地化json文件
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 是否记录进出参数
    /// </summary>
    public bool IsRecordPar { get; }

    public Type? LocalizerType { get; }

    public override void OnException(MethodContext context)
    {
        if (App.HttpContext.Request.Path.StartsWithSegments("/_blazor"))
        {
            //插入异常日志
            SysOperateLog log = GetOperLog(LocalizerType, context);

            log.Category = LogCateGoryEnum.Exception;//操作类型为异常
            log.ExeStatus = false;//操作状态为失败
            if (context.Exception is AppFriendlyException exception)
                log.ExeMessage = exception?.Message;
            else
                log.ExeMessage = context.Exception?.ToString();

            OperDescAttribute.WriteToQueue(log);
        }
    }

    public override void OnSuccess(MethodContext context)
    {
        if (App.HttpContext.Request.Path.StartsWithSegments("/_blazor"))
        {

            //插入操作日志
            SysOperateLog log = GetOperLog(LocalizerType, context);
            OperDescAttribute.WriteToQueue(log);
        }
    }

    /// <summary>
    /// 将日志消息写入数据库中
    /// </summary>
    private static async Task ProcessQueue()
    {
        var db = DbContext.Db.GetConnectionScopeWithAttr<SysOperateLog>().CopyNew();
        var appLifetime = App.RootServices!.GetService<IHostApplicationLifetime>()!;
        while (!appLifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                var data = _logMessageQueue.ToListWithDequeue(); // 从日志队列中获取数据
                if (data.Count > 0)
                {
                    await db.InsertableWithAttr(data).ExecuteCommandAsync(appLifetime.ApplicationStopping).ConfigureAwait(false);//入库
                }
            }
            catch (Exception ex)
            {
                NewLife.Log.XTrace.WriteException(ex);
            }
            finally
            {
                await Task.Delay(3000, appLifetime.ApplicationStopping).ConfigureAwait(false);
            }
        }
    }

    private SysOperateLog GetOperLog(Type? localizerType, MethodContext context)
    {
        var methodBase = context.Method;
        var userAgent = AppService.UserAgent;
        string? paramJson = null;
        if (IsRecordPar)
        {
            var args = context.Arguments;
            var parametersInfo = methodBase.GetParameters();
            var parametersDict = new Dictionary<string, object>();

            for (int i = 0; i < parametersInfo.Length; i++)
            {
                parametersDict[parametersInfo[i].Name!] = args[i];
            }
            paramJson = parametersDict.ToSystemTextJsonString();
        }
        var result = context.ReturnValue;
        var resultJson = IsRecordPar ? result?.ToSystemTextJsonString() : null;
        //操作日志表实体
        var log = new SysOperateLog
        {
            Name = (localizerType == null ? App.CreateLocalizerByType(typeof(OperDescAttribute)) : App.CreateLocalizerByType(localizerType))![Description],
            Category = LogCateGoryEnum.Operate,
            ExeStatus = true,
            OpIp = AppService?.RemoteIpAddress ?? string.Empty,
            OpBrowser = userAgent?.Browser,
            OpOs = userAgent?.Platform,
            OpTime = DateTime.Now,
            OpAccount = UserManager.UserAccount,
            ReqUrl = null,
            ReqMethod = "browser",
            ResultJson = resultJson,
            ClassName = methodBase.ReflectedType!.Name,
            MethodName = methodBase.Name,
            ParamJson = paramJson,
            VerificatId = UserManager.VerificatId,
        };
        return log;
    }

    /// <summary>
    /// 将日志消息写入队列中等待后台任务出队写入数据库
    /// </summary>
    /// <param name="logMsg">结构化日志消息</param>
    private static void WriteToQueue(SysOperateLog logMsg)
    {
        _logMessageQueue.Enqueue(logMsg);
    }
}
