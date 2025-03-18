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
using ThingsGateway.NewLife.Json.Extension;

namespace ThingsGateway.Gateway.Razor;

public partial class USheet
{

    private UniverSheet? _sheetExcel = null;
    private Task OnReadyAsync() => OnPushExcelData();

    [Parameter]
    public USheetDatas Model { get; set; }
    [Parameter]
    public Func<USheetDatas, Task>? OnSave { get; set; }

    private USheetDatas Data { get; set; }

    private async Task OnPushExcelData()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            await InvokeAsync(async () =>
            {
                await _sheetExcel.PushDataAsync(new UniverSheetData()
                {
                    CommandName = "SetWorkbook",
                    Data = Model.ToJsonNetString()
                });
            });
        });
        await Task.Delay(10);
    }
    [Inject]
    private IStringLocalizer<ThingsGateway.Razor._Imports> RazorLocalizer { get; set; }
    private async Task OnSaveExcelData()
    {
        var result = await _sheetExcel.PushDataAsync(new UniverSheetData()
        {
            CommandName = "GetWorkbook",
        });
        Data = result?.Data?.ToString().FromJsonNetString<USheetDatas>();
        StateHasChanged();
    }
    [CascadingParameter]
    private Func<Task>? OnCloseAsync { get; set; }

    public async Task Save()
    {
        try
        {
            await OnSaveExcelData();
            if (OnSave != null)
                await OnSave.Invoke(Data);
            if (OnCloseAsync != null)
                await OnCloseAsync();
            await ToastService.Default();
        }
        catch (Exception ex)
        {
            await ToastService.Warn(ex);
        }
    }
    [Inject]
    ToastService ToastService { get; set; }
}
