namespace SmartMeterEmulator;

/// <summary>
/// ECHONET Lite の EOJ (ECHONET Object) クラスコード定数。
/// </summary>
internal static class Eoj
{
    /// <summary>住宅設備関連機器クラスグループコード (0x02)</summary>
    internal const byte 住宅設備関連機器クラスグループ = 0x02;

    /// <summary>低圧スマート電力量メータ クラスコード (0x88)</summary>
    internal const byte 低圧スマート電力量メータ = 0x88;

    /// <summary>プロファイルクラスグループコード (0x0E)</summary>
    internal const byte プロファイルクラスグループ = 0x0E;

    /// <summary>ノードプロファイル クラスコード (0xF0)</summary>
    internal const byte ノードプロファイル = 0xF0;
}

/// <summary>
/// ECHONET Lite の EPC (ECHONET Property Code) 定数。
/// </summary>
internal static class Epc
{
    /// <summary>
    /// 機器オブジェクトスーパークラスの共通プロパティコード。
    /// すべての機器クラスが持つ。
    /// </summary>
    internal static class 共通
    {
        /// <summary>動作状態 (EPC=0x80)</summary>
        internal const byte 動作状態 = 0x80;

        /// <summary>規格Version情報 (EPC=0x82)</summary>
        internal const byte 規格Version情報 = 0x82;

        /// <summary>メーカコード (EPC=0x8A): 3バイト、メーカ固有コード</summary>
        internal const byte メーカコード = 0x8A;

        /// <summary>状変アナウンスプロパティマップ (EPC=0x9D): 自発通知するプロパティの一覧</summary>
        internal const byte 状変アナウンスプロパティマップ = 0x9D;

        /// <summary>Set プロパティマップ (EPC=0x9E): 書き込み可能なプロパティの一覧</summary>
        internal const byte Setプロパティマップ = 0x9E;

        /// <summary>Get プロパティマップ (EPC=0x9F): 読み出し可能なプロパティの一覧</summary>
        internal const byte Getプロパティマップ = 0x9F;
    }

    /// <summary>
    /// 低圧スマート電力量メータ クラス固有プロパティコード (クラスコード 0x88)。
    /// </summary>
    internal static class 低圧スマート電力量メータ
    {
        /// <summary>設置場所 (EPC=0x81): 1バイト、設置場所コード</summary>
        internal const byte 設置場所 = 0x81;

        /// <summary>製造番号 (EPC=0x8D): 12バイト ASCII</summary>
        internal const byte 製造番号 = 0x8D;

        /// <summary>現在時刻設定 (EPC=0x97): 2バイト [HH, mm]</summary>
        internal const byte 現在時刻 = 0x97;

        /// <summary>現在年月日設定 (EPC=0x98): 4バイト [YYYY(2B), MM, DD]</summary>
        internal const byte 現在年月日 = 0x98;

        /// <summary>Bルート識別番号 (EPC=0xC0): 8バイト</summary>
        internal const byte Bルート識別番号 = 0xC0;

        /// <summary>
        /// 1分積算電力量計測値 (EPC=0xD0):
        /// 15バイト [YYYY(2B), MM, DD, HH, mm, SS, 正方向(4B), 逆方向(4B)]
        /// </summary>
        internal const byte 一分積算電力量計測値 = 0xD0;

        /// <summary>係数 (EPC=0xD3): 4バイト uint、積算電力量に乗じる係数</summary>
        internal const byte 係数 = 0xD3;

        /// <summary>積算電力量有効桁数 (EPC=0xD7): 1バイト、有効桁数 (1〜6)</summary>
        internal const byte 積算電力量有効桁数 = 0xD7;

        /// <summary>積算電力量計測値（正方向計測値）(EPC=0xE0): 4バイト uint</summary>
        internal const byte 積算電力量計測値_正方向 = 0xE0;

        /// <summary>
        /// 積算電力量単位 (EPC=0xE1): 1バイト。
        /// 0x00=1kWh, 0x01=0.1kWh, 0x02=0.01kWh, 0x03=0.001kWh, 0x04=0.0001kWh,
        /// 0x0A=10kWh, 0x0B=100kWh, 0x0C=1000kWh, 0x0D=10000kWh
        /// </summary>
        internal const byte 積算電力量単位 = 0xE1;

        /// <summary>積算電力量計測値（逆方向計測値）(EPC=0xE3): 4バイト uint</summary>
        internal const byte 積算電力量計測値_逆方向 = 0xE3;

        /// <summary>瞬時電力計測値 (EPC=0xE7): 4バイト int32 [W]</summary>
        internal const byte 瞬時電力計測値 = 0xE7;

        /// <summary>
        /// 瞬時電流計測値 (EPC=0xE8): 4バイト [R相 int16, T相 int16]。
        /// 値は 0.1A 単位（例: 50 → 5.0A）
        /// </summary>
        internal const byte 瞬時電流計測値 = 0xE8;

        /// <summary>
        /// 定時積算電力量計測値（正方向計測値）(EPC=0xEA):
        /// 11バイト [YYYY(2B), MM, DD, HH, mm, SS, 積算値(4B)]。
        /// 30分毎に自発通知される。
        /// </summary>
        internal const byte 定時積算電力量計測値_正方向 = 0xEA;

        /// <summary>
        /// 定時積算電力量計測値（逆方向計測値）(EPC=0xEB):
        /// 11バイト [YYYY(2B), MM, DD, HH, mm, SS, 積算値(4B)]。
        /// 30分毎に自発通知される。
        /// </summary>
        internal const byte 定時積算電力量計測値_逆方向 = 0xEB;
    }

    /// <summary>
    /// ノードプロファイル クラス固有プロパティコード (クラスコード 0xF0)。
    /// </summary>
    internal static class ノードプロファイル
    {
        /// <summary>識別番号 (EPC=0x83): 12バイト [0xFE, メーカコード(3B), 固有番号(8B)]</summary>
        internal const byte 識別番号 = 0x83;

        /// <summary>自ノードインスタンス数 (EPC=0xD3): 3バイト uint24</summary>
        internal const byte 自ノードインスタンス数 = 0xD3;

        /// <summary>自ノードクラス数 (EPC=0xD4): 2バイト uint16</summary>
        internal const byte 自ノードクラス数 = 0xD4;

        /// <summary>
        /// インスタンスリスト通知 (EPC=0xD5):
        /// [インスタンス数(1B), EOJ×n(3B×n)]。
        /// 状変アナウンスプロパティとして自発通知される。
        /// </summary>
        internal const byte インスタンスリスト通知 = 0xD5;

        /// <summary>自ノードインスタンスリストS (EPC=0xD6): インスタンスリストと同形式</summary>
        internal const byte 自ノードインスタンスリストS = 0xD6;

        /// <summary>自ノードクラスリストS (EPC=0xD7): [クラス数(1B), ClassGroupCode+ClassCode(2B×n)]</summary>
        internal const byte 自ノードクラスリストS = 0xD7;
    }
}

/// <summary>
/// ECHONET Lite プロパティの固定 EDT (プロパティ値データ) 定数。
/// </summary>
internal static class Edt
{
    /// <summary>動作状態「ON」(0x30)</summary>
    internal const byte 動作状態_ON = 0x30;

    /// <summary>設置場所「未設定」(0x00)</summary>
    internal const byte 設置場所_未設定 = 0x00;

    /// <summary>
    /// 積算電力量単位「0.01 kWh」(EPC=0xE1, 値=0x02)。
    /// MeterSimulator の UnitValue (0.01m) と対応する。
    /// </summary>
    internal const byte 積算電力量単位_0_01kWh = 0x02;

    /// <summary>規格Version情報の Release 文字 (Release R → 'R')</summary>
    internal const byte 規格Version_ReleaseR = (byte)'R';

    /// <summary>規格Version情報の Revision 番号 (Revision 4 → 0x04)</summary>
    internal const byte 規格Version_Revision4 = 0x04;
}
