
//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

#pragma warning disable CA2007 // 考虑对等待的任务调用 ConfigureAwait



using BootstrapBlazor.Components;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using ThingsGateway.Admin.Application;
using ThingsGateway.Admin.Razor;
using ThingsGateway.Extension;
using ThingsGateway.Gateway.Application;

namespace ThingsGateway.Server;


[Authorize]
[IgnoreRolePermission]
[Route("/")]
[Route("/index")]
[TabItemOption(Text = "Home", Icon = "fas fa-house")]
public partial class GatewayIndex
{
    [Inject]
    private BlazorAppContext AppContext { get; set; }

    [Inject]
    private ISysOperateLogService SysOperateLogService { get; set; }

    [Inject]
    private IBackendLogService BackendLogService { get; set; }

    [Inject]
    private IRpcLogService RpcLogService { get; set; }

    [Inject]
    private IStringLocalizer<GatewayIndex> Localizer { get; set; }

    private IEnumerable<TimelineItem>? SysOperateLogItems { get; set; }
    private IEnumerable<TimelineItem>? BackendLogItems { get; set; }
    private IEnumerable<TimelineItem>? RpcLogItems { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var data = await SysOperateLogService.GetNewLog(AppContext.CurrentUser.Account);
        SysOperateLogItems = data.Select(a =>
        {
            return new TimelineItem()
            {
                Content = $"{a.Name}  [IP]  {a.OpIp} [Browser] {a.OpBrowser}",

                Description = a.OpTime.ToDefaultDateTimeFormat()
            };
        });

        var data1 = await BackendLogService.GetNewLog();
        BackendLogItems = data1.Select(a =>
        {
            return new TimelineItem()
            {
                Content = $"{a.LogLevel}  [Msg]  {a.LogMessage}",

                Description = a.LogTime.ToDefaultDateTimeFormat()
            };
        });

        var data2 = await RpcLogService.GetNewLog();
        RpcLogItems = data2.Select(a =>
        {
            return new TimelineItem()
            {
                Content = $"{a.OperateDevice} : {a.OperateObject}  [Source]  {a.OperateSource} ",

                Description = a.LogTime.ToDefaultDateTimeFormat()
            };
        });

        await base.OnParametersSetAsync();
    }
}

