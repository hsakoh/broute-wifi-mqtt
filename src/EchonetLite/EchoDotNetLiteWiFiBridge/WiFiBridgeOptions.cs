namespace EchoDotNetLiteWiFiBridge;

public class WiFiBridgeOptions
{
    /// <summary>
    /// 使用するネットワークインタフェース名 (例: "eth0", "wlan0")
    /// </summary>
    public string NetworkInterfaceName { get; set; } = default!;
}
