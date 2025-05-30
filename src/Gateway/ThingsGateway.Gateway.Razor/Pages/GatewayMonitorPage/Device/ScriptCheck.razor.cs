﻿//------------------------------------------------------------------------------
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

public partial class ScriptCheck
{
    private string Input { get; set; }
    private string Output { get; set; }

    [Parameter, EditorRequired]
    public IEnumerable<object> Data { get; set; }
    [Parameter, EditorRequired]
    public string Script { get; set; }
    [Parameter, EditorRequired]
    public EventCallback<string> ScriptChanged { get; set; }
    private async Task Change(string script)
    {
        Script = script;
        if (ScriptChanged.HasDelegate)
            await ScriptChanged.InvokeAsync(script);

    }
    private Type type;
    protected override void OnInitialized()
    {
        Input = Data.ToSystemTextJsonString();
        type = Data.GetType();
        base.OnInitialized();
    }

    private void CheckScript()
    {
        try
        {
            Data = (IEnumerable<object>)Newtonsoft.Json.JsonConvert.DeserializeObject(Input, type);
            var value = Data.GetDynamicModel(Script);
            Output = value.ToSystemTextJsonString();
        }

        catch (Exception ex)
        {
            Output = ex.ToString();
        }

    }
    [Inject]
    private IStringLocalizer<DeviceEditComponent> Localizer { get; set; }

    private async Task GetDemo(Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
    {
        Script = OnGetDemo?.Invoke();
        await Change(Script);
    }

    [Parameter, EditorRequired]
    public Func<string> OnGetDemo { get; set; }
}
