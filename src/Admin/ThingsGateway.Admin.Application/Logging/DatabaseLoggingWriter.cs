//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using SqlSugar;

using System.Collections.Concurrent;

using ThingsGateway.Extension;
using ThingsGateway.FriendlyException;
using ThingsGateway.Logging;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.Razor;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 数据库写入器
/// </summary>
public class DatabaseLoggingWriter : IDatabaseLoggingWriter
{
    /// <summary>
    /// 日志消息队列（线程安全）
    /// </summary>
    private readonly ConcurrentQueue<SysOperateLog> _operateLogMessageQueue = new();

    private SqlSugarClient SqlSugarClient;

    /// <summary>
    /// 此方法只会写入经由MVCFilter捕捉的方法日志，对于BlazorServer的内部操作，由<see cref="OperDescAttribute"/>执行
    /// </summary>
    /// <param name="logMsg"></param>
    /// <param name="flush"></param>
    public async Task WriteAsync(LogMessage logMsg, bool flush)
    {
        //转成实体
        var requestAuditData = logMsg.Context.Get(nameof(RequestAuditData)) as RequestAuditData;
        //日志时间赋值
        requestAuditData.LogDateTime = logMsg.LogDateTime;
        // requestAuditData.ReturnInformation.Value
        //验证失败不记录日志
        bool save = false;
        if (requestAuditData.Validation == null)
        {
            var operation = requestAuditData.Operation;//获取操作名称
            var client = requestAuditData.Client;//获取客户端信息
            var path = requestAuditData.Path;//获取操作名称
            var method = requestAuditData.Method;//获取方法
            //表示访问日志
            if (path == "/api/auth/login" || path == "/api/auth/logout")
            {
                //如果没有异常信息
                if (requestAuditData.Exception == null)
                {
                    save = await CreateVisitLog(operation, path, requestAuditData, client, flush).ConfigureAwait(false);//添加到访问日志
                }
                else
                {
                    //添加到异常日志
                    save = await CreateOperationLog(operation, path, requestAuditData, client, flush).ConfigureAwait(false);
                }
            }
            else
            {
                //只有定义了Title的POST方法才记录日志
                if (!operation.IsNullOrWhiteSpace() && method == "POST")
                {
                    //添加到操作日志
                    save = await CreateOperationLog(operation, path, requestAuditData, client, flush).ConfigureAwait(false);
                }
            }
        }
        if (save)
        {
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 创建操作日志
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="path">请求地址</param>
    /// <param name="requestAuditData">requestAuditData</param>
    /// <param name="userAgent">客户端信息</param>
    /// <param name="flush"></param>
    /// <returns></returns>
    private async Task<bool> CreateOperationLog(string operation, string path, RequestAuditData requestAuditData, UserAgent userAgent, bool flush)
    {
        //账号
        var opAccount = requestAuditData.AuthorizationClaims?.Where(it => it.Type == ClaimConst.Account).Select(it => it.Value).FirstOrDefault();

        //获取参数json字符串，
        var paramJson = requestAuditData.Parameters == null || requestAuditData.Parameters.Count == 0 ? null : requestAuditData.Parameters.ToSystemTextJsonString();

        //获取结果json字符串
        var resultJson = requestAuditData.ReturnInformation?.ToSystemTextJsonString();


        //操作日志表实体
        var sysLogOperate = new SysOperateLog
        {
            Name = operation,
            Category = LogCateGoryEnum.Operate,
            ExeStatus = true,
            OpIp = requestAuditData.RemoteIPv4,
            OpBrowser = userAgent?.Browser,
            OpOs = userAgent?.Platform,
            OpTime = requestAuditData.LogDateTime.LocalDateTime,
            OpAccount = opAccount,
            ReqMethod = requestAuditData.Method,
            ReqUrl = path,
            ResultJson = resultJson,
            ClassName = requestAuditData.ControllerName,
            MethodName = requestAuditData.ActionName,
            ParamJson = paramJson,
            VerificatId = UserManager.VerificatId,
        };
        //如果异常不为空
        if (requestAuditData.Exception != null)
        {
            sysLogOperate.Category = LogCateGoryEnum.Exception;//操作类型为异常
            sysLogOperate.ExeStatus = false;//操作状态为失败

            if (requestAuditData.Exception.Type == typeof(AppFriendlyException).ToString())
                sysLogOperate.ExeMessage = requestAuditData?.Exception.Message;
            else
                sysLogOperate.ExeMessage = $"{requestAuditData.Exception.Type}:{requestAuditData.Exception.Message}{Environment.NewLine}{requestAuditData.Exception.StackTrace}";
        }

        _operateLogMessageQueue.Enqueue(sysLogOperate);

        if (flush)
        {
            SqlSugarClient ??= DbContext.Db.GetConnectionScopeWithAttr<SysOperateLog>().CopyNew();
            await SqlSugarClient.InsertableWithAttr(_operateLogMessageQueue.ToListWithDequeue()).ExecuteCommandAsync().ConfigureAwait(false);//入库
            return true;
        }
        return false;
    }

    /// <summary>
    /// 创建访问日志
    /// </summary>
    /// <param name="operation">访问类型</param>
    /// <param name="path"></param>
    /// <param name="requestAuditData">requestAuditData</param>
    /// <param name="userAgent">客户端信息</param>
    /// <param name="flush"></param>
    private async Task<bool> CreateVisitLog(string operation, string path, RequestAuditData requestAuditData, UserAgent userAgent, bool flush)
    {
        long verificatId = 0;//验证Id
        var opAccount = "";//用户账号
        if (path == "/api/auth/login")
        {
            //如果是登录，用户信息就从返回值里拿
            var result = requestAuditData.ReturnInformation?.ToSystemTextJsonString();//返回值转json
            var userInfo = result.FromJsonNetString<UnifyResult<LoginOutput>>();//格式化成user表
            opAccount = userInfo.Data.Account;//赋值账号
            verificatId = userInfo.Data.VerificatId;
        }
        else
        {
            //如果是登录出，用户信息就从AuthorizationClaims里拿
            opAccount = requestAuditData.AuthorizationClaims.Where(it => it.Type == ClaimConst.Account).Select(it => it.Value).FirstOrDefault();
            verificatId = requestAuditData.AuthorizationClaims.Where(it => it.Type == ClaimConst.VerificatId).Select(it => it.Value).FirstOrDefault().ToLong();
        }
        //日志表实体
        var sysLogVisit = new SysOperateLog
        {
            Name = operation,
            Category = path == "/api/auth/login" ? LogCateGoryEnum.Login : LogCateGoryEnum.Logout,
            ExeStatus = true,
            OpIp = requestAuditData.RemoteIPv4,
            OpBrowser = userAgent?.Browser,
            OpOs = userAgent?.Platform,
            OpTime = requestAuditData.LogDateTime.LocalDateTime,
            VerificatId = verificatId,
            OpAccount = opAccount,

            ReqMethod = requestAuditData.Method,
            ReqUrl = path,
            ResultJson = requestAuditData.ReturnInformation?.ToSystemTextJsonString(),
            ClassName = requestAuditData.ControllerName,
            MethodName = requestAuditData.ActionName,
            ParamJson = requestAuditData.Parameters?.ToSystemTextJsonString(),
        };
        _operateLogMessageQueue.Enqueue(sysLogVisit);

        if (flush)
        {
            SqlSugarClient ??= DbContext.Db.GetConnectionScopeWithAttr<SysOperateLog>().CopyNew();
            await SqlSugarClient.InsertableWithAttr(_operateLogMessageQueue.ToListWithDequeue()).ExecuteCommandAsync().ConfigureAwait(false);//入库
            return true;
        }
        return false;
    }
}
