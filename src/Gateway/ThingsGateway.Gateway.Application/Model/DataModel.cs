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

using Newtonsoft.Json;

namespace ThingsGateway.Gateway.Application;

/// <summary>
/// 设备业务变化数据
/// </summary>
public class DeviceBasicData
{
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DeviceRuntime DeviceRuntime{ get; set; }

    /// <inheritdoc cref="PrimaryIdEntity.Id"/>
    public long Id { get; set; }

    /// <inheritdoc cref="Device.Name"/>
    public string Name => DeviceRuntime?.Name;

    /// <inheritdoc cref="DeviceRuntime.ActiveTime"/>
    public DateTime ActiveTime { get; set; }

    /// <inheritdoc cref="DeviceRuntime.DeviceStatus"/>
    public DeviceStatusEnum DeviceStatus { get; set; }

    /// <inheritdoc cref="DeviceRuntime.LastErrorMessage"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string LastErrorMessage { get; set; }

    /// <inheritdoc cref="DeviceRuntime.PluginName"/>
    public string PluginName => DeviceRuntime?.PluginName;

    /// <inheritdoc cref="Device.Description"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Description => DeviceRuntime?.Description;

    /// <inheritdoc cref="Device.Remark1"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark1 => DeviceRuntime?.Remark1;

    /// <inheritdoc cref="Device.Remark2"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark2 => DeviceRuntime?.Remark2;

    /// <inheritdoc cref="Device.Remark3"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark3 => DeviceRuntime?.Remark3;

    /// <inheritdoc cref="Device.Remark4"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark4 => DeviceRuntime?.Remark4;

    /// <inheritdoc cref="Device.Remark5"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark5 => DeviceRuntime?.Remark5;


}

/// <summary>
/// 变量业务变化数据
/// </summary>
public class VariableBasicData
{
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public VariableRuntime VariableRuntime { get; set; }

    /// <inheritdoc cref="PrimaryIdEntity.Id"/>
    public long Id => VariableRuntime.Id;

    /// <inheritdoc cref="Variable.Name"/>
    public string Name => VariableRuntime.Name;

    /// <inheritdoc cref="Variable.CollectGroup"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string CollectGroup => VariableRuntime.CollectGroup;

    /// <inheritdoc cref="Variable.BusinessGroup"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string BusinessGroup => VariableRuntime.BusinessGroup;

    /// <inheritdoc cref="VariableRuntime.DeviceName"/>
    public string DeviceName => VariableRuntime.DeviceName;

    /// <inheritdoc cref="VariableRuntime.RuntimeType"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string RuntimeType { get; set; }

    /// <inheritdoc cref="VariableRuntime.Value"/>
    public object Value { get; set; }
    /// <inheritdoc cref="VariableRuntime.RawValue"/>
    public object RawValue { get; set; }
    /// <inheritdoc cref="VariableRuntime.LastSetValue"/>
    public object LastSetValue { get; set; }

    /// <inheritdoc cref="VariableRuntime.ChangeTime"/>
    public DateTime ChangeTime { get; set; }

    /// <inheritdoc cref="VariableRuntime.CollectTime"/>
    public DateTime CollectTime { get; set; }

    /// <inheritdoc cref="VariableRuntime.IsOnline"/>
    public bool IsOnline { get; set; }

    /// <inheritdoc cref="VariableRuntime.DeviceRuntime"/>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [AutoGenerateColumn(Ignore = true)]
    public DeviceBasicData DeviceRuntime { get; set; }

    /// <inheritdoc cref="VariableRuntime.LastErrorMessage"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LastErrorMessage { get; set; }
    /// <inheritdoc cref="Variable.RegisterAddress"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RegisterAddress => VariableRuntime.RegisterAddress;

    /// <inheritdoc cref="Variable.Unit"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unit => VariableRuntime.Unit;

    /// <inheritdoc cref="Variable.Description"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Description => VariableRuntime.Description;

    /// <inheritdoc cref="Variable.ProtectType"/>
    public ProtectTypeEnum ProtectType => VariableRuntime.ProtectType;

    /// <inheritdoc cref="Variable.DataType"/>
    public DataTypeEnum DataType => VariableRuntime.DataType;

    /// <inheritdoc cref="Device.Remark1"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark1 => VariableRuntime.Remark1;

    /// <inheritdoc cref="Device.Remark2"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark2 => VariableRuntime.Remark2;

    /// <inheritdoc cref="Device.Remark3"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark3 => VariableRuntime.Remark3;

    /// <inheritdoc cref="Device.Remark4"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark4 => VariableRuntime.Remark4;

    /// <inheritdoc cref="Device.Remark5"/>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Remark5 => VariableRuntime.Remark5;
}


