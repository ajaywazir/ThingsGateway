﻿//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using ThingsGateway.Admin.Application;

namespace ThingsGateway.Admin.Razor;

public partial class MenuChoiceDialog
{
    private string ModuleTitle;

    [Parameter]
    [EditorRequired]
    [NotNull]
    public long ModuleId { get; set; }

    [Parameter]
    [EditorRequired]
    [NotNull]
    public long Value { get; set; }

    [Parameter]
    public EventCallback<long> ValueChanged { get; set; }

    [NotNull]
    private List<TreeViewItem<SysResource>>? Items { get; set; }

    [Inject]
    [NotNull]
    private ISysResourceService? SysResourceService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var all = (await SysResourceService.GetAllAsync());
        var items = all.Where(a => a.Category == ResourceCategoryEnum.Menu && a.Module == ModuleId);
        ModuleTitle = all.FirstOrDefault(a => a.Id == ModuleId)?.Title;
        Items = ResourceUtil.BuildTreeItemList(items, new List<long> { Value }, RenderTreeItem);
        await base.OnParametersSetAsync();
    }

    private static bool ModelEqualityComparer(SysResource x, SysResource y) => x.Id == y.Id;

    private async Task OnTreeItemClick(TreeViewItem<SysResource> item)
    {
        Value = item.Value.Id;
        if (ValueChanged.HasDelegate)
        {
            await ValueChanged.InvokeAsync(Value);
        }
    }
}
