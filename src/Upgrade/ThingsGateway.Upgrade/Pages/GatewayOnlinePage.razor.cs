//------------------------------------------------------------------------------
//  此代码版权声明为全文件覆盖，如有原作者特别声明，会在下方手动补充
//  此代码版权（除特别声明外的代码）归作者本人Diego所有
//  源代码使用协议遵循本仓库的开源协议及附加协议
//  Gitee源代码仓库：https://gitee.com/diego2098/ThingsGateway
//  Github源代码仓库：https://github.com/kimdiego2098/ThingsGateway
//  使用文档：https://thingsgateway.cn/
//  QQ群：605534569
//------------------------------------------------------------------------------
#pragma warning disable CA2007 // 考虑对等待的任务调用 ConfigureAwait

using Mapster;

using ThingsGateway.Admin.Application;

using TouchSocket.Dmtp;

namespace ThingsGateway.Upgrade;

public partial class GatewayOnlinePage
{
    [Inject]
    IStringLocalizer<ThingsGateway.Upgrade._Imports> UpgradeLocalizer { get; set; }
    private async Task OnUpgrade(TcpSessionClientDto tcpSessionClientDto)
    {
        try
        {
            await FileHostService.Updrade(tcpSessionClientDto.Id, default);
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            await ToastService.Warn(ex);
        }
    }
    private async Task OnRestart(TcpSessionClientDto tcpSessionClientDto)
    {
        try
        {
            await FileHostService.Restart(tcpSessionClientDto.Id, default);
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            await ToastService.Warn(ex);
        }
    }


    [Inject]
    private ToastService ToastService { get; set; }
    [Inject]
    private IFileHostService FileHostService { get; set; }

    public TcpSessionClientDto SearchModel { get; set; } = new TcpSessionClientDto();

    private Task<QueryData<TcpSessionClientDto>> OnQueryAsync(QueryPageOptions options)
    {
        if (TcpDmtpService != null)
        {
            var clients = TcpDmtpService.Clients.ToList();
            var data = clients.Adapt<List<TcpSessionClientDto>>();

            var query = data.GetQueryData(options);

            return Task.FromResult(query);
        }
        else
        {
            return Task.FromResult(new QueryData<TcpSessionClientDto>());
        }
    }

    public TcpDmtpService? TcpDmtpService => FileHostService.TcpDmtpService;
}

public class TcpSessionClientDto
{
    [AutoGenerateColumn(Searchable = true, Filterable = true, Sortable = true)]
    public string Id { get; set; }

    [AutoGenerateColumn(Searchable = true, Filterable = true, Sortable = true)]
    public string IP { get; set; }

    [AutoGenerateColumn(Searchable = true, Filterable = true, Sortable = true)]
    public int Port { get; set; }


}
