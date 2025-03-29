
using System.Collections.Concurrent;

using ThingsGateway.Foundation;
using ThingsGateway.Gateway.Application;

using TouchSocket.Core;

namespace ThingsGateway.RulesEngine;

[CategoryNode(Category = "Trigger", ImgUrl = "_content/ThingsGateway.RulesEngine/img/ValueChanged.svg", Desc = nameof(DeviceChangedTriggerNode), LocalizerType = typeof(ThingsGateway.RulesEngine._Imports), WidgetType = typeof(DeviceWidget))]
public class DeviceChangedTriggerNode : TextNode, ITriggerNode, IDisposable
{
    public DeviceChangedTriggerNode(string id, Point? position = null) : base(id, position) { Title = "DeviceChangedTriggerNode"; Placeholder = "Device.Placeholder"; }


    private Func<NodeOutput, Task> Func { get; set; }
    Task ITriggerNode.StartAsync(Func<NodeOutput, Task> func)
    {
        Func = func;
        FuncDict.Add(this, func);
        if (!DeviceChangedTriggerNodeDict.TryGetValue(Text, out var list))
        {
            var deviceChangedTriggerNodes = new ConcurrentList<DeviceChangedTriggerNode>();
            deviceChangedTriggerNodes.Add(this);
            DeviceChangedTriggerNodeDict.Add(Text, deviceChangedTriggerNodes);
        }
        else
        {
            list.Add(this);
        }
        return Task.CompletedTask;
    }
    public static Dictionary<string, ConcurrentList<DeviceChangedTriggerNode>> DeviceChangedTriggerNodeDict = new();
    public static Dictionary<DeviceChangedTriggerNode, Func<NodeOutput, Task>> FuncDict = new();

    public static BlockingCollection<DeviceBasicData> DeviceDatas = new();

    static DeviceChangedTriggerNode()
    {
        _ = RunAsync();
        GlobalData.DeviceStatusChangeEvent += GlobalData_DeviceStatusChangeEvent;
    }

    private static void GlobalData_DeviceStatusChangeEvent(DeviceRuntime deviceRunTime, DeviceBasicData deviceData)
    {
        if (DeviceChangedTriggerNodeDict.TryGetValue(deviceData.Name, out var deviceChangedTriggerNodes) && deviceChangedTriggerNodes?.Count > 0)
        {
            if (!DeviceDatas.IsAddingCompleted)
            {
                try
                {
                    DeviceDatas.Add(deviceData);
                    return;
                }
                catch (InvalidOperationException) { }
                catch { }
            }
        }
    }
    static Task RunAsync()
    {
        return DeviceDatas.GetConsumingEnumerable().ParallelForEachAsync((async (deviceDatas, token) =>
            {

                if (DeviceChangedTriggerNodeDict.TryGetValue(deviceDatas.Name, out var valueChangedTriggerNodes))
                {
                    await valueChangedTriggerNodes.ParallelForEachAsync(async (item, token) =>
                     {
                         try
                         {
                             if (FuncDict.TryGetValue(item, out var func))
                             {
                                 item.Logger?.Trace($"Device changed: {item.Text}");
                                 await func.Invoke(new NodeOutput() { Value = deviceDatas }).ConfigureAwait(false);

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
        FuncDict.Remove(this);
        if (DeviceChangedTriggerNodeDict.TryGetValue(Text, out var list))
        {
            list.Remove(this);
        }
    }
}

