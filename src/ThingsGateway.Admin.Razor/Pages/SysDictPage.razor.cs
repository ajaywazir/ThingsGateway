﻿
//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://diego2098.gitee.io/thingsgateway-docs/
//  QQ群：605534569
//------------------------------------------------------------------------------




using ThingsGateway.Admin.Application;

namespace ThingsGateway.Admin.Razor;

public partial class SysDictPage
{
    [Inject]
    [NotNull]
    private ISysDictService? SysDictService { get; set; }

    private SysDict? SearchModel { get; set; } = new();

    #region 查询

    private OperateLogPageInput CustomerSearchModel { get; set; } = new OperateLogPageInput();

    private async Task<QueryData<SysDict>> OnQueryAsync(QueryPageOptions options)
    {
        var data = await SysDictService.PageAsync(options);
        return data;
    }

    #endregion 查询
}