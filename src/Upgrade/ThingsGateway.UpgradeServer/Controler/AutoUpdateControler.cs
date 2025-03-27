//------------------------------------------------------------------------------
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

using ThingsGateway.Admin.Application;

namespace ThingsGateway.Upgrade;

/// <summary>
/// 设备控制
/// </summary>
[ApiDescriptionSettings("ThingsGateway.OpenApi", Order = 200)]
[Route("openApi/autoUpdate")]
[RolePermission]
[LoggingMonitor]
[Authorize(AuthenticationSchemes = "Bearer")]
public class AutoUpdateControler : ControllerBase
{
    private IUpdateZipFileHostedService _updateZipFileService;
    public AutoUpdateControler(IUpdateZipFileHostedService updateZipFileService)
    {
        _updateZipFileService = updateZipFileService;
    }
    [HttpPost("add")]
    public async Task Add(UpdateZipFileAddInput1 input1)
    {
        await _updateZipFileService.SaveUpdateZipFile(input1).ConfigureAwait(false);
    }
}
