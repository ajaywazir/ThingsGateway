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
using ThingsGateway.Plugin.DB;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.QuestDB;

/// <summary>
/// RabbitMQProducer
/// </summary>
public partial class QuestDBProducer : BusinessBaseWithCacheIntervalVariableModel<QuestDBHistoryValue>
{
    private TypeAdapterConfig _config;

    protected override ValueTask<OperResult> UpdateVarModel(IEnumerable<CacheDBItem<QuestDBHistoryValue>> item, CancellationToken cancellationToken)
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
    protected override ValueTask<OperResult> UpdateVarModels(IEnumerable<QuestDBHistoryValue> item, CancellationToken cancellationToken)
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
                AddQueueVarModel(new CacheDBItem<List<QuestDBHistoryValue>>(group.Adapt<List<QuestDBHistoryValue>>(_config)));
            }
            foreach (var variable in varList)
            {
                AddQueueVarModel(new CacheDBItem<QuestDBHistoryValue>(variable.Adapt<QuestDBHistoryValue>(_config)));
            }
        }
        else
        {
            foreach (var variable in variables)
            {
                AddQueueVarModel(new CacheDBItem<QuestDBHistoryValue>(variable.Adapt<QuestDBHistoryValue>(_config)));
            }
        }
    }


    private void UpdateVariable(VariableRuntime variableRuntime, VariableBasicData variable)
    {
        if (_driverPropertys.GroupUpdate && !variable.Group.IsNullOrEmpty() && VariableRuntimeGroups.TryGetValue(variable.Group, out var variableRuntimeGroup))
        {

            AddQueueVarModel(new CacheDBItem<List<QuestDBHistoryValue>>(variableRuntimeGroup.Adapt<List<QuestDBHistoryValue>>(_config)));

        }
        else
        {
            AddQueueVarModel(new CacheDBItem<QuestDBHistoryValue>(variableRuntime.Adapt<QuestDBHistoryValue>(_config)));
        }
    }
    private async ValueTask<OperResult> UpdateVarModel(IEnumerable<QuestDBHistoryValue> item, CancellationToken cancellationToken)
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

    private async ValueTask<OperResult> InserableAsync(List<QuestDBHistoryValue> dbInserts, CancellationToken cancellationToken)
    {
        try
        {
            var db = BusinessDatabaseUtil.GetDb(_driverPropertys.DbType, _driverPropertys.BigTextConnectStr);
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

                var result = await db.Insertable(dbInserts).AS(_driverPropertys.TableName).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);//不要加分表
                stopwatch.Stop();

                //var result = await db.Insertable(dbInserts).SplitTable().ExecuteCommandAsync().ConfigureAwait(false);
                if (result > 0)
                {
                    LogMessage.Trace($"TableName：{_driverPropertys.TableName}，Count：{result}，watchTime:  {stopwatch.ElapsedMilliseconds} ms");
                }
            }


            return OperResult.Success;
        }
        catch (Exception ex)
        {
            return new OperResult(ex);
        }
    }

    #endregion 方法
}
