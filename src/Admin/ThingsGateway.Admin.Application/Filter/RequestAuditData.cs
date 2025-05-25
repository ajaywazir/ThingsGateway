//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

namespace ThingsGateway.Admin.Application;

public class RequestAuditData
{
    /// <summary>
    /// 分类
    /// </summary>
    public string CateGory { get; set; }

    /// <summary>
    /// 客户端信息
    /// </summary>
    public UserAgent Client { get; set; }

    /// <summary>
    /// 请求方法：POST/GET
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// 操作名称
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 方法名称
    /// </summary>
    public string ActionName { get; set; }

    /// <summary>
    /// 认证信息
    /// </summary>
    public List<AuthorizationClaims> AuthorizationClaims { get; set; }

    /// <summary>
    /// 控制器名
    /// </summary>
    public string ControllerName { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public LogException Exception { get; set; }

    public long TimeOperationElapsedMilliseconds { get; set; }


    /// <summary>
    /// 服务端
    /// </summary>
    public string LocalIPv4 { get; set; }

    /// <summary>
    /// 日志时间
    /// </summary>
    public DateTimeOffset LogDateTime { get; set; }

    /// <summary>
    /// 参数列表
    /// </summary>
    public List<Parameters> Parameters { get; set; }

    /// <summary>
    /// 客户端IPV4地址
    /// </summary>
    public string RemoteIPv4 { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string RequestUrl { get; set; }

    /// <summary>
    /// 返回信息
    /// </summary>
    public object ReturnInformation { get; set; }

    /// <summary>
    /// 验证错误信息
    /// </summary>
    public Validation Validation { get; set; }
}

