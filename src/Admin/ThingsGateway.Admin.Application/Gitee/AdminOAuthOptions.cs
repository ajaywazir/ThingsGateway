using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;

namespace ThingsGateway.Admin.Application;

/// <summary>OAuthOptions 配置类</summary>
public abstract class AdminOAuthOptions : OAuthOptions
{
    /// <summary>默认构造函数</summary>
    protected AdminOAuthOptions()
    {
        ConfigureClaims();
        this.Events.OnRemoteFailure = context =>
        {
            var redirectUri = string.IsNullOrEmpty(HomePath) ? "/" : HomePath;
            context.Response.Redirect(redirectUri);
            context.HandleResponse();
            return Task.CompletedTask;
        };


    }

    /// <summary>配置 Claims 映射</summary>
    protected virtual void ConfigureClaims()
    {

    }

    /// <summary>获得/设置 登陆后首页</summary>
    public string HomePath { get; set; } = "/";

}
