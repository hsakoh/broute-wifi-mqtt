using BRouteController;
using EchoDotNetLite;
using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class BRouteControllerDependencyInjectionExtensions
{
    public static IServiceCollection AddBRouteController(this IServiceCollection services)
    {
        services.AddOptions<BRouteController.BRouteOptions>().Configure<IConfiguration>((settings, configuration) =>
        {
            configuration.GetSection("BRoute").Bind(settings);
        });

        // WiFiBridgeOptions を BRoute の設定から登録
        services.AddOptions<WiFiBridgeOptions>().Configure<IConfiguration>((settings, configuration) =>
        {
            // BRoute セクションの NetworkInterfaceName を共有
            var brouteSection = configuration.GetSection("BRoute");
            settings.NetworkInterfaceName = brouteSection["NetworkInterfaceName"] ?? "auto";
        });

        // WiFiBridge を登録
        services.AddEchoDotNetLiteWiFiBridge();

        // EchoClient を登録 (WiFiPANAClient を IPANAClient として使用)
        services.AddSingleton<EchoClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EchoClient>>();
            var panaClient = sp.GetRequiredService<WiFiLowerLayerClient>();
            return new EchoClient(logger, panaClient);
        });

        services.AddSingleton<BRouteControllerService>();
        return services;
    }
}
