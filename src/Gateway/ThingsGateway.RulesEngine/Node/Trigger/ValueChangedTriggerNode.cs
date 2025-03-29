using System.Collections.Concurrent;

using ThingsGateway.Foundation;
using ThingsGateway.Gateway.Application;

using TouchSocket.Core;

namespace ThingsGateway.RulesEngine;

[CategoryNode(Category = "Trigger", ImgUrl = "_content/ThingsGateway.RulesEngine/img/ValueChanged.svg", Desc = nameof(ValueChangedTriggerNode), LocalizerType = typeof(ThingsGateway.RulesEngine._Imports), WidgetType = typeof(VariableWidget))]
public class ValueChangedTriggerNode : VariableNode, ITriggerNode, IDisposable
{
    public ValueChangedTriggerNode(string id, Point? position = null) : base(id, position) { Title = "ValueChangedTriggerNode"; }

    private Func<NodeOutput, Task> Func { get; set; }
    Task ITriggerNode.StartAsync(Func<NodeOutput, Task> func)
    {
        Func = func;
        FuncDict.TryAdd(this, func);
        if (ValueChangedTriggerNodeDict.TryGetValue(DeviceText, out var deviceVariableDict))
        {

            if (deviceVariableDict.TryGetValue(Text, out var valueChangedTriggerNodes))
            {
                valueChangedTriggerNodes.Add(this);
            }
            else
            {
                deviceVariableDict.TryAdd(Text, new());
                deviceVariableDict[Text].Add(this);
            }
        }
        else
        {
            ValueChangedTriggerNodeDict.TryAdd(DeviceText, new());
            ValueChangedTriggerNodeDict[DeviceText].TryAdd(Text, new());
            ValueChangedTriggerNodeDict[DeviceText][Text].Add(this);

        }
        return Task.CompletedTask;
    }
    public static ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentList<ValueChangedTriggerNode>>> ValueChangedTriggerNodeDict = new();
    public static ConcurrentDictionary<ValueChangedTriggerNode, Func<NodeOutput, Task>> FuncDict = new();

    public static BlockingCollection<VariableBasicData> VariableBasicDatas = new();
    static ValueChangedTriggerNode()
    {
        _ = RunAsync();
        GlobalData.VariableValueChangeEvent += GlobalData_VariableValueChangeEvent;
    }
    private static void GlobalData_VariableValueChangeEvent(VariableRuntime variableRuntime, VariableBasicData variableData)
    {
        if (ValueChangedTriggerNodeDict.TryGetValue(variableRuntime.DeviceName, out var valueNodeDict) &&
                valueNodeDict.TryGetValue(variableRuntime.Name, out var valueChangedTriggerNodes) &&
                valueChangedTriggerNodes?.Count > 0)
        {
            if (!VariableBasicDatas.IsAddingCompleted)
            {
                try
                {
                    VariableBasicDatas.Add(variableData);
                    return;
                }
                catch (InvalidOperationException) { }
                catch { }
            }
        }
    }
    static Task RunAsync()
    {
        return VariableBasicDatas.GetConsumingEnumerable().ParallelForEachAsync((async (variableBasicData, token) =>
        {

            if (ValueChangedTriggerNodeDict.TryGetValue(variableBasicData.DeviceName, out var valueNodeDict) &&
        valueNodeDict.TryGetValue(variableBasicData.Name, out var valueChangedTriggerNodes))
            {
                await valueChangedTriggerNodes.ParallelForEachAsync(async (item, token) =>
            {
                try
                {
                    if (FuncDict.TryGetValue(item, out var func))
                    {
                        item.Logger?.Trace($"Variable changed: {item.Text}");
                        await func.Invoke(new NodeOutput() { Value = variableBasicData }).ConfigureAwait(false);

                    }
                }
                catch (Exception ex)
                {
                    item.Logger?.LogWarning(ex);
                }
            }, token).ConfigureAwait(false);
            }

        }), default);

    }

    public void Dispose()
    {
        FuncDict.TryRemove(this, out _);
        if (ValueChangedTriggerNodeDict.TryGetValue(DeviceText, out var valueNodeDict) &&
            valueNodeDict.TryGetValue(Text, out var valueChangedTriggerNodes))
        {
            valueChangedTriggerNodes.Remove(this);
        }
    }
}
