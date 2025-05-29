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

using ThingsGateway.Admin.Application;
using ThingsGateway.Gateway.Application;

namespace ThingsGateway.Gateway.Razor;

public partial class DeviceCopyComponent
{
    [Inject]
    IStringLocalizer<ThingsGateway.Gateway.Razor._Imports> GatewayLocalizer { get; set; }

    [Parameter]
    [EditorRequired]
    public Device Model { get; set; }


    [Parameter]
    [EditorRequired]
    public List<Variable> Variables { get; set; }

    [Parameter]
    public Func<Dictionary<Device, List<Variable>>, Task> OnSave { get; set; }

    [CascadingParameter]
    private Func<Task>? OnCloseAsync { get; set; }

    private int CopyCount { get; set; }

    private string CopyDeviceNamePrefix { get; set; }

    private int CopyDeviceNameSuffixNumber { get; set; }

    public async Task Save()
    {
        try
        {
            Dictionary<Device, List<Variable>> devices = new();
            for (int i = 0; i < CopyCount; i++)
            {
                Device device = Model.Adapt<Device>();
                device.Id = CommonUtils.GetSingleId();
                device.Name = $"{CopyDeviceNamePrefix}{CopyDeviceNameSuffixNumber + i}";
                List<Variable> variables = new();

                foreach (var item in Variables)
                {
                    Variable v = item.Adapt<Variable>();
                    v.Id = CommonUtils.GetSingleId();
                    v.DeviceId = device.Id;
                    variables.Add(v);
                }
                devices.Add(device, variables);
            }

            if (OnSave != null)
                await OnSave(devices);
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
