//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Mapster;

using System.Diagnostics;

using ThingsGateway.Foundation;
using ThingsGateway.NewLife;
using ThingsGateway.Plugin.DB;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.SqlDB;

/// <summary>
/// SqlDBProducer
/// </summary>
public partial class SqlDBProducer : BusinessBaseWithCacheIntervalVariableModel<SQLHistoryValue>
{
    private TypeAdapterConfig _config;
    private TimeTick _exRealTimerTick;
    private volatile bool _initRealData;

    protected override ValueTask<OperResult> UpdateVarModel(IEnumerable<CacheDBItem<SQLHistoryValue>> item, CancellationToken cancellationToken)
    {
        return UpdateVarModel(item.Select(a => a.Value).OrderBy(a => a.Id), cancellationToken);
    }
    protected override void VariableTimeInterval(IEnumerable<VariableRuntime> variableRuntimes, List<VariableBasicData> variables)
    {
        TimeIntervalUpdateVariable(variables);

        base.VariableTimeInterval(variableRuntimes, variables);
    }
    protected override void VariableChange(VariableRuntime variableRuntime, VariableBasicData variable)
    {
        UpdateVariable(variableRuntime, variable);
        base.VariableChange(variableRuntime, variable);
    }
    protected override ValueTask<OperResult> UpdateVarModels(IEnumerable<SQLHistoryValue> item, CancellationToken cancellationToken)
    {
        return UpdateVarModel(item, cancellationToken);
    }

    private void TimeIntervalUpdateVariable(List<VariableBasicData> variables)
    {
        if (_driverPropertys.GroupUpdate)
        {
            var varList = variables.Where(a => a.Group.IsNullOrEmpty());
            var varGroup = variables.Where(a => !a.Group.IsNullOrEmpty()).GroupBy(a => a.Group);

            foreach (var group in varGroup)
            {
                AddQueueVarModel(new CacheDBItem<List<SQLHistoryValue>>(group.Adapt<List<SQLHistoryValue>>(_config)));
            }
            foreach (var variable in varList)
            {
                AddQueueVarModel(new CacheDBItem<SQLHistoryValue>(variable.Adapt<SQLHistoryValue>(_config)));
            }
        }
        else
        {
            foreach (var variable in variables)
            {
                AddQueueVarModel(new CacheDBItem<SQLHistoryValue>(variable.Adapt<SQLHistoryValue>(_config)));
            }
        }
    }

    private void UpdateVariable(VariableRuntime variableRuntime, VariableBasicData variable)
    {
        if (_driverPropertys.IsHistoryDB)
        {
            if (_driverPropertys.GroupUpdate && !variable.Group.IsNullOrEmpty() && VariableRuntimeGroups.TryGetValue(variable.Group, out var variableRuntimeGroup))
            {

                AddQueueVarModel(new CacheDBItem<List<SQLHistoryValue>>(variableRuntimeGroup.Adapt<List<SQLHistoryValue>>(_config)));

            }
            else
            {
                AddQueueVarModel(new CacheDBItem<SQLHistoryValue>(variableRuntime.Adapt<SQLHistoryValue>(_config)));
            }
        }
    }


    private async ValueTask<OperResult> UpdateVarModel(IEnumerable<SQLHistoryValue> item, CancellationToken cancellationToken)
    {
        var result = await InserableAsync(item.ToList(), cancellationToken).ConfigureAwait(false);
        if (success != result.IsSuccess)
        {
            if (!result.IsSuccess)
                LogMessage.LogWarning(result.ToString());
            success = result.IsSuccess;
        }

        return result;
    }

    #region 方法

    private async ValueTask<OperResult> InserableAsync(List<SQLHistoryValue> dbInserts, CancellationToken cancellationToken)
    {
        try
        {
            var db = SqlDBBusinessDatabaseUtil.GetDb(_driverPropertys);
            db.Ado.CancellationToken = cancellationToken;
            if (!_driverPropertys.BigTextScriptHistoryTable.IsNullOrEmpty())
            {
                var getDeviceModel = CSharpScriptEngineExtension.Do<DynamicSQLBase>(_driverPropertys.BigTextScriptHistoryTable);

                getDeviceModel.Logger = LogMessage;

                await getDeviceModel.DBInsertable(db, dbInserts, cancellationToken).ConfigureAwait(false);

            }
            else
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                var result = await db.Fastest<SQLHistoryValue>().PageSize(50000).SplitTable().BulkCopyAsync(dbInserts).ConfigureAwait(false);
                //var result = await db.Insertable(dbInserts).SplitTable().ExecuteCommandAsync().ConfigureAwait(false);
                stopwatch.Stop();
                if (result > 0)
                {
                    LogMessage.Trace($"HistoryTable Data Count：{result}，watchTime:  {stopwatch.ElapsedMilliseconds} ms");
                }
            }


            return OperResult.Success;
        }
        catch (Exception ex)
        {
            return new OperResult(ex);
        }
    }

    private async ValueTask<OperResult> UpdateAsync(List<SQLRealValue> datas, CancellationToken cancellationToken)
    {
        try
        {
            var db = SqlDBBusinessDatabaseUtil.GetDb(_driverPropertys);
            db.Ado.CancellationToken = cancellationToken;

            if (!_driverPropertys.BigTextScriptRealTable.IsNullOrEmpty())
            {
                var getDeviceModel = CSharpScriptEngineExtension.Do<DynamicSQLBase>(_driverPropertys.BigTextScriptRealTable);
                getDeviceModel.Logger = LogMessage;

                await getDeviceModel.DBInsertable(db, datas, cancellationToken).ConfigureAwait(false);
                return OperResult.Success;

            }
            else
            {
                if (!_initRealData)
                {
                    if (datas?.Count > 0)
                    {
                        var result = await db.Storageable(datas).As(_driverPropertys.ReadDBTableName).PageSize(5000).ExecuteSqlBulkCopyAsync(cancellationToken).ConfigureAwait(false);
                        if (result > 0)
                            LogMessage.Trace($"RealTable Data Count：{result}");
                        _initRealData = true;
                        return OperResult.Success;
                    }
                    return OperResult.Success;
                }
                else
                {
                    if (datas?.Count > 0)
                    {
                        var result = await db.Fastest<SQLRealValue>().AS(_driverPropertys.ReadDBTableName).PageSize(100000).BulkUpdateAsync(datas).ConfigureAwait(false);
                        LogMessage.Trace($"RealTable Data Count：{result}");
                        return OperResult.Success;
                    }
                    return OperResult.Success;
                }
            }
        }
        catch (Exception ex)
        {
            return new OperResult(ex);
        }
    }



    #endregion 方法
}
