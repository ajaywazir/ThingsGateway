using Microsoft.Extensions.Configuration;

using ThingsGateway.ConfigurableOptions;
using ThingsGateway.NewLife.Extension;

namespace ThingsGateway.Upgrade;

public class UpgradeServerOptions : IConfigurableOptions<UpgradeServerOptions>
{
    public string UpgradeServerIP { get; set; }
    public int UpgradeServerPort { get; set; }
    public string VerifyToken { get; set; } = "ThingsGateway";
    public bool Enable { get; set; }
    public string Id { get; set; } = "ThingsGateway";

    public void PostConfigure(UpgradeServerOptions options, IConfiguration configuration)
    {
        if (options.VerifyToken.IsNullOrEmpty())
        {
            options.VerifyToken = "ThingsGateway";
        }
        if (options.Id.IsNullOrEmpty())
        {
            options.Id = "ThingsGateway";
        }
    }
}
