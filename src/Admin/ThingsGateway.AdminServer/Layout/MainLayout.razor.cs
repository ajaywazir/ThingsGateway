﻿//------------------------------------------------------------------------------
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

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using System.Diagnostics.CodeAnalysis;

using ThingsGateway.Admin.Application;
using ThingsGateway.Admin.Razor;
using ThingsGateway.Razor;

namespace ThingsGateway.AdminServer;

public partial class MainLayout : IDisposable
{
    [Inject]
    IStringLocalizer<ThingsGateway.Razor._Imports> RazorLocalizer { get; set; }
    private Task OnRefresh(ContextMenuItem item, object? context)
    {
        if (context is TabItem tabItem)
        {
            _tab.Refresh(tabItem);
        }
        return Task.CompletedTask;
    }

    private async Task OnClose(ContextMenuItem item, object? context)
    {
        if (context is TabItem tabItem)
        {
            await _tab.RemoveTab(tabItem);
        }
    }

    private Task OnCloseOther(ContextMenuItem item, object? context)
    {
        if (context is TabItem tabItem)
        {
            _tab.ActiveTab(tabItem);
        }
        _tab.CloseOtherTabs();
        return Task.CompletedTask;
    }

    private Task OnCloseAll(ContextMenuItem item, object? context)
    {
        _tab.CloseAllTabs();
        return Task.CompletedTask;
    }

    #region 全局通知

    [Inject]
    [NotNull]
    private IDispatchService<MessageItem>? DispatchService { get; set; }

    private async Task Dispatch(DispatchEntry<MessageItem> entry)
    {
        if (entry.Entry != null)
        {
            await InvokeAsync(async () =>
            {
                await ToastService.Show(new ToastOption()
                {
                    Title = $"{entry.Entry.Title} ",
                    Content = $"{entry.Entry.Message}",
                    Category = entry.Entry.Category,
                    Delay = 10 * 1000,
                    ForceDelay = true
                });
            });
        }
    }

    #endregion 全局通知

    #region 切换模块

    [Inject]
    private ISysResourceService SysResourceService { get; set; }

    [Inject]
    private IUserCenterService UserCenterService { get; set; }

    private Task ChoiceModule(long moduleId)
    {
        return ReloadMenu(moduleId);
    }

    #endregion 切换模块

    #region 个人信息修改

    private Task OnUserInfoDialog()
    {
        return DialogService.Show(new DialogOption()
        {
            IsScrolling = false,
            Title = Localizer["UserCenter"],
            ShowFooter = false,
            Component = BootstrapDynamicComponent.CreateComponent<UserCenterPage>(new Dictionary<string, object?>()
            {
            })
        });
    }

    #endregion 个人信息修改

    #region 注销

    [Inject]
    private AjaxService AjaxService { get; set; }
    [Inject]
    private IAppService AppService { get; set; }
    [Inject]
    [NotNull]
    private IAuthRazorService? AuthRazorService { get; set; }

    private async Task LogoutAsync()
    {

        try
        {

            var ret = await AuthRazorService.LoginOutAsync();
            if (ret.Code != 200)
            {
                await ToastService.Error(Localizer["LoginErrorh1"], $"{ret.Msg}");
            }
            else
            {
                await ToastService.Information(Localizer["LoginSuccessh1"], Localizer["LoginSuccessc1"]);
                await Task.Delay(1000);
                var url = AppService.GetReturnUrl(NavigationManager.ToBaseRelativePath(NavigationManager.Uri));
                await AjaxService.Goto(url);
            }
        }
        catch
        {
            await ToastService.Error(Localizer["LoginErrorh2"], Localizer["LoginErrorc2"]);
        }
    }

    #endregion 注销

    private string _versionString = string.Empty;
    [Inject]
    [NotNull]
    private BlazorAppContext? AppContext { get; set; }
    [Inject]
    private IStringLocalizer<ThingsGateway.Admin.Razor._Imports> AdminLocalizer { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    [NotNull]
    private FullScreenService FullScreenService { get; set; }

    [Inject]
    [NotNull]
    private IStringLocalizer<MainLayout>? Localizer { get; set; }

    [Inject]
    [NotNull]
    private IMenuService? MenuService { get; set; }

    [Inject]
    [NotNull]
    private ToastService? ToastService { get; set; }

    [Inject]
    [NotNull]
    private IAppVersionService? VersionService { get; set; }

    [Inject]
    [NotNull]
    private IOptions<WebsiteOptions>? WebsiteOption { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        _versionString = $"v{VersionService.Version}";
        DispatchService.Subscribe(Dispatch);
        await AppContext.InitUserAsync();
        await AppContext.InitMenus(NavigationManager.ToBaseRelativePath(NavigationManager.Uri));
        await base.OnInitializedAsync();
    }
    private Tab _tab { get; set; }

    [Inject]
    IServiceProvider ServiceProvider { get; set; }
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            DispatchService.UnSubscribe(Dispatch);
        }
    }

    private async Task ReloadMenu(long? moduleId = null)
    {
        await AppContext.InitMenus(NavigationManager.ToBaseRelativePath(NavigationManager.Uri), moduleId);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadUser()
    {
        await AppContext.InitUserAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowAbout()
    {
        DialogOption? op = null;

        op = new DialogOption()
        {
            IsScrolling = false,
            Size = Size.Medium,
            ShowFooter = false,
            Title = Localizer["About"],
            BodyTemplate = BootstrapDynamicComponent.CreateComponent<About>().Render(),
        };
        await DialogService.Show(op);
    }
}
