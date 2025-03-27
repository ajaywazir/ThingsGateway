using Microsoft.Extensions.Hosting;

using TouchSocket.Dmtp;

namespace ThingsGateway.Upgrade
{
    public interface IFileHostService : IHostedService
    {
        TcpDmtpService TcpDmtpService { get; set; }

        ValueTask Restart(string id, CancellationToken stoppingToken);
        ValueTask Updrade(string id, CancellationToken stoppingToken);
    }
}