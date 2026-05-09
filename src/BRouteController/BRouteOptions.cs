namespace BRouteController;

public class BRouteOptions
{
    /// <summary>
    /// 使用するネットワークインタフェース名。
    /// "auto" を指定すると自動検出を行います。
    /// </summary>
    public string NetworkInterfaceName { get; set; } = "auto";

    /// <summary>
    /// 瞬時値ポーリング間隔
    /// </summary>
    public TimeSpan InstantaneousValueInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan PropertyReadTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int PropertyReadMaxRetryAttempts { get; set; } = 2;
    public TimeSpan PropertyReadRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public bool ContinuePollingOnError { get; set; } = true;
}
