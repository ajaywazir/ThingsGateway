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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ThingsGateway.Admin.Application;

[ApiDescriptionSettings(false)]
[Route("api/auth")]
[RequestAudit]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginOutput> LoginAsync([FromBody] LoginInput input)
    {

        return _authService.LoginAsync(input);

    }

    [HttpGet("oauth-login")]
    [AllowAnonymous]
    public IActionResult OAuthLogin(string scheme = "Gitee", string returnUrl = "/")
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };
        return Challenge(props, scheme);
    }


    [HttpPost("logout")]
    [Authorize]
    [IgnoreRolePermission]
    public Task LogoutAsync()
    {
        return _authService.LoginOutAsync();
    }
}
