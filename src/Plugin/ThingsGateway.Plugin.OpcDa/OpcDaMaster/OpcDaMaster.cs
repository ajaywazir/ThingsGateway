//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;

using ThingsGateway.Foundation.OpcDa;
using ThingsGateway.Foundation.OpcDa.Da;
using ThingsGateway.Gateway.Application;
using ThingsGateway.NewLife.Json.Extension;
using ThingsGateway.NewLife.Threading;

using TouchSocket.Core;

namespace ThingsGateway.Plugin.OpcDa;

/// <summary>
/// <inheritdoc/>
/// </summary>
[OnlyWindowsSupport]
public class OpcDaMaster : CollectBase
{
    private readonly OpcDaMasterProperty _driverProperties = new();

    private ThingsGateway.Foundation.OpcDa.OpcDaMaster _plc;

    private volatile bool success = true;

    /// <inheritdoc/>
    public override CollectPropertyBase CollectProperties => _driverProperties;

    /// <inheritdoc/>
    public override Type DriverDebugUIType => typeof(ThingsGateway.Debug.OpcDaMaster);



    /// <inheritdoc/>
    protected override async Task InitChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        //载入配置
        OpcDaProperty opcNode = new()
        {
            OpcIP = _driverProperties.OpcIP,
            OpcName = _driverProperties.OpcName,
            UpdateRate = _driverProperties.UpdateRate,
            DeadBand = _driverProperties.DeadBand,
            GroupSize = _driverProperties.GroupSize,
            ActiveSubscribe = _driverProperties.ActiveSubscribe,
            CheckRate = _driverProperties.CheckRate
        };
        if (_plc == null)
        {
            _plc = new();
            _plc.DataChangedHandler += DataChangedHandler;
            _plc.LogEvent = (a, b, c, d) => LogMessage.Log((LogLevel)a, b, c, d);
        }
        _plc.Init(opcNode);
        await base.InitChannelAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override bool IsConnected() => _plc?.IsConnected == true;

    public override string ToString()
    {
        return $"{_driverProperties.OpcIP}-{_driverProperties.OpcName}";
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_plc != null)
            _plc.DataChangedHandler -= DataChangedHandler;
        _plc?.SafeDispose();

        VariableAddresDicts?.Clear();
        base.Dispose(disposing);
    }

    public override string GetAddressDescription()
    {
        return _plc?.GetAddressDescription();
    }


    protected override async ValueTask ProtectedExecuteAsync(CancellationToken cancellationToken)
    {
        if (_driverProperties.ActiveSubscribe)
        {
            //获取设备连接状态
            if (IsConnected())
            {
                //更新设备活动时间
                CurrentDevice.SetDeviceStatus(TimerX.Now, false);
            }
            else
            {
                CurrentDevice.SetDeviceStatus(TimerX.Now, true);
            }

            ScriptVariableRun(cancellationToken);

        }
        else
        {
            await base.ProtectedExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    protected override async Task<List<VariableSourceRead>> ProtectedLoadSourceReadAsync(List<VariableRuntime> deviceVariables)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        try
        {
            if (deviceVariables.Count > 0)
            {
                List<VariableSourceRead> variableSourceReads = new List<VariableSourceRead>();
                foreach (var deviceVariableGroups in deviceVariables.GroupBy(a => a.CollectGroup))
                {

                    var result = _plc.AddItemsWithSave(deviceVariableGroups.Where(a => !string.IsNullOrEmpty(a.RegisterAddress)).Select(a => a.RegisterAddress!).ToList());
                    var sourVars = result?.Select(
              it =>
              {
                  var read = new VariableSourceRead()
                  {
                      TimeTick = new(_driverProperties.UpdateRate.ToString()),
                      RegisterAddress = it.Key,
                  };
                  HashSet<string> ids = new(it.Value.Select(b => b.ItemID));

                  var variables = deviceVariableGroups.Where(a => ids.Contains(a.RegisterAddress));
                  foreach (var v in variables)
                  {
                      read.AddVariable(v);
                  }
                  return read;
              }).ToList();
                    variableSourceReads.AddRange(sourVars);
                }
                return variableSourceReads;
            }
            else
            {
                return new();
            }
        }
        finally
        {
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask<OperResult<byte[]>> ReadSourceAsync(VariableSourceRead deviceVariableSourceRead, CancellationToken cancellationToken)
    {
        try
        {
            await ReadWriteLock.ReaderLockAsync(cancellationToken).ConfigureAwait(false);
            _plc.ReadItemsWithGroup(deviceVariableSourceRead.RegisterAddress);
            return OperResult.CreateSuccessResult(Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            return new OperResult<byte[]>($"ReadSourceAsync Error：{Environment.NewLine}{ex}");
        }

    }

    /// <inheritdoc/>
    protected override async ValueTask<Dictionary<string, OperResult>> WriteValuesAsync(Dictionary<VariableRuntime, JToken> writeInfoLists, CancellationToken cancellationToken)
    {

        using var writeLock = ReadWriteLock.WriterLock();
        await ValueTask.CompletedTask.ConfigureAwait(false);
        try
        {
            var result = _plc.WriteItem(writeInfoLists.ToDictionary(a => a.Key.RegisterAddress!, a => a.Value.GetObjectFromJToken()!));
            return result.ToDictionary<KeyValuePair<string, Tuple<bool, string>>, string, OperResult>(a =>
            {
                return writeInfoLists.Keys.FirstOrDefault(b => b.RegisterAddress == a.Key).Name;
            }, a =>
            {
                if (!a.Value.Item1)
                    return new OperResult(a.Value.Item2);
                else
                    return OperResult.Success;
            }
                 );
        }
        finally
        {
        }
    }
    public override async Task AfterVariablesChangedAsync(CancellationToken cancellationToken)
    {
        _plc?.Disconnect();
        await base.AfterVariablesChangedAsync(cancellationToken).ConfigureAwait(false);

        VariableAddresDicts = IdVariableRuntimes.Select(a => a.Value).Where(it => !it.RegisterAddress.IsNullOrEmpty()).GroupBy(a => a.RegisterAddress).ToDictionary(a => a.Key!, b => b.ToList());
        try
        {
            _plc?.Connect();
        }
        catch (Exception ex)
        {
            LogMessage.LogWarning(ex);
        }
    }

    private Dictionary<string, List<VariableRuntime>> VariableAddresDicts { get; set; } = new();

    private void DataChangedHandler(string name, int serverGroupHandle, List<ItemReadResult> values)
    {
        DateTime time = DateTime.Now;
        try
        {
            if (CurrentDevice.Pause)
                return;
            if (DisposedValue)
                return;
            LogMessage.Trace($"{ToString()} Change:{Environment.NewLine} {values?.ToJsonNetString()}");

            foreach (var data in values)
            {
                if (CurrentDevice.Pause)
                    return;
                if (DisposedValue)
                    return;
                var type = data.Value.GetType();
                if (data.Value is Array)
                {
                    type = type.GetElementType();
                }
                if (!VariableAddresDicts.TryGetValue(data.Name, out var itemReads)) return;

                foreach (var item in itemReads)
                {
                    if (CurrentDevice.Pause)
                        return;
                    if (DisposedValue)
                        return;
                    var value = data.Value;
                    var quality = data.Quality;
                    if (_driverProperties.SourceTimestampEnable)
                    {
                        time = data.TimeStamp.ToLocalTime();
                    }
                    if (quality == 192)
                    {
                        if (item.DataType == DataTypeEnum.Object)
                            if (type.Namespace.StartsWith("System"))
                            {
                                var enumValues = Enum.GetValues<DataTypeEnum>();
                                var stringList = enumValues.Select(e => e.ToString());
                                if (stringList.Contains(type.Name))
                                    try { item.DataType = Enum.Parse<DataTypeEnum>(type.Name); } catch { }
                            }

                        var jToken = JToken.FromObject(value);
                        object newValue;
                        if (jToken is JValue jValue)
                        {
                            newValue = jValue.Value;
                        }
                        else
                        {
                            newValue = jToken;
                        }
                        item.SetValue(newValue, time);
                    }
                    else
                    {
                        item.SetValue(null, time, false);
                        item.VariableSource.LastErrorMessage = $"Bad quality：{quality}";
                    }
                }
            }

            success = true;
        }
        catch (Exception ex)
        {
            if (success)
                LogMessage?.LogWarning(ex);
            success = false;
        }
    }


}
