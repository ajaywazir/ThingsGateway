using Microsoft.Extensions.Hosting;

using ThingsGateway.Upgrade;

using TouchSocket.Dmtp;

namespace ThingsGateway.Management;

public interface IUpdateZipFileHostedService : IHostedService
{
    TextFileLogger TextLogger { get; }
    string LogPath { get; }
    TcpDmtpClient? TcpDmtpClient { get; set; }

    Task<List<UpdateZipFile>> GetList();
    Task Update(UpdateZipFile updateZipFile, Func<Task<bool>> check = null);
}