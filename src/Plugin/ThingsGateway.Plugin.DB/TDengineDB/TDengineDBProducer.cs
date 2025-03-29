//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using BootstrapBlazor.Components;

using Mapster;

using SqlSugar;

using ThingsGateway.Admin.Application;
using ThingsGateway.Foundation;
using ThingsGateway.NewLife.Extension;
using ThingsGateway.Plugin.DB;

namespace ThingsGateway.Plugin.TDengineDB;

/// <summary>
/// TDengineDBProducer
/// </summary>
public partial class TDengineDBProducer : BusinessBaseWithCacheIntervalVariableModel<TDengineDBHistoryValue>, IDBHistoryValueService
{
    internal readonly RealDBProducerProperty _driverPropertys = new()
    {
        DbType = DbType.TDengine,
        BigTextConnectStr = "Host=localhost;Port=6030;Username=root;Password=taosdata;Database=power"
    };
    private readonly TDengineDBProducerVariableProperty _variablePropertys = new();
    /// <inheritdoc/>
    public override Type DriverPropertyUIType => typeof(RealDBProducerPropertyRazor);

    /// <inheritdoc/>
    public override Type DriverUIType
    {
        get
        {
            if (_driverPropertys.BigTextScriptHistoryTable.IsNullOrEmpty())
                return typeof(TDengineDBPage);
            else
                return null;
        }
    }
    public override VariablePropertyBase VariablePropertys => _variablePropertys;

    protected override BusinessPropertyWithCacheInterval _businessPropertyWithCacheInterval => _driverPropertys;

    public async Task<SqlSugarPagedList<IDBHistoryValue>> GetDBHistoryValuePagesAsync(DBHistoryValuePageInput input)
    {
        var data = await Query(input).ToPagedListAsync<TDengineDBHistoryValue, IDBHistoryValue>(input.Current, input.Size).ConfigureAwait(false);//分页
        return data;
    }

    public async Task<List<IDBHistoryValue>> GetDBHistoryValuesAsync(DBHistoryValuePageInput input)
    {
        var data = await Query(input).ToListAsync().ConfigureAwait(false);
        return data.Cast<IDBHistoryValue>().ToList(); ;
    }
    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        _config = new TypeAdapterConfig();
        _config.ForType<VariableRuntime, TDengineDBHistoryValue>()
            .Map(dest => dest.Value, src => src.Value == null ? string.Empty : src.Value.ToString() ?? string.Empty)
            //.Map(dest => dest.Id, src => CommonUtils.GetSingleId())
            .Map(dest => dest.Id, src => src.Id)//Id更改为变量Id
            ;//注意sqlsugar插入时无时区，直接utc时间

        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override bool IsConnected() => success;

    /// <inheritdoc/>
    public override string ToString()
    {
        return $" {nameof(TDengineDBProducer)}";
    }

    internal ISugarQueryable<TDengineDBHistoryValue> Query(DBHistoryValuePageInput input)
    {
        var db = TDengineDBUtil.GetDb(_driverPropertys.DbType, _driverPropertys.BigTextConnectStr, _driverPropertys.TableNameLow);
        var query = db.Queryable<TDengineDBHistoryValue>()
                             .WhereIF(input.StartTime != null, a => a.CreateTime >= input.StartTime)
                           .WhereIF(input.EndTime != null, a => a.CreateTime <= input.EndTime)
                           .WhereIF(!string.IsNullOrEmpty(input.VariableName), it => it.Name.Contains(input.VariableName))
                           .WhereIF(input.VariableNames != null, it => input.VariableNames.Contains(it.Name))
                           ;

        for (int i = input.SortField.Count - 1; i >= 0; i--)
        {
            query = query.OrderByIF(!string.IsNullOrEmpty(input.SortField[i]), $"{input.SortField[i]} {(input.SortDesc[i] ? "desc" : "asc")}");
        }
        query = query.OrderBy(it => it.Id, OrderByType.Desc);//排序

        return query;
    }

    internal async Task<QueryData<TDengineDBHistoryValue>> QueryData(QueryPageOptions option)
    {
        var db = TDengineDBUtil.GetDb(_driverPropertys.DbType, _driverPropertys.BigTextConnectStr, _driverPropertys.TableNameLow);
        var ret = new QueryData<TDengineDBHistoryValue>()
        {
            IsSorted = option.SortOrder != SortOrder.Unset,
            IsFiltered = option.Filters.Count > 0,
            IsAdvanceSearch = option.AdvanceSearches.Count > 0 || option.CustomerSearches.Count > 0,
            IsSearch = option.Searches.Count > 0
        };
        var query = db.Queryable<TDengineDBHistoryValue>().AsTDengineSTable();

        query = db.GetQuery<TDengineDBHistoryValue>(option, query);

        if (option.IsPage)
        {
            RefAsync<int> totalCount = 0;

            var items = await query.ToPageListAsync(option.PageIndex, option.PageItems, totalCount).ConfigureAwait(false);

            ret.TotalCount = totalCount;
            ret.Items = items;
        }
        else if (option.IsVirtualScroll)
        {
            RefAsync<int> totalCount = 0;

            var items = await query.ToPageListAsync(option.StartIndex, option.PageItems, totalCount).ConfigureAwait(false);

            ret.TotalCount = totalCount;
            ret.Items = items;
        }
        else
        {
            var items = await query.ToListAsync().ConfigureAwait(false);
            ret.TotalCount = items.Count;
            ret.Items = items;
        }
        return ret;
    }

    protected override async Task ProtectedStartAsync(CancellationToken cancellationToken)
    {
        var db = TDengineDBUtil.GetDb(_driverPropertys.DbType, _driverPropertys.BigTextConnectStr, _driverPropertys.TableNameLow);
        db.DbMaintenance.CreateDatabase();


        //必须为间隔上传
        if (!_driverPropertys.BigTextScriptHistoryTable.IsNullOrEmpty())
        {
            var hisModel = CSharpScriptEngineExtension.Do<DynamicSQLBase>(_driverPropertys.BigTextScriptHistoryTable);
            {
                await hisModel.DBInit(db, cancellationToken).ConfigureAwait(false);
            }

        }
        else
        {

            var sql = $"""
                CREATE STABLE IF NOT EXISTS  `{_driverPropertys.TableNameLow}`(
                `createtime` TIMESTAMP   ,
                `collecttime` TIMESTAMP   ,
                `id` BIGINT   ,
                `isonline` BOOL   ,
                `value` VARCHAR(255)    ) TAGS(`devicename`  VARCHAR(100) ,`name`  VARCHAR(100))
                """;
            await db.Ado.ExecuteCommandAsync(sql, default, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        await base.ProtectedStartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {
        await UpdateVarModelMemory(cancellationToken).ConfigureAwait(false);
        await UpdateVarModelCache(cancellationToken).ConfigureAwait(false);

    }
}
