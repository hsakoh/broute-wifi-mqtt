using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartMeterEmulator;

/// <summary>
/// スマートメーターエミュレーターのメインワーカー。
/// WiFi UDP を起動し、起動通知・定期通知・瞬時値更新を担う。
/// </summary>
public class EmulatorWorker(
    ILogger<EmulatorWorker> logger,
    WiFiLowerLayerClient wifiClient,
    EchonetServer echonetServer,
    MeterSimulator simulator,
    IOptions<EmulatorOptions> options) : BackgroundService
{
    private readonly EmulatorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("スマートメーターエミュレーターを起動します");

        // WiFi UDP リスナーを開始
        wifiClient.Start();

        // 起動時にインスタンスリスト通知をマルチキャスト送信
        await echonetServer.SendInstanceListNotificationAsync();

        // 3 つのタスクを並行して動かす:
        //  1. 毎分: 瞬時値更新 + インスタンスリスト再通知（コントローラ起動を待つため）
        //  2. 30 分毎: 定時積算電力量通知
        //  3. 起動直後に遅延インスタンスリスト再通知（コントローラが後から起動するケース対応）

        var minuteTask = RunMinuteLoopAsync(stoppingToken);
        var halfHourTask = RunHalfHourLoopAsync(stoppingToken);
        var renotifyTask = RenotifyAfterDelayAsync(stoppingToken);

        await Task.WhenAll(minuteTask, halfHourTask, renotifyTask);
    }

    /// <summary>毎分: 瞬時値シミュレーション更新</summary>
    private async Task RunMinuteLoopAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(ct))
        {
            simulator.Update();
            logger.LogDebug("瞬時値を更新しました");
        }
    }

    /// <summary>30 分毎: 定時積算電力量通知</summary>
    private async Task RunHalfHourLoopAsync(CancellationToken ct)
    {
        // 最初の 30 分境界まで待機
        var now = DateTime.Now;
        var next = now.AddMinutes(30 - (now.Minute % 30)).AddSeconds(-now.Second);
        var wait = next - now;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, ct);

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await echonetServer.SendScheduledCumulativeNotificationAsync();
        }
    }

    /// <summary>
    /// コントローラが後から起動するケースに備え、
    /// 5 秒・30 秒・60 秒後にインスタンスリスト通知を再送する。
    /// </summary>
    private async Task RenotifyAfterDelayAsync(CancellationToken ct)
    {
        foreach (var delay in new[] { 5, 30, 60 })
        {
            await Task.Delay(TimeSpan.FromSeconds(delay), ct);
            logger.LogDebug("{Delay}秒後の再通知を送信します", delay);
            await echonetServer.SendInstanceListNotificationAsync();
        }
    }

    public override void Dispose()
    {
        wifiClient.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
