namespace SmartMeterEmulator;

public class EmulatorOptions
{
    /// <summary>スマートメーターと通信するネットワークインターフェース名</summary>
    public string NetworkInterfaceName { get; set; } = "auto";

    /// <summary>エミュレーション開始時の積算電力量（正方向）kWh</summary>
    public decimal InitialCumulativeForwardKWh { get; set; } = 5000.0m;

    /// <summary>エミュレーション開始時の積算電力量（逆方向）kWh</summary>
    public decimal InitialCumulativeReverseKWh { get; set; } = 0.0m;

    /// <summary>基準瞬時電力 W</summary>
    public int InstantaneousPowerW { get; set; } = 1000;

    /// <summary>基準瞬時電流 R相 A</summary>
    public decimal CurrentRA { get; set; } = 5.0m;

    /// <summary>基準瞬時電流 T相 A</summary>
    public decimal CurrentTA { get; set; } = 5.0m;

    /// <summary>製造番号（ASCII 最大 12 文字）</summary>
    public string SerialNumber { get; set; } = "EMULATOR001";
}
