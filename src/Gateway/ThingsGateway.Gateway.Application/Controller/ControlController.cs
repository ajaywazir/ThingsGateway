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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using SqlSugar;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO.Ports;

using ThingsGateway.FriendlyException;

using TouchSocket.Core;
using TouchSocket.Sockets;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 设备控制
/// </summary>
[ApiDescriptionSettings("ThingsGateway.OpenApi", Order = 200)]
[Route("openApi/control")]
[RolePermission]
[LoggingMonitor]
[Authorize(AuthenticationSchemes = "Bearer")]
public class ControlController : ControllerBase
{

    /// <summary>
    /// 清空全部缓存
    /// </summary>
    /// <returns></returns>
    [HttpPost("removeAllCache")]
    [DisplayName("清空全部缓存")]
    public void RemoveAllCache()
    {
        App.CacheService.Clear();
    }

    /// <summary>
    /// 删除通道/设备缓存
    /// </summary>
    /// <returns></returns>
    [HttpPost("removeCache")]
    [DisplayName("删除通道/设备缓存")]
    public void RemoveCache()
    {
        App.GetService<IDeviceService>().DeleteDeviceFromCache();
        App.GetService<IChannelService>().DeleteChannelFromCache();
    }

    /// <summary>
    /// 控制设备线程暂停
    /// </summary>
    /// <returns></returns>
    [HttpPost("pauseBusinessThread")]
    [DisplayName("控制设备线程启停")]
    public async Task PauseDeviceThreadAsync(long id, bool pause)
    {
        if (GlobalData.IdDevices.TryGetValue(id, out var device))
        {
            await GlobalData.SysUserService.CheckApiDataScopeAsync(device.CreateOrgId, device.CreateUserId).ConfigureAwait(false);
            if (device.Driver != null)
            {
                device.Driver.PauseThread(pause);
                return;
            }
        }
        throw Oops.Bah("device not found");
    }
    /// <summary>
    /// 重启当前机构线程
    /// </summary>
    /// <returns></returns>
    [HttpPost("restartScopeThread")]
    [DisplayName("重启当前机构线程")]
    public async Task RestartScopeThread()
    {
        var data = await GlobalData.GetCurrentUserChannels().ConfigureAwait(false);
        await GlobalData.ChannelRuntimeService.RestartChannelAsync(data).ConfigureAwait(false);
    }

    /// <summary>
    /// 重启全部线程
    /// </summary>
    /// <returns></returns>
    [HttpPost("restartAllThread")]
    [DisplayName("重启全部线程")]
    public async Task RestartAllThread()
    {
        await GlobalData.ChannelRuntimeService.RestartChannelAsync(GlobalData.Channels.Values).ConfigureAwait(false);
    }

    /// <summary>
    /// 重启设备线程
    /// </summary>
    /// <returns></returns>
    [HttpPost("restartThread")]
    [DisplayName("重启设备线程")]
    public async Task RestartDeviceThreadAsync(long deviceId)
    {
        if (GlobalData.IdDevices.TryGetValue(deviceId, out var deviceRuntime))
        {
            await GlobalData.SysUserService.CheckApiDataScopeAsync(deviceRuntime.CreateOrgId, deviceRuntime.CreateUserId).ConfigureAwait(false);
            if (GlobalData.TryGetDeviceThreadManage(deviceRuntime, out var deviceThreadManage))
            {
                await deviceThreadManage.RestartDeviceAsync(deviceRuntime, false).ConfigureAwait(false);
            }
        }
        throw Oops.Bah("device not found");
    }

    /// <summary>
    /// 写入多个变量
    /// </summary>
    [HttpPost("writeVariables")]
    [DisplayName("写入变量")]
    public async Task<Dictionary<string, Dictionary<string, OperResult>>> WriteVariablesAsync([FromBody] Dictionary<string, Dictionary<string, string>> deviceDatas)
    {
        foreach (var deviceData in deviceDatas)
        {
            if (GlobalData.Devices.TryGetValue(deviceData.Key, out var device))
            {
                var data = device.VariableRuntimes.Where(a => deviceData.Value.ContainsKey(a.Key)).ToList();
                await GlobalData.SysUserService.CheckApiDataScopeAsync(data.Select(a => a.Value.CreateOrgId), data.Select(a => a.Value.CreateUserId)).ConfigureAwait(false);
            }
        }

        return await GlobalData.RpcService.InvokeDeviceMethodAsync($"WebApi-{UserManager.UserAccount}-{App.HttpContext?.GetRemoteIpAddressToIPv4()}", deviceDatas).ConfigureAwait(false);

    }

    /// <summary>
    /// 保存通道
    /// </summary>
    [HttpPost("batchSaveChannel")]
    [DisplayName("保存通道")]
    public Task<bool> BatchSaveChannelAsync([FromBody] List<ChannelInput> channels, ItemChangedType type, bool restart)
    {
        return GlobalData.ChannelRuntimeService.BatchSaveChannelAsync(channels.Adapt<List<Channel>>(), type, restart);
    }

    /// <summary>
    /// 保存设备
    /// </summary>
    [HttpPost("batchSaveDevice")]
    [DisplayName("保存设备")]
    public Task<bool> BatchSaveDeviceAsync([FromBody] List<DeviceInput> devices, ItemChangedType type, bool restart)
    {
        return GlobalData.DeviceRuntimeService.BatchSaveDeviceAsync(devices.Adapt<List<Device>>(), type, restart);
    }

    /// <summary>
    /// 保存变量
    /// </summary>
    [HttpPost("batchSaveVariable")]
    [DisplayName("保存变量")]
    public Task<bool> BatchSaveVariableAsync([FromBody] List<VariableInput> variables, ItemChangedType type, bool restart)
    {
        return GlobalData.VariableRuntimeService.BatchSaveVariableAsync(variables.Adapt<List<Variable>>(), type, restart, default);
    }



}
public class ChannelInput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public virtual long Id { get; set; }

    /// <summary>
    /// 通道名称
    /// </summary>
    [Required]
    public virtual string Name { get; set; }

    /// <inheritdoc/>
    public virtual ChannelTypeEnum ChannelType { get; set; }

    /// <summary>
    /// 插件名称
    /// </summary>
    [Required]
    public virtual string PluginName { get; set; }

    /// <summary>
    /// 使能
    /// </summary>
    public virtual bool Enable { get; set; } = true;

    /// <summary>
    /// LogLevel
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// 远程地址，可由<see cref="IPHost"/> 与 <see cref="string"/> 相互转化
    /// </summary>
    [UriValidation]
    public virtual string RemoteUrl { get; set; } = "127.0.0.1:502";

    /// <summary>
    /// 本地地址，可由<see cref="IPHost.IPHost(string)"/>与<see href="IPHost.ToString()"/>相互转化
    /// </summary>
    [UriValidation]
    public virtual string BindUrl { get; set; }

    /// <summary>
    /// COM
    /// </summary>
    public virtual string PortName { get; set; } = "COM1";

    /// <summary>
    /// 波特率
    /// </summary>
    public virtual int BaudRate { get; set; } = 9600;

    /// <summary>
    /// 数据位
    /// </summary>
    public virtual int DataBits { get; set; } = 8;

    /// <summary>
    /// 校验位
    /// </summary>
    public virtual Parity Parity { get; set; }

    /// <summary>
    /// 停止位
    /// </summary>
    public virtual StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>
    /// DtrEnable
    /// </summary>
    public virtual bool DtrEnable { get; set; }

    /// <summary>
    /// RtsEnable
    /// </summary>
    public virtual bool RtsEnable { get; set; }

    /// <summary>
    /// 缓存超时
    /// </summary>
    [MinValue(100)]
    public virtual int CacheTimeout { get; set; } = 500;

    /// <summary>
    /// 连接超时
    /// </summary>
    [MinValue(100)]
    public virtual ushort ConnectTimeout { get; set; } = 3000;

    /// <summary>
    /// 最大并发数
    /// </summary>
    [MinValue(1)]
    public virtual int MaxConcurrentCount { get; set; } = 1;

    public virtual int MaxClientCount { get; set; } = 10000;
    public virtual int CheckClearTime { get; set; } = 120000;
    public virtual string Heartbeat { get; set; } = "Heartbeat";

    #region dtu终端

    public virtual int HeartbeatTime { get; set; } = 60000;
    public virtual string DtuId { get; set; }

    #endregion

    public virtual DtuSeviceType DtuSeviceType { get; set; }

}


public class DeviceInput : IValidatableObject
{
    public long Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    [Required]
    [RegularExpression(@"^[^.]*$", ErrorMessage = "The field {0} cannot contain a dot ('.')")]
    public virtual string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 通道
    /// </summary>
    [MinValue(1)]
    [Required]
    public virtual long ChannelId { get; set; }

    /// <summary>
    /// 默认执行间隔，支持corn表达式
    /// </summary>
    public virtual string IntervalTime { get; set; } = "1000";

    /// <summary>
    /// 设备使能
    /// </summary>
    public virtual bool Enable { get; set; } = true;

    /// <summary>
    /// LogLevel
    /// </summary>
    public virtual TouchSocket.Core.LogLevel LogLevel { get; set; } = TouchSocket.Core.LogLevel.Info;

    /// <summary>
    /// 设备属性Json
    /// </summary>
    public Dictionary<string, string>? DevicePropertys { get; set; } = new();

    #region 冗余配置

    /// <summary>
    /// 启用冗余
    /// </summary>
    public bool RedundantEnable { get; set; }

    /// <summary>
    /// 冗余设备Id,只能选择相同驱动
    /// </summary>
    public long? RedundantDeviceId { get; set; }

    /// <summary>
    /// 冗余模式
    /// </summary>
    public virtual RedundantSwitchTypeEnum RedundantSwitchType { get; set; }

    /// <summary>
    /// 冗余扫描间隔
    /// </summary>
    [MinValue(30000)]
    public virtual int RedundantScanIntervalTime { get; set; } = 30000;

    /// <summary>
    /// 冗余切换判断脚本，返回true则切换冗余设备
    /// </summary>
    public virtual string RedundantScript { get; set; }

    #endregion 冗余配置

    #region 备用字段

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark1 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark2 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark3 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark4 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark5 { get; set; }

    #endregion 备用字段

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RedundantEnable && RedundantDeviceId == null)
        {
            yield return new ValidationResult("When enable redundancy, you must select a redundant device.", new[] { nameof(RedundantEnable), nameof(RedundantDeviceId) });
        }
    }

}


public class VariableInput : IValidatableObject
{

    public virtual long Id { get; set; }

    /// <summary>
    /// 设备
    /// </summary>
    [Required]
    public virtual long DeviceId { get; set; }

    /// <summary>
    /// 变量名称
    /// </summary>
    [Required]
    public virtual string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 单位
    /// </summary>
    public virtual string? Unit { get; set; }

    /// <summary>
    /// 间隔时间
    /// </summary>
    public virtual string? IntervalTime { get; set; }

    /// <summary>
    /// 变量地址，可能带有额外的信息，比如<see cref="DataFormatEnum"/> ，以;分割
    /// </summary>
    public string? RegisterAddress { get; set; }

    /// <summary>
    /// 数组长度
    /// </summary>
    public int? ArrayLength { get; set; }

    /// <summary>
    /// 其他方法，若不为空，此时RegisterAddress为方法参数
    /// </summary>
    public string? OtherMethod { get; set; }

    /// <summary>
    /// 使能
    /// </summary>
    public virtual bool Enable { get; set; } = true;

    /// <summary>
    /// 读写权限
    /// </summary>
    public virtual ProtectTypeEnum ProtectType { get; set; } = ProtectTypeEnum.ReadWrite;

    /// <summary>
    /// 数据类型
    /// </summary>
    public virtual DataTypeEnum DataType { get; set; } = DataTypeEnum.Int16;

    /// <summary>
    /// 读取表达式
    /// </summary>
    public virtual string? ReadExpressions { get; set; }

    /// <summary>
    /// 写入表达式
    /// </summary>
    public virtual string? WriteExpressions { get; set; }

    /// <summary>
    /// 是否允许远程Rpc写入
    /// </summary>
    public virtual bool RpcWriteEnable { get; set; } = true;

    /// <summary>
    /// 初始值
    /// </summary>
    public object? InitValue
    {
        get
        {
            return _value;
        }
        set
        {
            if (value != null)
                _value = value?.ToString()?.GetJTokenFromString();
            else
                _value = null;
        }
    }
    private object? _value;

    /// <summary>
    /// 保存初始值
    /// </summary>
    public virtual bool SaveValue { get; set; } = false;

    /// <summary>
    /// 变量额外属性Json
    /// </summary>
    public Dictionary<long, Dictionary<string, string>>? VariablePropertys { get; set; }

    #region 报警
    /// <summary>
    /// 报警延时
    /// </summary>
    public int AlarmDelay { get; set; }

    /// <summary>
    /// 布尔开报警使能
    /// </summary>
    public bool BoolOpenAlarmEnable { get; set; }

    /// <summary>
    /// 布尔开报警约束
    /// </summary>
    public string? BoolOpenRestrainExpressions { get; set; }

    /// <summary>
    /// 布尔开报警文本
    /// </summary>
    public string? BoolOpenAlarmText { get; set; }

    /// <summary>
    /// 布尔关报警使能
    /// </summary>
    public bool BoolCloseAlarmEnable { get; set; }

    /// <summary>
    /// 布尔关报警约束
    /// </summary>
    public string? BoolCloseRestrainExpressions { get; set; }

    /// <summary>
    /// 布尔关报警文本
    /// </summary>
    public string? BoolCloseAlarmText { get; set; }

    /// <summary>
    /// 高报使能
    /// </summary>
    public bool HAlarmEnable { get; set; }

    /// <summary>
    /// 高报约束
    /// </summary>
    public string? HRestrainExpressions { get; set; }

    /// <summary>
    /// 高报文本
    /// </summary>
    public string? HAlarmText { get; set; }

    /// <summary>
    /// 高限值
    /// </summary>
    public double? HAlarmCode { get; set; }

    /// <summary>
    /// 高高报使能
    /// </summary>
    public bool HHAlarmEnable { get; set; }

    /// <summary>
    /// 高高报约束
    /// </summary>
    public string? HHRestrainExpressions { get; set; }

    /// <summary>
    /// 高高报文本
    /// </summary>
    public string? HHAlarmText { get; set; }

    /// <summary>
    /// 高高限值
    /// </summary>
    public double? HHAlarmCode { get; set; }

    /// <summary>
    /// 低报使能
    /// </summary>
    public bool LAlarmEnable { get; set; }

    /// <summary>
    /// 低报约束
    /// </summary>
    public string? LRestrainExpressions { get; set; }

    /// <summary>
    /// 低报文本
    /// </summary>
    public string? LAlarmText { get; set; }

    /// <summary>
    /// 低限值
    /// </summary>
    public double? LAlarmCode { get; set; }

    /// <summary>
    /// 低低报使能
    /// </summary>
    public bool LLAlarmEnable { get; set; }

    /// <summary>
    /// 低低报约束
    /// </summary>
    public string? LLRestrainExpressions { get; set; }

    /// <summary>
    /// 低低报文本
    /// </summary>
    public string? LLAlarmText { get; set; }

    /// <summary>
    /// 低低限值
    /// </summary>
    public double? LLAlarmCode { get; set; }

    /// <summary>
    /// 自定义报警使能
    /// </summary>
    public bool CustomAlarmEnable { get; set; }

    /// <summary>
    /// 自定义报警条件约束
    /// </summary>
    public string? CustomRestrainExpressions { get; set; }

    /// <summary>
    /// 自定义文本
    /// </summary>
    public string? CustomAlarmText { get; set; }

    /// <summary>
    /// 自定义报警条件
    /// </summary>
    public string? CustomAlarmCode { get; set; }

    #endregion 报警

    #region 备用字段

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark1 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark2 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark3 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark4 { get; set; }

    /// <summary>
    /// 自定义
    /// </summary>
    public string? Remark5 { get; set; }

    #endregion 备用字段

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(RegisterAddress) && string.IsNullOrEmpty(OtherMethod))
        {
            yield return new ValidationResult("Both RegisterAddress and OtherMethod cannot be empty or null.", new[] { nameof(RegisterAddress), nameof(OtherMethod) });
        }
        if (HHAlarmEnable && HHAlarmCode == null)
        {
            yield return new ValidationResult("HHAlarmCode cannot be null when HHAlarmEnable is true", new[] { nameof(HHAlarmCode) });
        }
        if (HAlarmEnable && HAlarmCode == null)
        {
            yield return new ValidationResult("HAlarmCode cannot be null when HAlarmEnable is true", new[] { nameof(HAlarmCode) });
        }
        if (LAlarmEnable && LAlarmCode == null)
        {
            yield return new ValidationResult("LAlarmCode cannot be null when LAlarmEnable is true", new[] { nameof(LAlarmCode) });
        }
        if (LLAlarmEnable && LLAlarmCode == null)
        {
            yield return new ValidationResult("LLAlarmCode cannot be null when LLAlarmEnable is true", new[] { nameof(LLAlarmCode) });
        }

        if (HHAlarmEnable && HAlarmEnable && HHAlarmCode <= HAlarmCode)
        {
            yield return new ValidationResult("HHAlarmCode must be greater than HAlarmCode", new[] { nameof(HHAlarmCode), nameof(HAlarmCode) });
        }
        if (HAlarmEnable && LAlarmEnable && HAlarmCode <= LAlarmCode)
        {
            yield return new ValidationResult("HAlarmCode must be greater than LAlarmCode", new[] { nameof(HAlarmCode), nameof(LAlarmCode) });
        }
        if (LAlarmEnable && LLAlarmEnable && LAlarmCode <= LLAlarmCode)
        {
            yield return new ValidationResult("LAlarmCode must be greater than LLAlarmCode", new[] { nameof(LAlarmCode), nameof(LLAlarmCode) });
        }

        if (HHAlarmEnable && LAlarmEnable && HHAlarmCode <= LAlarmCode)
        {
            yield return new ValidationResult("HHAlarmCode should be greater than or less than LAlarmCode", new[] { nameof(HHAlarmCode), nameof(LAlarmCode) });
        }
        if (HHAlarmEnable && LLAlarmEnable && HHAlarmCode <= LLAlarmCode)
        {
            yield return new ValidationResult("HHAlarmCode should be greater than or less than LLAlarmCode", new[] { nameof(HHAlarmCode), nameof(LLAlarmCode) });
        }
        if (HAlarmEnable && LLAlarmEnable && HAlarmCode <= LLAlarmCode)
        {
            yield return new ValidationResult("HAlarmCode should be greater than or less than LLAlarmCode", new[] { nameof(HAlarmCode), nameof(LLAlarmCode) });
        }
    }


}