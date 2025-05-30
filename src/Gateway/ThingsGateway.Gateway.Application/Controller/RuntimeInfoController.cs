﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SqlSugar;

using System.ComponentModel;

using ThingsGateway.NewLife.Extension;
namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 采集状态信息
/// </summary>
[ApiDescriptionSettings("ThingsGateway.OpenApi", Order = 200)]
[DisplayName("数据状态")]
[Route("openApi/runtimeInfo")]
[ApiController]
[RolePermission]
[Authorize(AuthenticationSchemes = "Bearer")]
public class RuntimeInfoController : ControllerBase
{
    /// <summary>
    /// 获取通道信息
    /// </summary>
    /// <returns></returns>
    [HttpGet("channelList")]
    [DisplayName("获取通道信息")]
    public async Task<SqlSugarPagedList<ChannelRuntime>> GetChannelListAsync(ChannelPageInput input)
    {

        var channelRuntimes = await GlobalData.GetCurrentUserChannels().ConfigureAwait(false);

        var data = channelRuntimes
         .WhereIF(!string.IsNullOrEmpty(input.Name), u => u.Name.Contains(input.Name))
         .WhereIF(!string.IsNullOrEmpty(input.PluginName), u => u.PluginName == input.PluginName)
         .WhereIF(input.PluginType != null, u => u.PluginType == input.PluginType)
         .ToPagedList(input);
        return data;
    }


    /// <summary>
    /// 获取设备信息
    /// </summary>
    /// <returns></returns>
    [HttpGet("deviceList")]
    [DisplayName("获取设备信息")]
    public async Task<SqlSugarPagedList<DeviceRuntime>> GetDeviceListAsync(DevicePageInput input)
    {
        var deviceRuntimes = await GlobalData.GetCurrentUserDevices().ConfigureAwait(false);
        var data = deviceRuntimes
         .WhereIF(!string.IsNullOrEmpty(input.Name), u => u.Name.Contains(input.Name))
         .WhereIF(!input.ChannelName.IsNullOrEmpty(), u => u.ChannelName == input.ChannelName)
         .WhereIF(!string.IsNullOrEmpty(input.PluginName), u => u.PluginName == input.PluginName)
         .WhereIF(input.PluginType != null, u => u.PluginType == input.PluginType)
         .ToPagedList(input);
        return data;
    }

    /// <summary>
    /// 获取实时报警信息
    /// </summary>
    /// <returns></returns>
    [HttpGet("realAlarmList")]
    [DisplayName("获取实时报警变量信息")]
    public async Task<SqlSugarPagedList<AlarmVariable>> GetRealAlarmList(AlarmVariablePageInput input)
    {
        var realAlarmVariables = await GlobalData.GetCurrentUserRealAlarmVariables().ConfigureAwait(false);

        var data = realAlarmVariables
            .WhereIF(!input.RegisterAddress.IsNullOrEmpty(), a => a.RegisterAddress == input.RegisterAddress)
            .WhereIF(!input.Name.IsNullOrEmpty(), a => a.Name == input.Name)
            .WhereIF(!input.DeviceName.IsNullOrEmpty(), a => a.DeviceName == input.DeviceName)
            .ToPagedList(input);
        return data;
    }

    /// <summary>
    /// 确认实时报警
    /// </summary>
    /// <returns></returns>
    [HttpPost("checkRealAlarm")]
    [RequestAudit]
    [DisplayName("确认实时报警")]
    public async Task CheckRealAlarm(long variableId)
    {
        if (GlobalData.ReadOnlyRealAlarmIdVariables.TryGetValue(variableId, out var variable))
        {
            await GlobalData.SysUserService.CheckApiDataScopeAsync(variable.CreateOrgId, variable.CreateUserId).ConfigureAwait(false);
            GlobalData.AlarmHostedService.ConfirmAlarm(variableId);
        }
    }

    /// <summary>
    /// 获取变量信息
    /// </summary>
    /// <returns></returns>
    [HttpGet("variableList")]
    [DisplayName("获取变量信息")]
    public async Task<SqlSugarPagedList<VariableRuntime>> GetVariableList(VariablePageInput input)
    {
        var variables = await GlobalData.GetCurrentUserIdVariables().ConfigureAwait(false);
        var data = variables
        .WhereIF(!input.Name.IsNullOrWhiteSpace(), a => a.Name == input.Name)
         .WhereIF(!input.DeviceName.IsNullOrEmpty(), a => a.DeviceName == input.DeviceName)
            .WhereIF(!input.RegisterAddress.IsNullOrWhiteSpace(), a => a.RegisterAddress == input.RegisterAddress)
            .WhereIF(input.BusinessDeviceId > 0, a =>
            {
                return GlobalData.ContainsVariable(input.BusinessDeviceId, a);
            }
           )


            .ToPagedList(input);
        return data;
    }


    /// <summary>
    /// 获取默认插件属性
    /// </summary>
    [HttpGet("getPluginPropertys")]
    [DisplayName("获取默认插件属性")]
    public Dictionary<string, string> GetPluginPropertys(string pluginName)
    {
        var data = GlobalData.PluginService.GetDriverPropertyTypes(pluginName);

        var devicePropertys = PluginServiceUtil.SetDict(data.Model);

        return devicePropertys;
    }

    /// <summary>
    /// 获取插件
    /// </summary>
    [HttpGet("getPluginInfos")]
    [DisplayName("获取插件")]
    public SqlSugarPagedList<PluginInfo> GetPluginInfos(PluginInfoPageInput input)
    {
        //指定关键词搜索为插件FullName
        return GlobalData.PluginService.GetList().WhereIF(!input.Name.IsNullOrWhiteSpace(), a => a.Name == input.Name)
                .ToPagedList(input);
    }
}
public class PluginInfoPageInput : BasePageInput
{
    /// <inheritdoc/>
    public string? Name { get; set; }
}
public class ChannelPageInput : BasePageInput
{
    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <inheritdoc/>
    public string? PluginName { get; set; }

    /// <inheritdoc/>
    public PluginTypeEnum? PluginType { get; set; }
}


public class DevicePageInput : BasePageInput
{
    /// <inheritdoc/>
    public string? ChannelName { get; set; }

    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <inheritdoc/>
    public string? PluginName { get; set; }

    /// <inheritdoc/>
    public PluginTypeEnum? PluginType { get; set; }
}

public class AlarmVariablePageInput : BasePageInput
{
    /// <inheritdoc/>
    public string? DeviceName { get; set; }

    /// <inheritdoc/>
    public string Name { get; set; }

    /// <inheritdoc/>
    public string RegisterAddress { get; set; }
}

public class VariablePageInput : BasePageInput
{
    /// <inheritdoc/>
    public long BusinessDeviceId { get; set; }

    /// <inheritdoc/>
    public string? DeviceName { get; set; }

    /// <inheritdoc/>
    public string Name { get; set; }

    /// <inheritdoc/>
    public string RegisterAddress { get; set; }
}