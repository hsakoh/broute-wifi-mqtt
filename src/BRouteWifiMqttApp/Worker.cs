using BRouteController;
using HomeAssistantAddOn.Mqtt;
using System.Reflection;

namespace BRouteWifiMqttApp;

public class Worker(
    ILogger<Worker> logger,
    BRouteControllerService bRouteControllerService,
    MqttService mqttService
) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await mqttService.StartAsync();

        // メーター検出イベントを購読
        bRouteControllerService.OnMeterDiscovered += async (_, meter) =>
        {
            await PublishDeviceConfigAsync(meter);
            // HA がエンティティを登録し状態トピックへのサブスクライブを完了するまで待機する。
            // Config 送信直後に状態を送ると HA がまだサブスクライブしていないため受け取れない。
            await Task.Delay(TimeSpan.FromSeconds(3));
            await PublishDeviceStaticStatusAsync(meter);
            await PublishDeviceActiveStatusAsync(meter);
            await PublishDevicePassiveStatusAsync(meter);
            await PublishDevicePassive1MinStatusAsync(meter);
            SubscribeCommandTopic(meter);
        };
        bRouteControllerService.OnActiveDataUpdated += async (_, meter) =>
            await PublishDeviceActiveStatusAsync(meter);
        bRouteControllerService.OnPassiveDataUpdated += async (_, meter) =>
            await PublishDevicePassiveStatusAsync(meter);
        bRouteControllerService.OnPassiveOnTimeDataUpdated += async (_, meter) =>
            await PublishDevicePassiveOnTimeStatusAsync(meter);
        bRouteControllerService.OnPassive1MinDataUpdated += async (_, meter) =>
            await PublishDevicePassive1MinStatusAsync(meter);

        await bRouteControllerService.InitializeAsync(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bRouteControllerService.PollAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await mqttService.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    #region HA Device Discovery

    private static string AppVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private async Task PublishDeviceConfigAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        var passiveTopic = $"broute/{serial}/state/passive";
        var passive1MinTopic = $"broute/{serial}/state/passive_1min";
        var activeTopic = $"broute/{serial}/state/active";
        var staticTopic = $"broute/{serial}/state/static";
        var cmdTopic = $"broute/{serial}/cmd";

        var payload = new
        {
            dev = new
            {
                ids = new[] { $"smart_meter_{serial}" },
                name = $"低圧スマート電力量メータ ({serial})",
                mf = meter.メーカコード ?? "",
                sn = serial,
            },
            o = new
            {
                name = "broute-wifi-mqtt",
                sw = AppVersion,
                url = "https://github.com/hsakoh/broute-wifi-mqtt",
            },
            cmps = new Dictionary<string, object>
            {
                ["placement"] = BuildSensorComponent(serial, "placement", "設置場所", staticTopic, icon: "mdi:map-marker"),
                ["version"] = BuildSensorComponent(serial, "version", "規格Version情報", staticTopic, icon: "mdi:information"),
                ["makercode"] = BuildSensorComponent(serial, "makercode", "メーカコード", staticTopic, icon: "mdi:factory"),
                ["serialnumber"] = BuildSensorComponent(serial, "serialnumber", "製造番号", staticTopic, icon: "mdi:identifier"),                ["b_route_id"] = BuildSensorComponent(serial, "b_route_id", "Bルート識別番号", staticTopic, icon: "mdi:identifier"),
                ["cumulative_1min_normal"] = BuildSensorComponent(serial, "cumulative_1min_normal", "1分積算電力量計測値(正方向)", passive1MinTopic,
                    device_class: "energy", state_class: "total_increasing", unit_of_measurement: "kWh"),
                ["cumulative_1min_reverse"] = BuildSensorComponent(serial, "cumulative_1min_reverse", "1分積算電力量計測値(逆方向)", passive1MinTopic,
                    device_class: "energy", state_class: "total_increasing", unit_of_measurement: "kWh"),
                ["cumulative_1min_timestamp"] = BuildSensorComponent(serial, "cumulative_1min_timestamp", "更新日時(1分積算電力量)", passive1MinTopic,
                    device_class: "timestamp",
                    value_template: "{% set ts = value_json.get('timestamp', {}) %} {% if ts %}\n  {{ (ts / 1000) | timestamp_local | as_datetime }}\n{% else %}\n  {{ this.state }}\n{% endif %}"),
                ["btn_1min"] = BuildButtonComponent(serial, "1min", "1分積算電力量の取得", cmdTopic),                ["cumulative_normal"] = BuildSensorComponent(serial, "cumulative_normal", "積算電力量計測値(正方向)", passiveTopic,
                    device_class: "energy", state_class: "total_increasing", unit_of_measurement: "kWh"),
                ["cumulative_reverse"] = BuildSensorComponent(serial, "cumulative_reverse", "積算電力量計測値(逆方向)", passiveTopic,
                    device_class: "energy", state_class: "total_increasing", unit_of_measurement: "kWh"),
                ["passive_timestamp"] = BuildSensorComponent(serial, "passive_timestamp", "更新日時(積算電力量)", passiveTopic,
                    device_class: "timestamp",
                    value_template: "{% set ts = value_json.get('timestamp', {}) %} {% if ts %}\n  {{ (ts / 1000) | timestamp_local | as_datetime }}\n{% else %}\n  {{ this.state }}\n{% endif %}"),
                ["instantaneous_current_r"] = BuildSensorComponent(serial, "instantaneous_current_r", "瞬時電流計測値(R相)", activeTopic,
                    device_class: "current", state_class: "measurement", unit_of_measurement: "A"),
                ["instantaneous_current_t"] = BuildSensorComponent(serial, "instantaneous_current_t", "瞬時電流計測値(T相)", activeTopic,
                    device_class: "current", state_class: "measurement", unit_of_measurement: "A"),
                ["instantaneous_electric_power"] = BuildSensorComponent(serial, "instantaneous_electric_power", "瞬時電力計測値", activeTopic,
                    device_class: "power", state_class: "measurement", unit_of_measurement: "W"),
                ["active_timestamp"] = BuildSensorComponent(serial, "active_timestamp", "更新日時(瞬時値)", activeTopic,
                    device_class: "timestamp",
                    value_template: "{% set ts = value_json.get('timestamp', {}) %} {% if ts %}\n  {{ (ts / 1000) | timestamp_local | as_datetime }}\n{% else %}\n  {{ this.state }}\n{% endif %}"),
                ["btn_active"] = BuildButtonComponent(serial, "active", "瞬時値の取得", cmdTopic),
                ["btn_passive"] = BuildButtonComponent(serial, "passive", "積算電力量の取得", cmdTopic),
            },
        };

        await mqttService.PublishAsync($"homeassistant/device/{serial}/config", payload, true);
        logger.LogInformation("Device Discovery 登録完了: {Serial}", serial);
    }

    private static object BuildSensorComponent(
        string serial, string type, string name, string stateTopic,
        string? icon = null,
        string? device_class = null,
        string? state_class = null,
        string? unit_of_measurement = null,
        string? value_template = null) => new
    {
        p = "sensor",
        name,
        icon,
        device_class,
        state_class,
        unit_of_measurement,
        state_topic = stateTopic,
        value_template = value_template ?? $"{{{{value_json.{type}}}}}",
        unique_id = $"{type}_{serial}",
    };

    private static object BuildButtonComponent(
        string serial, string type, string name, string cmdTopic) => new
    {
        p = "button",
        name,
        command_topic = cmdTopic,
        payload_press = type,
        unique_id = $"btn_{type}_{serial}",
    };

    #endregion

    #region Status Publishing

    private async Task PublishDeviceStaticStatusAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        await mqttService.PublishAsync($"broute/{serial}/state/static", new
        {
            placement = meter.設置場所,
            version = meter.規格Version情報,
            makercode = meter.メーカコード,
            serialnumber = serial,            b_route_id = meter.Bルート識別番号,        }, true);
        logger.LogInformation("ステータス(静的)通知 {Serial}", serial);
    }

    private async Task PublishDeviceActiveStatusAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        await mqttService.PublishAsync($"broute/{serial}/state/active", new
        {
            instantaneous_current_r = meter.瞬時電流計測値?.r,
            instantaneous_current_t = meter.瞬時電流計測値?.t,
            instantaneous_electric_power = meter.瞬時電力計測値,
            timestamp = meter.現在年月日時刻,
        }, true);
        logger.LogInformation("ステータス(瞬時)通知 {Serial} {r}A {t}A {e}W",
            serial,
            meter.瞬時電流計測値?.r,
            meter.瞬時電流計測値?.t,
            meter.瞬時電力計測値);
    }

    private async Task PublishDevicePassiveStatusAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        await mqttService.PublishAsync($"broute/{serial}/state/passive", new
        {
            cumulative_normal = meter.積算電力量計測値_正方向計測値,
            cumulative_reverse = meter.積算電力量計測値_逆方向計測値,
            timestamp = meter.現在年月日時刻,
        }, true);
        logger.LogInformation("ステータス(積算)通知 {Serial} {n}kWh {r}kWh",
            serial,
            meter.積算電力量計測値_正方向計測値,
            meter.積算電力量計測値_逆方向計測値);
    }

    private async Task PublishDevicePassive1MinStatusAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        var data = meter.一分積算電力量計測値;
        await mqttService.PublishAsync($"broute/{serial}/state/passive_1min", new
        {
            cumulative_1min_normal = data?.normalKWh,
            cumulative_1min_reverse = data?.reverseKWh,
            timestamp = data?.datetime,
        }, true);
        logger.LogInformation("ステータス(1分積算)通知 {Serial} {n}kWh {r}kWh",
            serial,
            data?.normalKWh,
            data?.reverseKWh);
    }

    private async Task PublishDevicePassiveOnTimeStatusAsync(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        await mqttService.PublishAsync($"broute/{serial}/state/passive", new
        {
            cumulative_normal = meter.定時積算電力量計測値_正方向計測値?.kWh,
            cumulative_reverse = meter.定時積算電力量計測値_逆方向計測値?.kWh,
            timestamp = meter.定時積算電力量計測値_逆方向計測値?.datetime,
        }, true);
        logger.LogInformation("ステータス(積算-定時)通知 {Serial} {n}kWh {r}kWh",
            serial,
            meter.定時積算電力量計測値_正方向計測値?.kWh,
            meter.定時積算電力量計測値_逆方向計測値?.kWh);
    }

    #endregion

    private void SubscribeCommandTopic(低圧スマート電力量メータ meter)
    {
        var serial = meter.製造番号!;
        mqttService.Subscribe($"broute/{serial}/cmd", async (payload) =>
        {
            logger.LogInformation("コマンド受信: {Serial} {Payload}", serial, payload);
            switch (payload?.Trim())
            {
                case "active":
                    await bRouteControllerService.ReadActivePropertiesForMeterAsync(meter);
                    break;
                case "passive":
                    await bRouteControllerService.ReadPassivePropertiesForMeterAsync(meter);
                    break;
                case "1min":
                    await bRouteControllerService.ReadPassive1MinPropertiesForMeterAsync(meter);
                    break;
                default:
                    logger.LogWarning("不明なコマンド: {Payload}", payload);
                    break;
            }
        });
    }
}
