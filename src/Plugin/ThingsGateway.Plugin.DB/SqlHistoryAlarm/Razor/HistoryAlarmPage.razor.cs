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

using ThingsGateway.Razor;

namespace ThingsGateway.Plugin.SqlHistoryAlarm;

/// <summary>
/// HistoryAlarmPage
/// </summary>
public partial class HistoryAlarmPage : IDriverUIBase
{
    [Parameter, EditorRequired]
    public object Driver { get; set; }

    public SqlHistoryAlarm SqlHistoryAlarmProducer => (SqlHistoryAlarm)Driver;
    private HistoryAlarmPageInput CustomerSearchModel { get; set; } = new();

    private async Task<QueryData<HistoryAlarm>> OnQueryAsync(QueryPageOptions options)
    {
        var query = await SqlHistoryAlarmProducer.QueryData(options).ConfigureAwait(false);
        return query;
    }
}
