//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Confluent.Kafka;

using Mapster;

using ThingsGateway.Extension.Generic;
using ThingsGateway.Foundation;
using ThingsGateway.Foundation.Extension.Generic;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.Kafka;

/// <summary>
/// Kafka消息生产
/// </summary>
public partial class KafkaProducer : BusinessBaseWithCacheIntervalScript<VariableBasicData, DeviceBasicData, AlarmVariable>
{
    private IProducer<Null, byte[]> _producer;
    private ProducerBuilder<Null, byte[]> _producerBuilder;
    private ProducerConfig _producerconfig;
    private volatile bool producerSuccess = true;

    protected override void AlarmChange(AlarmVariable alarmVariable)
    {
        if (!_businessPropertyWithCacheIntervalScript.AlarmTopic.IsNullOrWhiteSpace())
            AddQueueAlarmModel(new(alarmVariable));
        base.AlarmChange(alarmVariable);
    }

    protected override void DeviceTimeInterval(DeviceRuntime deviceRunTime, DeviceBasicData deviceData)
    {
        if (!_businessPropertyWithCacheIntervalScript.DeviceTopic.IsNullOrWhiteSpace())
            AddQueueDevModel(new(deviceData));
        base.DeviceChange(deviceRunTime, deviceData);
    }

    protected override void DeviceChange(DeviceRuntime deviceRunTime, DeviceBasicData deviceData)
    {
        if (!_businessPropertyWithCacheIntervalScript.DeviceTopic.IsNullOrWhiteSpace())
            AddQueueDevModel(new(deviceData));
        base.DeviceChange(deviceRunTime, deviceData);
    }

    protected override ValueTask<OperResult> UpdateAlarmModel(IEnumerable<CacheDBItem<AlarmVariable>> item, CancellationToken cancellationToken)
    {
        return UpdateAlarmModel(item.Select(a => a.Value).OrderBy(a => a.Id), cancellationToken);
    }

    protected override ValueTask<OperResult> UpdateDevModel(IEnumerable<CacheDBItem<DeviceBasicData>> item, CancellationToken cancellationToken)
    {
        return UpdateDevModel(item.Select(a => a.Value).OrderBy(a => a.Id), cancellationToken);
    }

    protected override ValueTask<OperResult> UpdateVarModel(IEnumerable<CacheDBItem<VariableBasicData>> item, CancellationToken cancellationToken)
    {
        return UpdateVarModel(item.Select(a => a.Value).OrderBy(a => a.Id), cancellationToken);
    }
    protected override ValueTask<OperResult> UpdateVarModels(IEnumerable<VariableBasicData> item, CancellationToken cancellationToken)
    {
        return UpdateVarModel(item, cancellationToken);
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
    private void TimeIntervalUpdateVariable(List<VariableBasicData> variables)
    {
        if (!_businessPropertyWithCacheIntervalScript.VariableTopic.IsNullOrWhiteSpace())
        {
            if (_driverPropertys.GroupUpdate)
            {
                var varList = variables.Where(a => a.BusinessGroup.IsNullOrEmpty());
                var varGroup = variables.Where(a => !a.BusinessGroup.IsNullOrEmpty()).GroupBy(a => a.BusinessGroup);

                foreach (var group in varGroup)
                {
                    AddQueueVarModel(new CacheDBItem<List<VariableBasicData>>(group.Adapt<List<VariableBasicData>>()));
                }
                foreach (var variable in varList)
                {
                    AddQueueVarModel(new CacheDBItem<VariableBasicData>(variable));
                }
            }
            else
            {
                foreach (var variable in variables)
                {
                    AddQueueVarModel(new CacheDBItem<VariableBasicData>(variable));
                }
            }
        }
    }

    private void UpdateVariable(VariableRuntime variableRuntime, VariableBasicData variable)
    {
        if (!_businessPropertyWithCacheIntervalScript.VariableTopic.IsNullOrWhiteSpace())
        {
            if (_driverPropertys.GroupUpdate && !variable.BusinessGroup.IsNullOrEmpty() && VariableRuntimeGroups.TryGetValue(variable.BusinessGroup, out var variableRuntimeGroup))
            {

                AddQueueVarModel(new CacheDBItem<List<VariableBasicData>>(variableRuntimeGroup.Adapt<List<VariableBasicData>>()));

            }
            else
            {
                AddQueueVarModel(new CacheDBItem<VariableBasicData>(variable));
            }
        }
    }


    #region private

    private async ValueTask<OperResult> Update(List<TopicArray> topicArrayList, CancellationToken cancellationToken)
    {
        foreach (var topicArray in topicArrayList)
        {
            var result = await KafKaUpAsync(topicArray, cancellationToken).ConfigureAwait(false);
            if (success != result.IsSuccess)
            {
                if (!result.IsSuccess)
                {
                    LogMessage.LogWarning(result.ToString());
                }
                success = result.IsSuccess;
            }
            if (!result.IsSuccess)
            {
                return result;
            }
        }
        return OperResult.Success;
    }

    private async ValueTask<OperResult> UpdateAlarmModel(IEnumerable<AlarmVariable> item, CancellationToken cancellationToken)
    {
        var topicArrayList = GetAlarmTopicArrays(item);
        return await Update(topicArrayList, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<OperResult> UpdateDevModel(IEnumerable<DeviceBasicData> item, CancellationToken cancellationToken)
    {
        var topicArrayList = GetDeviceTopicArray(item);
        return await Update(topicArrayList, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<OperResult> UpdateVarModel(IEnumerable<VariableBasicData> item, CancellationToken cancellationToken)
    {
        var topicArrayList = GetVariableBasicDataTopicArray(item.WhereIf(_driverPropertys.OnlineFilter, a => a.IsOnline == true));
        return await Update(topicArrayList, cancellationToken).ConfigureAwait(false);
    }

    #endregion private

    #region 方法

    private async Task AllPublishAsync(CancellationToken cancellationToken)
    {
        //保留消息
        //分解List，避免超出字节大小限制
        var varData = IdVariableRuntimes.Select(a => a.Value).Adapt<List<VariableBasicData>>().ChunkBetter(_driverPropertys.SplitSize);
        var devData = CollectDevices?.Select(a => a.Value).Adapt<List<DeviceBasicData>>().ChunkBetter(_driverPropertys.SplitSize);
        var alramData = GlobalData.ReadOnlyRealAlarmIdVariables.Select(a => a.Value).Adapt<List<AlarmVariable>>().ChunkBetter(_driverPropertys.SplitSize);
        foreach (var item in varData)
        {
            if (!success)
                break;
            await UpdateVarModel(item, cancellationToken).ConfigureAwait(false);
        }
        if (devData != null)
        {
            foreach (var item in devData)
            {
                if (!success)
                    break;
                await UpdateDevModel(item, cancellationToken).ConfigureAwait(false);
            }
        }
        foreach (var item in alramData)
        {
            if (!success)
                break;
            await UpdateAlarmModel(item, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// kafka上传，返回上传结果
    /// </summary>
    public async ValueTask<OperResult> KafKaUpAsync(TopicArray topicArray, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource cancellationTokenSource = new(_driverPropertys.Timeout);
            using CancellationTokenSource stoppingToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
            var result = await _producer.ProduceAsync(topicArray.Topic, new Message<Null, byte[]> { Value = topicArray.Json }, stoppingToken.Token).ConfigureAwait(false);
            if (result.Status != PersistenceStatus.Persisted)
            {
                return new OperResult("Upload fail");
            }
            else
            {
                if (_driverPropertys.DetailLog)
                {
                    if (LogMessage.LogLevel <= TouchSocket.Core.LogLevel.Trace)
                        LogMessage.LogTrace(GetDetailLogString(topicArray, _memoryVarModelQueue.Count));
                    else if (LogMessage.LogLevel <= TouchSocket.Core.LogLevel.Debug)
                        LogMessage.LogDebug(GetCountLogString(topicArray, _memoryVarModelQueue.Count));
                }
                else
                {
                    if (LogMessage.LogLevel <= TouchSocket.Core.LogLevel.Debug)
                        LogMessage.LogDebug(GetCountLogString(topicArray, _memoryVarModelQueue.Count));
                }
                return OperResult.Success;
            }
        }
        catch (OperationCanceledException)
        {
            return new OperResult("Timeout");
        }
        catch (Exception ex)
        {
            return new OperResult(ex);
        }
    }

    #endregion 方法
}
