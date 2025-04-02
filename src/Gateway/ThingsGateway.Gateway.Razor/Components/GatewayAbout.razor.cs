//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

using ThingsGateway.Authentication;

namespace ThingsGateway.Gateway.Razor;

/// <inheritdoc/>
public partial class GatewayAbout
{
    [Inject]
    [NotNull]
    private IStringLocalizer<GatewayAbout>? Localizer { get; set; }
    [Inject]
    [NotNull]
    private IStringLocalizer<About>? AboutLocalizer { get; set; }
    [Inject]
    [NotNull]
    private IOptions<WebsiteOptions>? WebsiteOption { get; set; }

    private string Password { get; set; }
    private AuthorizeInfo AuthorizeInfo { get; set; }
    [Inject]
    ToastService ToastService { get; set; }

    protected override void OnParametersSet()
    {
        ProAuthentication.TryGetAuthorizeInfo(out var authorizeInfo);
        AuthorizeInfo = authorizeInfo;
        base.OnParametersSet();
    }

    private async Task Register()
    {
        var result = ProAuthentication.TryAuthorize(Password, out var authorizeInfo);
        if (result)
        {
            AuthorizeInfo = authorizeInfo;
            await ToastService.Default();
        }
        else
            await ToastService.Default(false);

        Password = string.Empty;
        await InvokeAsync(StateHasChanged);
    }
    private async Task Unregister()
    {
        ProAuthentication.UnAuthorize();
        var result = ProAuthentication.TryGetAuthorizeInfo(out var authorizeInfo);
        AuthorizeInfo = authorizeInfo;

        await InvokeAsync(StateHasChanged);
    }

}
