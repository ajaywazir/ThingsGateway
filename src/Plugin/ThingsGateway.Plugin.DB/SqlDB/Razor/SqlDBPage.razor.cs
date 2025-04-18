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

using Microsoft.AspNetCore.Components;

using ThingsGateway.Plugin.DB;
using ThingsGateway.Razor;

namespace ThingsGateway.Plugin.SqlDB;

public partial class SqlDBPage : IDriverUIBase
{
    private readonly SqlDBPageInput _searchHistory = new();
    private readonly SqlDBPageInput _searchReal = new();

    [Parameter, EditorRequired]
    public object Driver { get; set; }

    public SqlDBProducer SqlDBProducer => (SqlDBProducer)Driver;

    private async Task<QueryData<SQLHistoryValue>> OnQueryHistoryAsync(QueryPageOptions options)
    {
        var query = await SqlDBProducer.QueryHistoryData(options).ConfigureAwait(false);
        return query;
    }

    private async Task<QueryData<SQLRealValue>> OnQueryRealAsync(QueryPageOptions options)
    {
        var query = await SqlDBProducer.QueryRealData(options).ConfigureAwait(false);
        return query;
    }
}
