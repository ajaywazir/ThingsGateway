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

using ThingsGateway.Gateway.Application;

using Yitter.IdGenerator;

namespace ThingsGateway.Gateway.Razor;

public partial class ChannelCopyComponent
{
    [Inject]
    IStringLocalizer<ThingsGateway.Gateway.Razor._Imports> GatewayLocalizer { get; set; }

    [Parameter]
    [EditorRequired]
    public Channel Model { get; set; }


    [Parameter]
    [EditorRequired]
    public Dictionary<Device, List<Variable>> Devices { get; set; }

    [Parameter]
    public Func<List<Channel>, Dictionary<Device, List<Variable>>, Task> OnSave { get; set; }

    [CascadingParameter]
    private Func<Task>? OnCloseAsync { get; set; }

    private int CopyCount { get; set; }

    private string CopyChannelNamePrefix { get; set; }

    private int CopyChannelNameSuffixNumber { get; set; }
    private string CopyDeviceNamePrefix { get; set; }

    private int CopyDeviceNameSuffixNumber { get; set; }

    public async Task Save()
    {
        try
        {
            List<Channel> channels = new();
            Dictionary<Device, List<Variable>> devices = new();
            for (int i = 0; i < CopyCount; i++)
            {
                Channel channel = Model.Adapt<Channel>();
                channel.Id = YitIdHelper.NextId();
                channel.Name = $"{CopyChannelNamePrefix}{CopyChannelNameSuffixNumber + i}";

                int index = 0;
                foreach (var item in Devices)
                {
                    Device device = item.Key.Adapt<Device>();
                    device.Id = YitIdHelper.NextId();
                    device.Name = $"{channel.Name}_{CopyDeviceNamePrefix}{CopyDeviceNameSuffixNumber + (index++)}";
                    device.ChannelId = channel.Id;
                    List<Variable> variables = new();

                    foreach (var variable in item.Value)
                    {
                        Variable v = variable.Adapt<Variable>();
                        v.Id = YitIdHelper.NextId();
                        v.DeviceId = device.Id;
                        variables.Add(v);
                    }
                    devices.Add(device, variables);
                }

                channels.Add(channel);

            }

            if (OnSave != null)
                await OnSave(channels, devices);
            if (OnCloseAsync != null)
                await OnCloseAsync();
            await ToastService.Default();
        }
        catch (Exception ex)
        {
            await ToastService.Warn(ex);
        }
    }


}
