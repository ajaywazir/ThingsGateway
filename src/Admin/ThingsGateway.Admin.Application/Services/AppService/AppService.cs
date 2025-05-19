﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

using System.Security.Claims;

namespace ThingsGateway.Admin.Application;

public class AppService : IAppService
{
    private readonly IUserAgentService UserAgentService;
    public AppService(IUserAgentService userAgentService)
    {
        UserAgentService = userAgentService;
    }
    public string GetReturnUrl(string returnUrl)
    {
        var url = QueryHelpers.AddQueryString(CookieAuthenticationDefaults.LoginPath, new Dictionary<string, string?>
        {
            ["ReturnUrl"] = returnUrl
        });
        return url;
    }

    public async Task LoginOutAsync()
    {
        try
        {
            await App.HttpContext!.SignOutAsync().ConfigureAwait(false);
            App.HttpContext!.SignoutToSwagger();
        }
        catch
        {
        }
    }
    public UserAgent? UserAgent
    {
        get
        {
            var str = App.HttpContext?.Request?.Headers?.UserAgent;
            if (!string.IsNullOrEmpty(str))
            {
                return UserAgentService.Parse(str);
            }
            return null;
        }
    }



    public async Task LoginAsync(ClaimsIdentity identity, int expire)
    {
        var diffTime = DateTime.Now + TimeSpan.FromMinutes(expire);
        //var diffTime = DateTime.Now.AddMinutes(expire);
        await App.HttpContext!.SignInAsync(ClaimConst.Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties()
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = diffTime,
        }).ConfigureAwait(false);
    }
    public ClaimsPrincipal? User => App.User;

    public string? RemoteIpAddress => App.HttpContext?.GetRemoteIpAddressToIPv4();

    public int LocalPort => App.HttpContext.Connection.LocalPort;
}
