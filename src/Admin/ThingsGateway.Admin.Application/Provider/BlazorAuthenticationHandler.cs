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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

using System.Security.Claims;

using ThingsGateway.Authorization;
using ThingsGateway.DataEncryption;

namespace ThingsGateway.Admin.Application;

/// <inheritdoc/>
public class BlazorAuthenticationHandler : AppAuthorizeHandler
{
    private readonly ISysDictService _sysDictService;
    private readonly ISysRoleService _sysRoleService;
    private readonly ISysUserService _sysUserService;

    public BlazorAuthenticationHandler(ISysUserService sysUserService, ISysRoleService sysRoleService, ISysDictService sysDictService)
    {
        _sysUserService = sysUserService;
        _sysRoleService = sysRoleService;
        _sysDictService = sysDictService;
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(AuthorizationHandlerContext context, DefaultHttpContext httpContext)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated;
        if (isAuthenticated == true)
        {
            if (await CheckVerificatFromCacheAsync(context).ConfigureAwait(false))
            {
                await AuthorizeHandleAsync(context).ConfigureAwait(false);
            }
            else
            {
                await Fail(context).ConfigureAwait(false);
            }
        }
        else
        {
            await Fail(context).ConfigureAwait(false);// 授权失败
        }

    }
    static async Task Fail(AuthorizationHandlerContext context)
    {
        var verificatId = UserManager.VerificatId;
        var verificatInfo = App.GetService<IVerificatInfoService>().GetOne(verificatId, false);
        if (App.HttpContext != null)
        {
            var identity = new ClaimsIdentity();
            App.HttpContext.User = new ClaimsPrincipal(identity);
        }
        context.Fail(); // 授权失败

        if (verificatInfo != null)
            await App.GetService<INoticeService>().UserLoginOut(verificatInfo.ClientIds, App.CreateLocalizerByType(typeof(BlazorAuthenticationHandler))["UserExpire"]).ConfigureAwait(false);

        DefaultHttpContext currentHttpContext = context.GetCurrentHttpContext();
        if (currentHttpContext == null)
            return;
        currentHttpContext.Response.StatusCode = 401; //返回401给授权筛选器用
        currentHttpContext.SignoutToSwagger();
        await currentHttpContext.SignOutAsync().ConfigureAwait(false);
    }
    /// <inheritdoc/>
    public override async Task<bool> PipelineAsync(AuthorizationHandlerContext context, DefaultHttpContext httpContext)
    {
        var userId = context.User.Claims.FirstOrDefault(it => it.Type == ClaimConst.UserId)?.Value?.ToLong(0) ?? 0;

        var user = await _sysUserService.GetUserByIdAsync(userId).ConfigureAwait(false);
        if (context.Resource is Microsoft.AspNetCore.Components.RouteData routeData)
        {
            var roles = await _sysRoleService.GetRoleListByUserIdAsync(userId).ConfigureAwait(false);

            //这里鉴别用户使能状态
            if (user == null || !user.Status)
            {
                return false;
            }

            //超级管理员都能访问
            var isSuperAdmin = context.User.Claims.FirstOrDefault(it => it.Type == ClaimConst.SuperAdmin)?.Value.ToBoolean();
            if (isSuperAdmin == true) return true;

            // 获取超级管理员特性
            var superAdminAttr = routeData.PageType.CustomAttributes.FirstOrDefault(x =>
               x.AttributeType == typeof(SuperAdminAttribute));

            if (superAdminAttr != null) //如果是超级管理员才能访问的接口
            {
                return false; //直接没权限
            }
            //获取角色授权特性
            var isRolePermission = routeData.PageType.CustomAttributes.FirstOrDefault(x =>
               x.AttributeType == typeof(RolePermissionAttribute));
            if (isRolePermission != null)
            {
                //获取忽略角色授权特性
                var isIgnoreRolePermission = routeData.PageType.CustomAttributes.FirstOrDefault(x =>
       x.AttributeType == typeof(IgnoreRolePermissionAttribute));
                if (isIgnoreRolePermission == null)
                {
                    // 路由名称
                    var routeName = routeData.PageType.CustomAttributes.FirstOrDefault(x =>
                        x.AttributeType == typeof(RouteAttribute))?.ConstructorArguments?[0].Value as string;
                    if (routeName == null) return true;

                    if ((!user.PermissionCodeList.Contains(routeName.CutStart("/")) && !user.PermissionCodeList.Contains(routeName))) //如果当前路由信息不包含在角色授权路由列表中则认证失败
                        return false;
                    else
                        return true;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }
        else
        {
            //这里鉴别用户使能状态
            if (user == null || !user.Status)
            {
                return false;
            }

            //超级管理员都能访问
            var isSuperAdmin = context.User.Claims.FirstOrDefault(it => it.Type == ClaimConst.SuperAdmin)?.Value?.ToBoolean();
            if (isSuperAdmin == true) return true;

            if (httpContext == null)
            {
                //非API请求
                return true;
            }

            var superAdminAttr = httpContext.GetMetadata<SuperAdminAttribute>();

            if (superAdminAttr != null) //如果是超级管理员才能访问的接口
            {
                //获取忽略超级管理员特性
                var ignoreSpuerAdmin = httpContext.GetMetadata<IgnoreSuperAdminAttribute>();
                if (ignoreSpuerAdmin == null && isSuperAdmin != true) //如果只能超级管理员访问并且用户不是超级管理员
                    return false; //直接没权限
            }

            //获取角色授权特性
            var isRolePermission = httpContext.GetMetadata<RolePermissionAttribute>();
            if (isRolePermission != null)
            {
                //获取忽略角色授权特性
                var ignoreRolePermission = httpContext.GetMetadata<IgnoreRolePermissionAttribute>();
                if (ignoreRolePermission == null)
                {
                    // 路由名称
                    var routeName = httpContext.Request.Path.Value;
                    if (routeName == null) return true;
                    if ((!user.PermissionCodeList.Contains(routeName.CutStart("/")) && !user.PermissionCodeList.Contains(routeName))) //如果当前路由信息不包含在角色授权路由列表中则认证失败
                        return false;
                    else
                    {
                        return true; //没有用户信息则返回认证失败
                    }
                }
            }
            else
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// 检查 BearerToken/Cookie 有效性
    /// </summary>
    /// <param name="context">DefaultHttpContext</param>
    /// <returns></returns>
    private async Task<bool> CheckVerificatFromCacheAsync(AuthorizationHandlerContext context)
    {
        DefaultHttpContext currentHttpContext = context.GetCurrentHttpContext();
        var userId = context.User.Claims.FirstOrDefault(it => it.Type == ClaimConst.UserId)?.Value?.ToLong();
        var verificatId = context.User.Claims.FirstOrDefault(it => it.Type == ClaimConst.VerificatId)?.Value?.ToLong();
        var expire = (await _sysDictService.GetAppConfigAsync().ConfigureAwait(false)).LoginPolicy.VerificatExpireTime;
        if (currentHttpContext == null)
        {
            return CheckVerificat(userId, verificatId);
        }
        else
        {
            if (await JWTEncryption.AutoRefreshToken(context, currentHttpContext, expire, expire * 2).ConfigureAwait(false))
            {
                //var token = JWTEncryption.GetJwtBearerToken(currentHttpContext); //获取当前token

                var verificatInfo = userId != null ? VerificatInfoService.GetOne(verificatId ?? 0, false) : null;//获取token信息
                if (verificatInfo != null)
                {
                    if (verificatInfo.VerificatTimeout < DateTime.Now.AddMinutes(1))
                    {
                        verificatInfo.VerificatTimeout = verificatInfo.VerificatTimeout.AddMinutes(verificatInfo.Expire); //新的过期时间
                        VerificatInfoService.Update(verificatInfo); //更新tokne信息到cache
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //失败
                return false;
            }
        }
    }
    private static IVerificatInfoService _verificatInfoService;
    private static IVerificatInfoService VerificatInfoService
    {
        get
        {
            if (_verificatInfoService == null)
                _verificatInfoService = App.GetService<IVerificatInfoService>();

            return _verificatInfoService;
        }
    }
    public static bool CheckVerificat(long? userId, long? verificatId, bool autoUpdate = true)
    {
        var verificatInfo = userId != null ? VerificatInfoService.GetOne(verificatId ?? 0, false) : null;//获取token信息

        if (verificatInfo != null)
        {
            if (verificatInfo.VerificatTimeout < DateTime.Now.AddMinutes(1))
            {
                if (!autoUpdate && verificatInfo.VerificatTimeout < DateTime.Now)
                    return false;

                if (verificatInfo.VerificatTimeout.AddMinutes(verificatInfo.Expire) < DateTime.Now)
                {
                    return false;
                }

                if (autoUpdate)
                {
                    verificatInfo.VerificatTimeout = verificatInfo.VerificatTimeout.AddMinutes(verificatInfo.Expire); //新的过期时间
                    VerificatInfoService.Update(verificatInfo); //更新tokne信息到cache


                }

                //无法在server中刷新cookies，单页面应用会一直保持登录状态，所以这里不需要刷新cookies，但是F5刷新后会重新登录

            }


            return true;
        }
        else
        {
            return false;
        }
    }
}
