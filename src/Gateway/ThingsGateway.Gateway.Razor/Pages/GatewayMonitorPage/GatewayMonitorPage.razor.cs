//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using ThingsGateway.Gateway.Application;

namespace ThingsGateway.Gateway.Razor;

public partial class GatewayMonitorPage : IDisposable
{
    private ChannelDeviceTreeItem SelectModel { get; set; } = new() { ChannelDevicePluginType = ChannelDevicePluginTypeEnum.PluginType, PluginType = PluginTypeEnum.Collect };

    #region 查询
    [Inject]
    private IDispatchService<DeviceRuntime> DeviceRuntimeDispatchService { get; set; }
    [Inject]
    private IDispatchService<ChannelRuntime> ChannelRuntimeDispatchService { get; set; }

    protected override void OnInitialized()
    {
        ChannelRuntimeDispatchService.Subscribe(Refresh);
        DeviceRuntimeDispatchService.Subscribe(Refresh);
        base.OnInitialized();
    }
    public void Dispose()
    {
        ChannelRuntimeDispatchService.UnSubscribe(Refresh);
        DeviceRuntimeDispatchService.UnSubscribe(Refresh);
        GC.SuppressFinalize(this);
    }
    private async Task Refresh(DispatchEntry<DeviceRuntime> entry)
    {
        await InvokeAsync(StateHasChanged);
    }
    private async Task Refresh(DispatchEntry<ChannelRuntime> entry)
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task TreeChangedAsync(ChannelDeviceTreeItem channelDeviceTreeItem)
    {
        ShowChannelRuntime = 0;
        ShowDeviceRuntime = 0;
        SelectModel = channelDeviceTreeItem;
        if (channelDeviceTreeItem.TryGetChannelRuntime(out var channelRuntime))
        {
            ShowChannelRuntime = channelRuntime.Id;
            if (channelRuntime.IsCollect == true)
            {
                VariableRuntimes = channelRuntime.ReadDeviceRuntimes.SelectMany(a => a.Value.ReadOnlyVariableRuntimes.Select(a => a.Value).Where(a => a != null));
            }
            else
            {
                VariableRuntimes = channelRuntime.ReadDeviceRuntimes.Where(a => a.Value?.Driver?.IdVariableRuntimes != null).SelectMany(a => a.Value?.Driver?.IdVariableRuntimes?.Where(a => a.Value != null)?.Select(a => a.Value)).Where(a => a != null);
            }

        }
        else if (channelDeviceTreeItem.TryGetDeviceRuntime(out var deviceRuntime))
        {
            ShowDeviceRuntime = deviceRuntime.Id;
            if (deviceRuntime.IsCollect == true)
            {
                VariableRuntimes = deviceRuntime.ReadOnlyVariableRuntimes.Select(a => a.Value).Where(a => a != null);
            }
            else
            {
                VariableRuntimes = deviceRuntime.Driver?.IdVariableRuntimes?.Where(a => a.Value != null)
.Select(a => a.Value) ?? Enumerable.Empty<VariableRuntime>();
            }

        }
        else if (channelDeviceTreeItem.TryGetPluginName(out var pluginName))
        {
            var pluginType = GlobalData.PluginService.GetList().FirstOrDefault(a => a.FullName == pluginName)?.PluginType;
            if (pluginType == PluginTypeEnum.Collect)
            {
                var channels = await GlobalData.GetCurrentUserChannels().ConfigureAwait(false);
                VariableRuntimes = channels.Where(a => a.PluginName == pluginName).SelectMany(a => a.ReadDeviceRuntimes).SelectMany(a => a.Value.ReadOnlyVariableRuntimes).Select(a => a.Value).Where(a => a != null);
            }
            else
            {
                var channels = await GlobalData.GetCurrentUserChannels().ConfigureAwait(false);
                VariableRuntimes = channels.Where(a => a.PluginName == pluginName).SelectMany(a => a.ReadDeviceRuntimes).Where(a => a.Value.Driver?.IdVariableRuntimes != null).SelectMany(a => a.Value.Driver?.IdVariableRuntimes).Select(a => a.Value);
            }
        }
        else
        {
            var variables = await GlobalData.GetCurrentUserIdVariables().ConfigureAwait(false);
            VariableRuntimes = variables.Where(a => a != null);
        }
        await InvokeAsync(StateHasChanged);
    }

    #endregion 查询
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await TreeChangedAsync(SelectModel);
        await base.OnAfterRenderAsync(firstRender);
    }
    public IEnumerable<VariableRuntime> VariableRuntimes { get; set; } = Enumerable.Empty<VariableRuntime>();

    private long ShowChannelRuntime { get; set; }
    private long ShowDeviceRuntime { get; set; }
    public ShowTypeEnum? ShowType { get; set; }
    private bool AutoRestartThread { get; set; } = true;


}
