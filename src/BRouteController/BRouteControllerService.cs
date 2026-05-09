using EchoDotNetLite;
using EchoDotNetLite.Common;
using EchoDotNetLite.Models;
using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BRouteController;

public class BRouteControllerService : IDisposable
{
    private readonly ILogger<BRouteControllerService> _logger;
    private readonly IOptionsMonitor<BRouteOptions> _optionsMonitor;
    private readonly WiFiLowerLayerClient _wifiClient;
    private readonly EchoClient _echoClient;
    private readonly SemaphoreSlim _semaphore;

    // IPv6 アドレス（スコープID付き文字列）→ メーターインスタンス
    public ConcurrentDictionary<string, 低圧スマート電力量メータ> Meters { get; } = new();

    public event EventHandler<低圧スマート電力量メータ>? OnMeterDiscovered;
    public event EventHandler<低圧スマート電力量メータ>? OnActiveDataUpdated;
    public event EventHandler<低圧スマート電力量メータ>? OnPassiveDataUpdated;
    public event EventHandler<低圧スマート電力量メータ>? OnPassiveOnTimeDataUpdated;
    public event EventHandler<低圧スマート電力量メータ>? OnPassive1MinDataUpdated;

    public BRouteControllerService(
        ILogger<BRouteControllerService> logger,
        IOptionsMonitor<BRouteOptions> optionsMonitor,
        WiFiLowerLayerClient wifiClient,
        EchoClient echoClient)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _wifiClient = wifiClient;
        _echoClient = echoClient;
        _semaphore = new SemaphoreSlim(1, 1);

        _echoClient.OnNodeJoined += OnNodeJoined;

        // コントローラとしてふるまう
        _echoClient.SelfNode.Devices.Add(
            new EchoObjectInstance(
                EchoDotNetLite.Specifications.機器.管理操作関連機器.コントローラ, 0x01));
    }

    public void Dispose()
    {
        _logger.LogTrace("Dispose");
        GC.SuppressFinalize(this);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        // 自ノードのリンクローカルアドレスを取得
        var selfAddress = GetSelfLinkLocalAddress(_optionsMonitor.CurrentValue.NetworkInterfaceName, _logger);
        _logger.LogInformation("自ノード IPv6 アドレス: {Address}", selfAddress);

        // EchoClient を初期化（自ノードアドレスを設定）
        _echoClient.Initialize(selfAddress);

        // インスタンスリスト通知（自コントローラの存在をマルチキャストで通知）
        await _echoClient.インスタンスリスト通知Async();

        // スマートメーターからの一斉同報を待機する
        // スマートメーターはインスタンスリスト通知を受信すると応答する
        _logger.LogInformation("スマートメーターからの通知を待機中...");
    }

    public async Task PollAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(_optionsMonitor.CurrentValue.InstantaneousValueInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var meter in Meters.Values)
            {
                await ReadActivePropertiesForMeterAsync(meter, _optionsMonitor.CurrentValue.ContinuePollingOnError);
            }
        }
    }

    public async Task ReadActivePropertiesForMeterAsync(低圧スマート電力量メータ meter, bool continueOnError = false)
    {
        var node = meter.EchoNode;
        var device = meter.EchoObjectInstance;
        await _semaphore.WaitAsync();
        try
        {
            {
                var target = new byte[] { 0x97, 0x98, 0xD3, 0xE1, 0xD7 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
            {
                var target = new byte[] { 0xE7, 0xE8 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プロパティ値読み出しで例外 [{Address}]", node.Address);
            if (!continueOnError)
                throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ReadPassive1MinPropertiesForMeterAsync(低圧スマート電力量メータ meter)
    {
        var node = meter.EchoNode;
        var device = meter.EchoObjectInstance;
        await _semaphore.WaitAsync();
        try
        {
            {
                var target = new byte[] { 0x97, 0x98, 0xD3, 0xE1, 0xD7 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
            {
                var target = new byte[] { 0xD0 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プロパティ値読み出しで例外 [{Address}]", node.Address);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ReadPassivePropertiesForMeterAsync(低圧スマート電力量メータ meter)
    {
        var node = meter.EchoNode;
        var device = meter.EchoObjectInstance;
        await _semaphore.WaitAsync();
        try
        {
            {
                var target = new byte[] { 0x97, 0x98, 0xD3, 0xE1, 0xD7 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
            {
                var target = new byte[] { 0xE0, 0xE3 };
                var properties = device.GETProperties.Where(p => target.Contains(p.Spec.Code));
                await ReadPropertyWithRetry(node, device, properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プロパティ値読み出しで例外 [{Address}]", node.Address);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ReadPropertyWithRetry(EchoNode node, EchoObjectInstance device, IEnumerable<EchoPropertyInstance> properties)
    {
        (bool, List<PropertyRequest>)? readResult = null;
        for (var count = 0; count <= _optionsMonitor.CurrentValue.PropertyReadMaxRetryAttempts; count++)
        {
            try
            {
                readResult = await _echoClient.プロパティ値読み出し(
                    _echoClient.SelfNode.Devices.First(),
                    node, device, properties,
                    (int)_optionsMonitor.CurrentValue.PropertyReadTimeout.TotalMilliseconds);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Delay} 後にプロパティ値読み出しを再試行します", _optionsMonitor.CurrentValue.PropertyReadRetryDelay);
                await Task.Delay(_optionsMonitor.CurrentValue.PropertyReadRetryDelay);
            }
        }
        if (readResult == null)
        {
            _logger.LogWarning("プロパティ値読み出し リトライオーバー");
            throw new ApplicationException("プロパティ値読み出し リトライオーバー");
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task InitializeMeterAsync(EchoNode node, CancellationToken ct = default)
    {
        _logger.LogDebug("メーター [{Address}] デバイス検出待機中", node.Address);
        while (!node.Devices.Any())
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogDebug("デバイス検出待機中 [{Address}]", node.Address);
            await Task.Delay(2_000, ct);
        }

        var device = node.Devices.First();

        // スマートメーター以外の機器は対象外として早期リターン
        if (device.Spec != EchoDotNetLite.Specifications.機器.住宅設備関連機器.低圧スマート電力量メータ)
        {
            _logger.LogInformation(
                "対象外の機器を検出しました。スキップします [{Address}] ClassGroup={ClassGroup} Class={Class}",
                node.Address,
                device.Spec.ClassGroup.ClassGroupCode,
                device.Spec.Class.ClassCode);
            return;
        }

        _logger.LogDebug("メーター [{Address}] プロパティマップ読み込み待機中", node.Address);
        while (!device.IsPropertyMapGet)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogDebug("プロパティマップ読み込み待機中 [{Address}]", node.Address);
            await Task.Delay(2_000, ct);
        }

        _logger.LogInformation("メーター [{Address}] GETプロパティ一括取得", node.Address);
        foreach (var prop in device.GETProperties)
        {
            await ReadPropertyWithRetry(node, device, [prop]);
        }

        var meter = new 低圧スマート電力量メータ(node, device);
        if (Meters.TryAdd(node.Address, meter))
        {
            _logger.LogInformation("メーター登録: [{Address}] 製造番号={Serial}", node.Address, meter.製造番号);
            OnMeterDiscovered?.Invoke(this, meter);
        }
    }

    private void OnNodeJoined(object? sender, EchoNode e)
    {
        _logger.LogInformation("EchoNode 検出: {Address}", e.Address);
        e.OnCollectionChanged += OnEchoObjectChange;

        // メーターの初期化を非同期で実行
        _ = Task.Run(async () =>
        {
            try
            {
                await InitializeMeterAsync(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "メーター [{Address}] の初期化に失敗しました", e.Address);
            }
        });
    }

    private void OnEchoObjectChange(object? sender, (CollectionChangeType type, EchoObjectInstance instance) e)
    {
        switch (e.type)
        {
            case CollectionChangeType.Add:
                _logger.LogInformation("EchoObject Add {Object}", e.instance.GetDebugString());
                e.instance.OnCollectionChanged += OnEchoPropertyChange;
                break;
            case CollectionChangeType.Remove:
                _logger.LogInformation("EchoObject Remove {Object}", e.instance.GetDebugString());
                break;
        }
    }

    private void OnEchoPropertyChange(object? sender, (CollectionChangeType type, EchoPropertyInstance instance) e)
    {
        switch (e.type)
        {
            case CollectionChangeType.Add:
                _logger.LogInformation("EchoProperty Add {Property}", e.instance.GetDebugString());
                e.instance.ValueChanged += OnEchoPropertyValueChanged;
                break;
            case CollectionChangeType.Remove:
                _logger.LogInformation("EchoProperty Remove {Property}", e.instance.GetDebugString());
                break;
        }
    }

    private void OnEchoPropertyValueChanged(object? sender, byte[] e)
    {
        if (sender is not EchoPropertyInstance echoPropertyInstance)
            return;

        _logger.LogInformation("EchoProperty Change {Property}", echoPropertyInstance.GetDebugString());

        // どのメーターのプロパティか特定
        低圧スマート電力量メータ? meter = null;
        foreach (var m in Meters.Values)
        {
            if (m.EchoObjectInstance.GETProperties.Contains(echoPropertyInstance))
            {
                meter = m;
                break;
            }
        }

        if (meter == null) return;

        var code = echoPropertyInstance.Spec.Code;
        if (code == 0xE0 || code == 0xE3)
        {
            Task.Run(() => OnPassiveDataUpdated?.Invoke(this, meter));
        }
        else if (code == 0xEA || code == 0xEB)
        {
            Task.Run(() => OnPassiveOnTimeDataUpdated?.Invoke(this, meter));
        }
        else if (code == 0xD0)
        {
            Task.Run(() => OnPassive1MinDataUpdated?.Invoke(this, meter));
        }
        else if (code == 0xE7 || code == 0xE8)
        {
            Task.Run(() => OnActiveDataUpdated?.Invoke(this, meter));
        }
    }

    private static string GetSelfLinkLocalAddress(string networkInterfaceName, ILogger? logger = null)
    {
        var ni = EchoDotNetLiteWiFiBridge.NetworkInterfaceResolver.Resolve(networkInterfaceName, logger);

        var linkLocal = ni.GetIPProperties().UnicastAddresses
            .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                      && ua.Address.IsIPv6LinkLocal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"インタフェース '{ni.Name}' にリンクローカルアドレスがありません。");

        return linkLocal.Address.ToString();
    }
}
