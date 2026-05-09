using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class WiFiBridgeDependencyInjectionExtensions
{
    public static IServiceCollection AddEchoDotNetLiteWiFiBridge(this IServiceCollection services)
    {
        services.AddSingleton<WiFiLowerLayerClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<WiFiBridgeOptions>>().CurrentValue;
            var logger = sp.GetRequiredService<ILogger<WiFiLowerLayerClient>>();
            var client = new WiFiLowerLayerClient(logger, options.NetworkInterfaceName);
            client.Start();
            return client;
        });
        return services;
    }
}
