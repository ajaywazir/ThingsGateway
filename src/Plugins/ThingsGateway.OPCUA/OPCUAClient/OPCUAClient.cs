﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Opc.Ua;
using Opc.Ua.Client;

using ThingsGateway.Foundation;
using ThingsGateway.Foundation.Adapter.OPCUA;
using ThingsGateway.Web.Foundation;

using TouchSocket.Core;

namespace ThingsGateway.OPCUA;

/// <summary>
/// OPCUA客户端
/// </summary>
public class OPCUAClient : CollectBase
{
    internal CollectDeviceRunTime Device;
    internal Foundation.Adapter.OPCUA.OPCUAClient PLC = null;
    private List<CollectVariableRunTime> _deviceVariables = new();
    private OPCUAClientProperty driverPropertys = new();
    /// <inheritdoc cref="OPCUAClient"/>
    public OPCUAClient(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    /// <inheritdoc/>
    public override Type DriverImportUIType => typeof(ImportVariable);

    /// <inheritdoc/>
    public override DriverPropertyBase DriverPropertys => driverPropertys;
    /// <inheritdoc/>
    public override ThingsGatewayBitConverter ThingsGatewayBitConverter { get; } = new(EndianType.Little);


    /// <inheritdoc/>
    public override void AfterStop()
    {
        PLC?.Disconnect();
    }

    /// <inheritdoc/>
    public override async Task BeforStartAsync()
    {
        await PLC?.ConnectAsync();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (PLC != null)
        {
            PLC.DataChangedHandler -= dataChangedHandler;
            PLC.OpcStatusChange -= opcStatusChange;
            PLC.Disconnect();
            PLC.Dispose();
            PLC = null;
        }
    }

    /// <inheritdoc/>
    public override OperResult IsConnected()
    {
        return PLC.Connected ? OperResult.CreateSuccessResult() : new OperResult("失败");
    }

    /// <inheritdoc/>
    public override bool IsSupportAddressRequest()
    {
        return !driverPropertys.ActiveSubscribe;
    }

    /// <inheritdoc/>
    public override OperResult<List<DeviceVariableSourceRead>> LoadSourceRead(List<CollectVariableRunTime> deviceVariables)
    {
        _deviceVariables = deviceVariables;
        if (deviceVariables.Count > 0)
        {
            var result = PLC.SetTags(deviceVariables.Select(a => a.VariableAddress).ToList());
            var sourVars = result?.Select(
      it =>
      {
          return new DeviceVariableSourceRead(driverPropertys.UpdateRate)
          {
              Address = it.Key,
              DeviceVariables = deviceVariables.Where(a => it.Value.Contains(a.VariableAddress)).ToList()
          };
      }).ToList();
            return OperResult.CreateSuccessResult(sourVars);
        }
        else
        {
            return OperResult.CreateSuccessResult(new List<DeviceVariableSourceRead>());
        }
    }

    /// <inheritdoc/>
    public override async Task<OperResult<byte[]>> ReadSourceAsync(DeviceVariableSourceRead deviceVariableSourceRead, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var result = await PLC.ReadNodeAsync(deviceVariableSourceRead.DeviceVariables.Select(a => a.VariableAddress).ToArray());

        if (result.Any(a => StatusCode.IsBad(a.StatusCode)))
        {
            return new OperResult<byte[]>($"读取失败");
        }
        else
        {
            return OperResult.CreateSuccessResult<byte[]>(null);
        }
    }

    /// <inheritdoc/>
    public override async Task<OperResult> WriteValueAsync(CollectVariableRunTime deviceVariable, string value)
    {
        await Task.CompletedTask;
        var result = PLC.WriteNode(deviceVariable.VariableAddress, Convert.ChangeType(value, deviceVariable.DataType));
        return result ? OperResult.CreateSuccessResult() : new OperResult();
    }

    /// <inheritdoc/>
    protected override void Init(CollectDeviceRunTime device, object client = null)
    {
        Device = device;
        OPCNode oPCNode = new();
        oPCNode.OPCUrl = driverPropertys.OPCURL;
        oPCNode.UpdateRate = driverPropertys.UpdateRate;
        oPCNode.DeadBand = driverPropertys.DeadBand;
        oPCNode.GroupSize = driverPropertys.GroupSize;
        oPCNode.ReconnectPeriod = driverPropertys.ReconnectPeriod;
        oPCNode.IsUseSecurity = driverPropertys.IsUseSecurity;
        if (PLC == null)
        {
            PLC = new();
            PLC.DataChangedHandler += dataChangedHandler;
            PLC.OpcStatusChange += opcStatusChange; ;
        }
        if (!driverPropertys.UserName.IsNullOrEmpty())
        {
            PLC.UserIdentity = new UserIdentity(driverPropertys.UserName, driverPropertys.Password);
        }
        else
        {
            PLC.UserIdentity = new UserIdentity(new AnonymousIdentityToken());
        }
        PLC.OPCNode = oPCNode;
    }

    /// <inheritdoc/>
    protected override Task<OperResult<byte[]>> ReadAsync(string address, int length, CancellationToken cancellationToken)
    {
        //不走ReadAsync
        throw new NotImplementedException();
    }
    private void dataChangedHandler(List<(MonitoredItem monitoredItem, MonitoredItemNotification monitoredItemNotification)> values)
    {
        try
        {
            if (!Device.Enable)
            {
                return;
            }
            Device.DeviceStatus = DeviceStatusEnum.OnLine;

            if (IsLogOut)
                _logger?.LogTrace(ToString() + " OPC值变化" + values.ToJson());

            foreach (var data in values)
            {
                if (!Device.Enable)
                {
                    return;
                }

                var itemReads = _deviceVariables.Where(it => it.VariableAddress == data.monitoredItem.StartNodeId).ToList();
                foreach (var item in itemReads)
                {
                    var value = data.monitoredItemNotification.Value.Value;
                    var quality = StatusCode.IsBad(data.monitoredItemNotification.Value.StatusCode) ? 0 : 192;

                    var time = data.monitoredItemNotification.Value.SourceTimestamp;
                    if (value != null && quality == 192)
                    {
                        item.SetValue(value, time);
                    }
                    else
                    {
                        item.SetValue(null, time);
                        Device.DeviceStatus = DeviceStatusEnum.OnLineButNoInitialValue;
                        Device.DeviceOffMsg = $"{item.Name} 质量为Bad ";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, ToString());
            Device.DeviceOffMsg = ex.Message;
        }
    }
    private void opcStatusChange(object sender, OPCUAStatusEventArgs e)
    {
        if (e.Error)
        {
            _logger.LogWarning(e.Text);
            Device.DeviceStatus = DeviceStatusEnum.OnLineButNoInitialValue;
            Device.DeviceOffMsg = $"{e.Text}";
        }
        else
        {
            _logger.LogTrace(e.Text);
        }
    }
}

/// <inheritdoc/>
public class OPCUAClientProperty : DriverPropertyBase
{
    /// <summary>
    /// 连接Url
    /// </summary>
    [DeviceProperty("连接Url", "")] public string OPCURL { get; set; } = "opc.tcp://127.0.0.1:49320";


    /// <summary>
    /// 登录账号
    /// </summary>
    [DeviceProperty("登录账号", "为空时将采用匿名方式登录")] public string UserName { get; set; }
    /// <summary>
    /// 登录密码
    /// </summary>
    [DeviceProperty("登录密码", "")] public string Password { get; set; }

    /// <summary>
    /// 激活订阅
    /// </summary>
    [DeviceProperty("激活订阅", "")] public bool ActiveSubscribe { get; set; } = true;

    /// <summary>
    /// 死区
    /// </summary>
    [DeviceProperty("死区", "")] public float DeadBand { get; set; } = 0;

    /// <summary>
    /// 自动分组大小
    /// </summary>
    [DeviceProperty("自动分组大小", "")] public int GroupSize { get; set; } = 500;

    /// <summary>
    /// 安全策略
    /// </summary>
    [DeviceProperty("安全策略", "True为使用安全策略，False为无")] public bool IsUseSecurity { get; set; } = true;



    /// <summary>
    /// 重连频率
    /// </summary>
    [DeviceProperty("重连频率", "")] public int ReconnectPeriod { get; set; } = 5000;

    /// <summary>
    /// 更新频率
    /// </summary>
    [DeviceProperty("更新频率", "")] public int UpdateRate { get; set; } = 1000;

}