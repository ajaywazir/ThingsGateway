using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ThingsGateway.Admin.Application;

/// <summary>
/// 只适合 Demo 登录，会直接授权超管的权限
/// </summary>
public class AdminOAuthHandler<TOptions>(
   IVerificatInfoService verificatInfoService,
   IAppService appService,
   ISysUserService sysUserService,
   ISysDictService configService,
    IOptionsMonitor<TOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : OAuthHandler<TOptions>(options, logger, encoder)
    where TOptions : AdminOAuthOptions, new()
{
    private async Task<LoginEvent> GetLogin()
    {
        var sysUser = await sysUserService.GetUserByIdAsync(RoleConst.SuperAdminId).ConfigureAwait(false);//获取用户信息

        var appConfig = await configService.GetAppConfigAsync().ConfigureAwait(false);


        var expire = appConfig.LoginPolicy.VerificatExpireTime;

        var loginEvent = new LoginEvent
        {
            Ip = appService.RemoteIpAddress,
            Device = appService.UserAgent?.Platform,
            Expire = expire,
            SysUser = sysUser,
            VerificatId = CommonUtils.GetSingleId()
        };

        //获取verificat列表
        var tokenTimeout = loginEvent.DateTime.AddMinutes(loginEvent.Expire);
        //生成verificat信息
        var verificatInfo = new VerificatInfo
        {
            Device = loginEvent.Device,
            Expire = loginEvent.Expire,
            VerificatTimeout = tokenTimeout,
            Id = loginEvent.VerificatId,
            UserId = loginEvent.SysUser.Id,
            LoginIp = loginEvent.Ip,
            LoginTime = loginEvent.DateTime
        };


        //添加到verificat列表
        verificatInfoService.Add(verificatInfo);

        return loginEvent;
    }
    /// <summary>
    /// 登录事件
    /// </summary>
    /// <param name="loginEvent"></param>
    /// <returns></returns>
    private async Task UpdateUser(LoginEvent loginEvent)
    {
        var sysUser = loginEvent.SysUser;

        #region 登录/密码策略

        var key = CacheConst.Cache_LoginErrorCount + sysUser.Account;//获取登录错误次数Key值
        App.CacheService.Remove(key);//移除登录错误次数

        //获取用户verificat列表
        var userToken = verificatInfoService.GetOne(loginEvent.VerificatId);

        #endregion 登录/密码策略

        #region 重新赋值属性,设置本次登录信息为最新的信息

        sysUser.LastLoginIp = sysUser.LatestLoginIp;
        sysUser.LastLoginTime = sysUser.LatestLoginTime;
        sysUser.LatestLoginIp = loginEvent.Ip;
        sysUser.LatestLoginTime = loginEvent.DateTime;

        #endregion 重新赋值属性,设置本次登录信息为最新的信息

        using var db = DbContext.Db.GetConnectionScopeWithAttr<SysUser>().CopyNew();
        //更新用户登录信息
        if (await db.Updateable(sysUser).UpdateColumns(it => new
        {
            it.LastLoginIp,
            it.LastLoginTime,
            it.LatestLoginIp,
            it.LatestLoginTime,
        }).ExecuteCommandAsync().ConfigureAwait(false) > 0)
            App.CacheService.HashAdd(CacheConst.Cache_SysUser, sysUser.Id.ToString(), sysUser);//更新Cache信息
    }

    protected override async Task<AuthenticationTicket> CreateTicketAsync(
        ClaimsIdentity identity,
        AuthenticationProperties properties,
        OAuthTokenResponse tokens)
    {
        properties.RedirectUri = Options.HomePath;
        properties.IsPersistent = true;

        if (!string.IsNullOrEmpty(tokens.ExpiresIn) && int.TryParse(tokens.ExpiresIn, out var result))
        {
            properties.ExpiresUtc = TimeProvider.System.GetUtcNow().AddSeconds(result);
        }
        var user = await HandleUserInfoAsync(tokens).ConfigureAwait(false);

        var sysUser = await GetLogin().ConfigureAwait(false);
        await UpdateUser(sysUser).ConfigureAwait(false);
        identity.AddClaim(new Claim(ClaimConst.VerificatId, sysUser.VerificatId.ToString()));
        identity.AddClaim(new Claim(ClaimConst.UserId, RoleConst.SuperAdminId.ToString()));

        identity.AddClaim(new Claim(ClaimConst.SuperAdmin, "true"));
        identity.AddClaim(new Claim(ClaimConst.OrgId, RoleConst.DefaultTenantId.ToString()));
        identity.AddClaim(new Claim(ClaimConst.TenantId, RoleConst.DefaultTenantId.ToString()));


        var context = new OAuthCreatingTicketContext(
            new ClaimsPrincipal(identity),
            properties,
            Context,
            Scheme,
            Options,
            Backchannel,
            tokens,
            user
        );

        context.RunClaimActions();
        await Events.CreatingTicket(context).ConfigureAwait(false);

        return new AuthenticationTicket(context.Principal, context.Properties, Scheme.Name);
    }

    /// <summary>刷新 Token 方法</summary>
    protected virtual async Task<OAuthTokenResponse> RefreshTokenAsync(OAuthTokenResponse oAuthToken)
    {
        var query = new Dictionary<string, string>
        {
            { "refresh_token", oAuthToken.RefreshToken },
            { "grant_type", "refresh_token" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, QueryHelpers.AddQueryString(Options.TokenEndpoint, query));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await Backchannel.SendAsync(request, Context.RequestAborted).ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return OAuthTokenResponse.Success(JsonDocument.Parse(content));
        }

        return OAuthTokenResponse.Failed(new OAuthTokenException($"OAuth token endpoint failure: {await Display(response).ConfigureAwait(false)}"));
    }

    /// <summary>处理用户信息方法</summary>
    protected virtual async Task<JsonElement> HandleUserInfoAsync(OAuthTokenResponse tokens)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUserInfoUrl(tokens));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await Backchannel.SendAsync(request, Context.RequestAborted).ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return JsonDocument.Parse(content).RootElement;
        }

        throw new OAuthTokenException($"OAuth user info endpoint failure: {await Display(response).ConfigureAwait(false)}");
    }

    /// <summary>生成用户信息请求地址方法</summary>
    protected virtual string BuildUserInfoUrl(OAuthTokenResponse tokens)
    {
        return QueryHelpers.AddQueryString(Options.UserInformationEndpoint, new Dictionary<string, string>
        {
            { "access_token", tokens.AccessToken }
        });
    }

    /// <summary>生成错误信息方法</summary>
    protected static async Task<string> Display(HttpResponseMessage response)
    {
        var output = new StringBuilder();
        output.Append($"Status: {response.StatusCode}; ");
        output.Append($"Headers: {response.Headers}; ");
        output.Append($"Body: {await response.Content.ReadAsStringAsync().ConfigureAwait(false)};");

        return output.ToString();
    }
}

/// <summary>自定义 Token 异常</summary>
public class OAuthTokenException(string message) : Exception(message);
