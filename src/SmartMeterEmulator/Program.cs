using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.Options;
using SmartMeterEmulator;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<EmulatorOptions>(
    builder.Configuration.GetSection("Emulator"));

// WiFiLowerLayerClient: Options から NetworkInterfaceName を解決して構築
builder.Services.AddSingleton<WiFiLowerLayerClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<EmulatorOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<WiFiLowerLayerClient>>();
    return new WiFiLowerLayerClient(logger, opts.NetworkInterfaceName);
});

builder.Services.AddSingleton<MeterSimulator>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<EmulatorOptions>>().Value;
    return new MeterSimulator(opts);
});

builder.Services.AddSingleton<EchonetServer>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<EmulatorOptions>>().Value;
    return new EchonetServer(
        sp.GetRequiredService<ILogger<EchonetServer>>(),
        sp.GetRequiredService<WiFiLowerLayerClient>(),
        sp.GetRequiredService<MeterSimulator>(),
        opts);
});

builder.Services.AddHostedService<EmulatorWorker>();

await builder.Build().RunAsync();
