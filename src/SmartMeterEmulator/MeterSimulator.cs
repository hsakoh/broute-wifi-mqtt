using System.Buffers.Binary;

namespace SmartMeterEmulator;

/// <summary>
/// スマートメーターの模擬データを保持・更新する。
/// </summary>
public class MeterSimulator
{
    /// <summary>係数（EPC=<see cref="Epc.低圧スマート電力量メータ.係数"/>=0xD3）: 固定値 1。積算電力量に乗じる値。</summary>
    private const uint Coefficient = 1;

    /// <summary>
    /// 積算電力量単位コード（EPC=<see cref="Epc.低圧スマート電力量メータ.積算電力量単位"/>=0xE1）: <see cref="Edt.積算電力量単位_0_01kWh"/>。
    /// <see cref="UnitValue"/>（0.01m）と対応する。
    /// </summary>
    private const byte UnitCode = Edt.積算電力量単位_0_01kWh;

    /// <summary>積算電力量単位の乗数: 0.01 kWh（<see cref="UnitCode"/>=0x02 に対応）</summary>
    private const decimal UnitValue = 0.01m;

    private decimal _cumulativeForwardKWh;
    private decimal _cumulativeReverseKWh;
    private int _instantaneousPowerW;
    private decimal _currentRA;
    private decimal _currentTA;
    private DateTimeOffset _lastScheduledTime;
    private decimal _scheduledForwardKWh;
    private decimal _scheduledReverseKWh;

    private readonly EmulatorOptions _options;

    public MeterSimulator(EmulatorOptions options)
    {
        _options = options;
        _cumulativeForwardKWh = options.InitialCumulativeForwardKWh;
        _cumulativeReverseKWh = options.InitialCumulativeReverseKWh;
        _instantaneousPowerW = options.InstantaneousPowerW;
        _currentRA = options.CurrentRA;
        _currentTA = options.CurrentTA;

        // 直近の30分境界を計算
        var now = DateTimeOffset.Now;
        var mins = now.Minute - (now.Minute % 30);
        _lastScheduledTime = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, mins, 0, now.Offset);
        _scheduledForwardKWh = _cumulativeForwardKWh;
        _scheduledReverseKWh = _cumulativeReverseKWh;
    }

    /// <summary>
    /// 瞬時値を更新し、積算電力量を加算する（毎分呼び出すことを想定）。
    /// </summary>
    public void Update()
    {
        // 毎分、瞬時電力分の積算電力量を加算（kWh）
        _cumulativeForwardKWh += _instantaneousPowerW / 1000m / 60m;

        // 瞬時値をわずかに変動させる（±15%）
        var jitter = 0.85 + Random.Shared.NextDouble() * 0.30;
        _instantaneousPowerW = (int)(_options.InstantaneousPowerW * jitter);
        _currentRA = _options.CurrentRA * (decimal)(0.85 + Random.Shared.NextDouble() * 0.30);
        _currentTA = _options.CurrentTA * (decimal)(0.85 + Random.Shared.NextDouble() * 0.30);
    }

    /// <summary>
    /// 定時積算電力量を更新する（30分毎に呼び出す）。
    /// </summary>
    public void UpdateScheduled()
    {
        _lastScheduledTime = DateTimeOffset.Now;
        _scheduledForwardKWh = _cumulativeForwardKWh;
        _scheduledReverseKWh = _cumulativeReverseKWh;
    }

    // ---- プロパティ値生成メソッド ----

    public byte[] GetCurrentTime()
    {
        var now = DateTimeOffset.Now;
        return [(byte)now.Hour, (byte)now.Minute];
    }

    public byte[] GetCurrentDate()
    {
        var now = DateTimeOffset.Now;
        var result = new byte[4];
        BinaryPrimitives.WriteInt16BigEndian(result, (short)now.Year);
        result[2] = (byte)now.Month;
        result[3] = (byte)now.Day;
        return result;
    }

    public byte[] GetCumulativeForward()
        => EncodeCumulative(_cumulativeForwardKWh);

    public byte[] GetCumulativeReverse()
        => EncodeCumulative(_cumulativeReverseKWh);

    public byte[] GetInstantaneousPower()
    {
        var result = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(result, _instantaneousPowerW);
        return result;
    }

    public byte[] GetInstantaneousCurrent()
    {
        // R相・T相各 int16 × 0.1A
        var result = new byte[4];
        BinaryPrimitives.WriteInt16BigEndian(result, (short)Math.Round(_currentRA * 10));
        BinaryPrimitives.WriteInt16BigEndian(result.AsSpan()[2..], (short)Math.Round(_currentTA * 10));
        return result;
    }

    public byte[] GetScheduledForward()
        => EncodeScheduledCumulative(_lastScheduledTime, _scheduledForwardKWh);

    public byte[] GetScheduledReverse()
        => EncodeScheduledCumulative(_lastScheduledTime, _scheduledReverseKWh);

    public byte[] Get1MinCumulative()
    {
        var now = DateTimeOffset.Now;
        var result = new byte[15];
        BinaryPrimitives.WriteInt16BigEndian(result, (short)now.Year);
        result[2] = (byte)now.Month;
        result[3] = (byte)now.Day;
        result[4] = (byte)now.Hour;
        result[5] = (byte)now.Minute;
        result[6] = (byte)now.Second;
        var fwdRaw = RawValue(_cumulativeForwardKWh);
        var revRaw = RawValue(_cumulativeReverseKWh);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan()[7..], fwdRaw);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan()[11..], revRaw);
        return result;
    }

    private static byte[] EncodeCumulative(decimal kWh)
    {
        var result = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(result, RawValue(kWh));
        return result;
    }

    private static byte[] EncodeScheduledCumulative(DateTimeOffset time, decimal kWh)
    {
        var result = new byte[11];
        BinaryPrimitives.WriteInt16BigEndian(result, (short)time.Year);
        result[2] = (byte)time.Month;
        result[3] = (byte)time.Day;
        result[4] = (byte)time.Hour;
        result[5] = (byte)time.Minute;
        result[6] = (byte)time.Second;
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan()[7..], RawValue(kWh));
        return result;
    }

    // kWh を生値（係数・単位適用後の uint）に変換
    private static uint RawValue(decimal kWh)
        => (uint)Math.Round(kWh / (Coefficient * UnitValue));
}
