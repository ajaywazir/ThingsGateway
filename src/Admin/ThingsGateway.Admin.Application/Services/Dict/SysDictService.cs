﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using ThingsGateway.FriendlyException;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.Razor;

namespace ThingsGateway.Admin.Application;

internal sealed class SysDictService : BaseService<SysDict>, ISysDictService
{
    /// <summary>
    /// 删除业务配置
    /// </summary>
    /// <param name="ids">id列表</param>
    [OperDesc("DeleteDict")]
    public async Task<bool> DeleteDictAsync(IEnumerable<long> ids)
    {
        var result = await base.DeleteAsync(ids).ConfigureAwait(false);
        if (result)
            RefreshCache(DictTypeEnum.Define);
        return result;
    }

    /// <summary>
    /// 修改登录策略
    /// </summary>
    /// <param name="input">登录策略</param>
    [OperDesc("EditLoginPolicy")]
    public async Task EditLoginPolicyAsync(LoginPolicy input)
    {
        using var db = GetDB();
        //更新数据
        List<SysDict> dicts = new List<SysDict>()
        {
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(LoginPolicy), Name = nameof(LoginPolicy.SingleOpen), Code = input.SingleOpen.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(LoginPolicy), Name = nameof(LoginPolicy.ErrorCount), Code = input.ErrorCount.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(LoginPolicy), Name = nameof(LoginPolicy.ErrorLockTime), Code = input.ErrorLockTime.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(LoginPolicy), Name = nameof(LoginPolicy.ErrorResetTime), Code = input.ErrorResetTime.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(LoginPolicy), Name = nameof(LoginPolicy.VerificatExpireTime), Code = input.VerificatExpireTime.ToString() },
    };
        var storageable = await db.Storageable(dicts).WhereColumns(it => new { it.DictType, it.Category, it.Name }).ToStorageAsync().ConfigureAwait(false);

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await storageable.AsUpdateable.UpdateColumns(it => new { it.Code }).ExecuteCommandAsync().ConfigureAwait(false);
            await storageable.AsInsertable.ExecuteCommandAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            RefreshCache(DictTypeEnum.System);//刷新缓存
        }
        else
        {
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <summary>
    /// 修改页面策略
    /// </summary>
    /// <param name="input">页面策略</param>
    [OperDesc("EditPagePolicy")]
    public async Task EditPagePolicyAsync(PagePolicy input)
    {
        using var db = GetDB();
        //更新数据
        List<SysDict> dicts = new List<SysDict>()
        {
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PagePolicy), Name = nameof(PagePolicy.Shortcuts), Code = input.Shortcuts.ToSystemTextJsonString() },
    };
        var storageable = await db.Storageable(dicts).WhereColumns(it => new { it.DictType, it.Category, it.Name }).ToStorageAsync().ConfigureAwait(false);

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await storageable.AsUpdateable.UpdateColumns(it => new { it.Code }).ExecuteCommandAsync().ConfigureAwait(false);
            await storageable.AsInsertable.ExecuteCommandAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            RefreshCache(DictTypeEnum.System);//刷新缓存
        }
        else
        {
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <summary>
    /// 修改密码策略
    /// </summary>
    /// <param name="input">密码策略</param>
    [OperDesc("EditPasswordPolicy")]
    public async Task EditPasswordPolicyAsync(PasswordPolicy input)
    {
        using var db = GetDB();
        //更新数据
        List<SysDict> dicts = new List<SysDict>()
        {
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.PasswordContainLower), Code = input.PasswordContainLower.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.DefaultPassword), Code = input.DefaultPassword.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.PasswordContainChar), Code = input.PasswordContainChar.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.PasswordContainUpper), Code = input.PasswordContainUpper.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.PasswordMinLen), Code = input.PasswordMinLen.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(PasswordPolicy), Name = nameof(PasswordPolicy.PasswordContainNum), Code = input.PasswordContainNum.ToString() },
    };
        var storageable = await db.Storageable(dicts).WhereColumns(it => new { it.DictType, it.Category, it.Name }).ToStorageAsync().ConfigureAwait(false);

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await storageable.AsUpdateable.UpdateColumns(it => new { it.Code }).ExecuteCommandAsync().ConfigureAwait(false);
            await storageable.AsInsertable.ExecuteCommandAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            RefreshCache(DictTypeEnum.System);//刷新缓存
        }
        else
        {
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <summary>
    /// 修改网站设置
    /// </summary>
    /// <param name="input"></param>
    [OperDesc("EditWebsitePolicy")]
    public async Task EditWebsitePolicyAsync(WebsitePolicy input)
    {
        var websiteOptions = App.GetOptions<WebsiteOptions>()!;
        if (websiteOptions.Demo)
        {
            throw Oops.Bah(Localizer["DemoCanotUpdateWebsitePolicy"]);
        }

        using var db = GetDB();
        //更新数据
        List<SysDict> dicts = new List<SysDict>()
        {
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(WebsitePolicy), Name = nameof(WebsitePolicy.WebStatus), Code = input.WebStatus.ToString() },
            new SysDict() { DictType = DictTypeEnum.System, Category = nameof(WebsitePolicy), Name = nameof(WebsitePolicy.CloseTip), Code = input.CloseTip },
         };
        var storageable = await db.Storageable(dicts).WhereColumns(it => new { it.DictType, it.Category, it.Name }).ToStorageAsync().ConfigureAwait(false);

        //事务
        var result = await db.UseTranAsync(async () =>
        {
            await storageable.AsUpdateable.UpdateColumns(it => new { it.Code }).ExecuteCommandAsync().ConfigureAwait(false);
            await storageable.AsInsertable.ExecuteCommandAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        if (result.IsSuccess)//如果成功了
        {
            RefreshCache(DictTypeEnum.System);//刷新缓存
        }
        else
        {
            throw new(result.ErrorMessage, result.ErrorException);
        }
    }

    /// <summary>
    /// 获取系统配置
    /// </summary>
    public async Task<AppConfig> GetAppConfigAsync()
    {
        var key = $"{CacheConst.Cache_SysDict}{DictTypeEnum.System}{nameof(AppConfig)}";//系统配置key
        var appConfig = App.CacheService.Get<AppConfig>(key);
        if (appConfig == null)
        {
            List<SysDict> sysDicts = await GetSystemConfigAsync().ConfigureAwait(false);

            appConfig = new AppConfig() { LoginPolicy = new(), PasswordPolicy = new(), PagePolicy = new(), WebsitePolicy = new() };
            //登录策略
            appConfig.LoginPolicy.ErrorCount = sysDicts.FirstOrDefault(a => a.Category == nameof(LoginPolicy) && a.Name == nameof(LoginPolicy.ErrorCount))?.Code.ToInt() ?? 3;
            appConfig.LoginPolicy.ErrorLockTime = sysDicts.FirstOrDefault(a => a.Category == nameof(LoginPolicy) && a.Name == nameof(LoginPolicy.ErrorLockTime))?.Code.ToInt() ?? 1;
            appConfig.LoginPolicy.ErrorResetTime = sysDicts.FirstOrDefault(a => a.Category == nameof(LoginPolicy) && a.Name == nameof(LoginPolicy.ErrorResetTime))?.Code.ToInt() ?? 1;
            appConfig.LoginPolicy.SingleOpen = sysDicts.FirstOrDefault(a => a.Category == nameof(LoginPolicy) && a.Name == nameof(LoginPolicy.SingleOpen))?.Code.ToBoolean() ?? false;
            appConfig.LoginPolicy.VerificatExpireTime = sysDicts.FirstOrDefault(a => a.Category == nameof(LoginPolicy) && a.Name == nameof(LoginPolicy.VerificatExpireTime))?.Code.ToInt() ?? 14400;
            //密码策略
            appConfig.PasswordPolicy.PasswordContainChar = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.PasswordContainChar))?.Code.ToBoolean() ?? false;
            appConfig.PasswordPolicy.PasswordContainNum = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.PasswordContainNum))?.Code.ToBoolean() ?? false;
            appConfig.PasswordPolicy.PasswordContainLower = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.PasswordContainLower))?.Code.ToBoolean() ?? false;
            appConfig.PasswordPolicy.PasswordMinLen = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.PasswordMinLen))?.Code.ToInt() ?? 6;
            appConfig.PasswordPolicy.PasswordContainUpper = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.PasswordContainUpper))?.Code.ToBoolean() ?? false;
            appConfig.PasswordPolicy.DefaultPassword = sysDicts.FirstOrDefault(a => a.Category == nameof(PasswordPolicy) && a.Name == nameof(PasswordPolicy.DefaultPassword))?.Code ?? "111111";

            //页面策略
            appConfig.PagePolicy.Shortcuts = sysDicts.FirstOrDefault(a => a.Category == nameof(PagePolicy) && a.Name == nameof(PagePolicy.Shortcuts))?.Code.FromJsonNetString<List<long>>() ?? new List<long>();

            //网站设置
            appConfig.WebsitePolicy.WebStatus = sysDicts.FirstOrDefault(a => a.Category == nameof(WebsitePolicy) && a.Name == nameof(WebsitePolicy.WebStatus))?.Code.ToBoolean() ?? true;
            appConfig.WebsitePolicy.CloseTip = sysDicts.FirstOrDefault(a => a.Category == nameof(WebsitePolicy) && a.Name == nameof(WebsitePolicy.CloseTip))?.Code ?? "";
            App.CacheService.Set(key, appConfig);
        }

        return appConfig;
    }

    /// <summary>
    /// 根据分类从缓存/数据库获取列表
    /// </summary>
    /// <param name="category">分类</param>
    /// <param name="name">名称</param>
    /// <returns>配置列表</returns>
    public async Task<SysDict> GetByKeyAsync(string category, string name)
    {
        var key = CacheConst.Cache_SysDict + DictTypeEnum.Define;
        var field = $"{category}:sysdict:{name}";
        var sysDict = App.CacheService.HashGetOne<SysDict>(key, field);
        if (sysDict == null)
        {
            using var db = GetDB();
            sysDict = await db.Queryable<SysDict>().FirstAsync(a => a.DictType == DictTypeEnum.Define && a.Category == category && a.Name == name).ConfigureAwait(false);
            App.CacheService.HashAdd(key, field, sysDict);
        }
        return sysDict;
    }

    /// <summary>
    /// 从缓存/数据库获取系统配置列表
    /// </summary>
    /// <returns></returns>
    public async Task<IDictionary<string, SysDict>> GetDefineConfigAsync()
    {
        var key = $"{CacheConst.Cache_SysDict}{DictTypeEnum.Define}";//系统配置key
        var sysDicts = App.CacheService.HashGetAll<SysDict>(key);
        if (sysDicts.Count == 0)
        {
            using var db = GetDB();
            sysDicts = (await db.Queryable<SysDict>().Where(a => a.DictType == DictTypeEnum.Define).ToListAsync().ConfigureAwait(false)).ToDictionary(a =>
            $"{a.Category}:sysdict:{a.Name}", a => a);
            App.CacheService.Set(key, sysDicts);
        }

        return sysDicts;
    }


    /// <summary>
    /// 从缓存/数据库获取系统配置列表
    /// </summary>
    /// <returns></returns>
    public async Task<List<SysDict>> GetSystemConfigAsync()
    {
        var key = $"{CacheConst.Cache_SysDict}{DictTypeEnum.System}";//系统配置key
        var sysDicts = App.CacheService.Get<List<SysDict>>(key);
        if (sysDicts == null)
        {
            using var db = GetDB();
            sysDicts = await db.Queryable<SysDict>().Where(a => a.DictType == DictTypeEnum.System).ToListAsync().ConfigureAwait(false);
            App.CacheService.Set(key, sysDicts);
        }

        return sysDicts;
    }


    /// <summary>
    /// 表格查询
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public Task<QueryData<SysDict>> PageAsync(QueryPageOptions option)
    {
        return QueryAsync(option,
            a => a.Where(it => it.DictType == DictTypeEnum.Define)
            .WhereIF(!option.SearchText.IsNullOrWhiteSpace(), a => a.Category.Contains(option.SearchText!)));
    }

    /// <summary>
    /// 修改业务配置
    /// </summary>
    /// <param name="input">配置项</param>
    /// <param name="type">保存类型</param>
    [OperDesc("SaveDict")]
    public async Task<bool> SaveDictAsync(SysDict input, ItemChangedType type)
    {
        await CheckInput(input).ConfigureAwait(false);//检查参数
        var reuslt = await base.SaveAsync(input, type).ConfigureAwait(false);
        if (reuslt)
            RefreshCache(DictTypeEnum.Define);

        return reuslt;
    }

    #region 方法

    /// <summary>
    /// 检查输入参数
    /// </summary>
    /// <param name="input">配置项</param>
    private async Task CheckInput(SysDict input)
    {

        //设置类型为业务
        input.DictType = DictTypeEnum.Define;

        var dict = await GetByKeyAsync(input.Category, input.Name).ConfigureAwait(false);//获取全部字典

        //判断是否从存在重复

        if (dict != null && dict.Id != input.Id)
        {
            throw Oops.Bah(Localizer["DictDup", input.Category, input.Name]);
        }

    }

    /// <summary>
    /// 刷新缓存
    /// </summary>
    /// <param name="define">类型</param>
    /// <returns></returns>
    private void RefreshCache(DictTypeEnum define)
    {
        App.CacheService.Remove($"{CacheConst.Cache_SysDict}{define}");
        if (define == DictTypeEnum.System)
            App.CacheService.Remove($"{CacheConst.Cache_SysDict}{define}{nameof(AppConfig)}");
    }


    #endregion 方法
}
