//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------

using TouchSocket.Dmtp.Rpc;
using TouchSocket.Rpc;

namespace ThingsGateway.Management;

public partial class UpgradeRpcServer : SingletonRpcServer
{
    [DmtpRpc(MethodInvoke = true)]
    public void Restart()
    {
        RestartServerHelper.RestartServer();
    }
    [DmtpRpc(MethodInvoke = true)]
    public async Task Upgrade()
    {
        var _updateZipFileService = App.GetService<IUpdateZipFileHostedService>();
        var data = await _updateZipFileService.GetList().ConfigureAwait(false);
        if (data.Count != 0)
            await _updateZipFileService.Update(data.OrderByDescending(a => a.Version).FirstOrDefault()).ConfigureAwait(false);
    }
}
