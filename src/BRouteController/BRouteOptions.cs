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

    /// <summary>
    /// プロパティ値読み出しのタイムアウト
    /// </summary>
    public TimeSpan PropertyReadTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// プロパティ値読み出しの最大再試行回数
    /// </summary>
    public int PropertyReadMaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// プロパティ値読み出し失敗時の再試行間隔
    /// </summary>
    public TimeSpan PropertyReadRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// プロパティ読み出し後のインターバル（連続リクエストの抑制）
    /// </summary>
    public TimeSpan PropertyReadIntervalDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// ポーリング中にタイムアウト等のエラーが発生した場合でも処理を継続するかどうか
    /// </summary>
    public bool ContinuePollingOnError { get; set; } = true;
}
