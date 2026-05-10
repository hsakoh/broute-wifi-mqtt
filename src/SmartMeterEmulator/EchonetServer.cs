using EchoDotNetLite;
using EchoDotNetLite.Enums;
using EchoDotNetLite.Models;
using EchoDotNetLiteWiFiBridge;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SmartMeterEmulator;

/// <summary>
/// ECHONET Lite プロトコルのサーバー側を担当し、
/// 低圧スマート電力量メータ（0x02, 0x88, 0x01）として振る舞う。
/// </summary>
public class EchonetServer
{
    // ---- EOJ 定義 ----
    private static readonly EOJ SmartMeterEOJ = new()
    {
        ClassGroupCode = Eoj.住宅設備関連機器クラスグループ,
        ClassCode = Eoj.低圧スマート電力量メータ,
        InstanceCode = 0x01,
    };
    private static readonly EOJ NodeProfileEOJ = new()
    {
        ClassGroupCode = Eoj.プロファイルクラスグループ,
        ClassCode = Eoj.ノードプロファイル,
        InstanceCode = 0x01,
    };

    // ---- スマートメーター プロパティマップ ----
    // GET プロパティ (21 個、Pattern2 ビットマップ形式):
    //   Epc.共通.動作状態 (0x80), 低圧スマート電力量メータ.設置場所 (0x81),
    //   Epc.共通.規格Version情報 (0x82), Epc.共通.メーカコード (0x8A),
    //   低圧スマート電力量メータ.製造番号 (0x8D), 低圧スマート電力量メータ.現在時刻 (0x97),
    //   低圧スマート電力量メータ.現在年月日 (0x98), Epc.共通.状変アナウンスプロパティマップ (0x9D),
    //   Epc.共通.Setプロパティマップ (0x9E), Epc.共通.Getプロパティマップ (0x9F),
    //   低圧スマート電力量メータ.Bルート識別番号 (0xC0), 低圧スマート電力量メータ.一分積算電力量計測値 (0xD0),
    //   低圧スマート電力量メータ.係数 (0xD3), 低圧スマート電力量メータ.積算電力量有効桁数 (0xD7),
    //   低圧スマート電力量メータ.積算電力量計測値_正方向 (0xE0), 低圧スマート電力量メータ.積算電力量単位 (0xE1),
    //   低圧スマート電力量メータ.積算電力量計測値_逆方向 (0xE3), 低圧スマート電力量メータ.瞬時電力計測値 (0xE7),
    //   低圧スマート電力量メータ.瞬時電流計測値 (0xE8),
    //   低圧スマート電力量メータ.定時積算電力量計測値_正方向 (0xEA),
    //   低圧スマート電力量メータ.定時積算電力量計測値_逆方向 (0xEB)
    private static readonly byte[] MeterGetPropertyMap =
    [
        21,                                     // プロパティ数 (21 >= 16 なので Pattern2)
        0x71, 0x41, 0x01, 0x60, 0x00, 0x00,   // byte[0]～[5]  : 0x8F～0x80 対応ビット列
        0x00, 0x62, 0x42, 0x00, 0x41, 0x40,   // byte[6]～[11] : 0x9F～0x90 対応ビット列
        0x00, 0x03, 0x02, 0x02,                // byte[12]～[15]: 0xEF～0xE0 対応ビット列
    ];
    // ANNO: 低圧スマート電力量メータ.定時積算電力量計測値_正方向 (0xEA),
    //       低圧スマート電力量メータ.定時積算電力量計測値_逆方向 (0xEB) (2 個 → Pattern1)
    private static readonly byte[] MeterAnnoPropertyMap =
    [
        2,
        Epc.低圧スマート電力量メータ.定時積算電力量計測値_正方向,
        Epc.低圧スマート電力量メータ.定時積算電力量計測値_逆方向,
    ];
    // SET: なし
    private static readonly byte[] MeterSetPropertyMap = [0];

    // ---- ノードプロファイル プロパティマップ ----
    // GET プロパティ (12 個 → Pattern1):
    //   Epc.共通.動作状態 (0x80), Epc.共通.規格Version情報 (0x82),
    //   Epc.ノードプロファイル.識別番号 (0x83), Epc.共通.メーカコード (0x8A),
    //   Epc.共通.状変アナウンスプロパティマップ (0x9D), Epc.共通.Setプロパティマップ (0x9E),
    //   Epc.共通.Getプロパティマップ (0x9F),
    //   Epc.ノードプロファイル.自ノードインスタンス数 (0xD3),
    //   Epc.ノードプロファイル.自ノードクラス数 (0xD4),
    //   Epc.ノードプロファイル.インスタンスリスト通知 (0xD5),
    //   Epc.ノードプロファイル.自ノードインスタンスリストS (0xD6),
    //   Epc.ノードプロファイル.自ノードクラスリストS (0xD7)
    private static readonly byte[] NodeProfileGetPropertyMap =
    [
        12,
        Epc.共通.動作状態,
        Epc.共通.規格Version情報,
        Epc.ノードプロファイル.識別番号,
        Epc.共通.メーカコード,
        Epc.共通.状変アナウンスプロパティマップ,
        Epc.共通.Setプロパティマップ,
        Epc.共通.Getプロパティマップ,
        Epc.ノードプロファイル.自ノードインスタンス数,
        Epc.ノードプロファイル.自ノードクラス数,
        Epc.ノードプロファイル.インスタンスリスト通知,
        Epc.ノードプロファイル.自ノードインスタンスリストS,
        Epc.ノードプロファイル.自ノードクラスリストS,
    ];
    // ANNO: Epc.ノードプロファイル.インスタンスリスト通知 (0xD5) (1 個)
    private static readonly byte[] NodeProfileAnnoPropertyMap = [1, Epc.ノードプロファイル.インスタンスリスト通知];
    // SET: なし
    private static readonly byte[] NodeProfileSetPropertyMap = [0];

    // インスタンスリスト: スマートメーター 1 台
    // [インスタンス数(1), ClassGroupCode, ClassCode, InstanceCode]
    private static readonly byte[] InstanceList =
    [
        1,
        Eoj.住宅設備関連機器クラスグループ,
        Eoj.低圧スマート電力量メータ,
        0x01,
    ];

    private readonly ILogger<EchonetServer> _logger;
    private readonly WiFiLowerLayerClient _wifiClient;
    private readonly MeterSimulator _simulator;
    private readonly EmulatorOptions _options;
    private ushort _tid;

    public EchonetServer(
        ILogger<EchonetServer> logger,
        WiFiLowerLayerClient wifiClient,
        MeterSimulator simulator,
        EmulatorOptions options)
    {
        _logger = logger;
        _wifiClient = wifiClient;
        _simulator = simulator;
        _options = options;

        _wifiClient.OnEventReceived += OnEventReceived;
    }

    private ushort NextTid() => ++_tid;

    // ---- 受信ハンドラ ----

    private void OnEventReceived(object? sender, (string address, byte[] data) value)
    {
        var frame = FrameSerializer.Deserialize(value.data);
        if (frame is null) return;

        if (frame.EDATA is EDATA1 edata)
        {
            _logger.LogDebug("受信: {Address} ESV={ESV} SEOJ={SEOJ} DEOJ={DEOJ}",
                value.address, edata.ESV,
                $"{edata.SEOJ.ClassGroupCode:X2}{edata.SEOJ.ClassCode:X2}{edata.SEOJ.InstanceCode:X2}",
                $"{edata.DEOJ.ClassGroupCode:X2}{edata.DEOJ.ClassCode:X2}{edata.DEOJ.InstanceCode:X2}");
        }

        _ = Task.Run(async () =>
        {
            try { await HandleFrameAsync(value.address, frame); }
            catch (Exception ex) { _logger.LogError(ex, "フレーム処理中にエラーが発生しました"); }
        });
    }

    private async Task HandleFrameAsync(string senderAddress, Frame frame)
    {
        if (frame.EHD2 != EHD2.Type1 || frame.EDATA is not EDATA1 edata)
            return;

        switch (edata.ESV)
        {
            case ESV.Get:
                await HandleGetAsync(senderAddress, frame.TID, edata);
                break;

            case ESV.INF:
                // コントローラからのインスタンスリスト通知（Epc.ノードプロファイル.インスタンスリスト通知）を受信したら自分も通知する
                if (edata.SEOJ.ClassGroupCode == Eoj.プロファイルクラスグループ
                    && edata.SEOJ.ClassCode == Eoj.ノードプロファイル
                    && edata.OPCList?.Any(p => p.EPC == Epc.ノードプロファイル.インスタンスリスト通知) == true)
                {
                    _logger.LogInformation(
                        "コントローラからのインスタンスリスト通知を受信 ({Address})、インスタンスリスト通知を送信します",
                        senderAddress);
                    await SendInstanceListNotificationAsync();
                }
                break;

            case ESV.INF_REQ:
                // ノードプロファイル宛ての インスタンスリスト通知要求（Epc.ノードプロファイル.インスタンスリスト通知）→ INF で応答
                if (edata.DEOJ == NodeProfileEOJ
                    && edata.OPCList?.Any(p => p.EPC == Epc.ノードプロファイル.インスタンスリスト通知) == true)
                {
                    await SendInstanceListNotificationAsync();
                }
                break;
        }
    }

    // ---- Get 要求処理 ----

    private async Task HandleGetAsync(string senderAddress, ushort tid, EDATA1 edata)
    {
        _logger.LogInformation("GET 要求受信: {Address} SEOJ={SEOJ} DEOJ={DEOJ} EPCs=[{EPCs}]",
            senderAddress,
            $"{edata.SEOJ.ClassGroupCode:X2}{edata.SEOJ.ClassCode:X2}{edata.SEOJ.InstanceCode:X2}",
            $"{edata.DEOJ.ClassGroupCode:X2}{edata.DEOJ.ClassCode:X2}{edata.DEOJ.InstanceCode:X2}",
            string.Join(", ", (edata.OPCList ?? []).Select(p => $"0x{p.EPC:X2}")));

        bool hasError = false;
        var responseList = new List<PropertyRequest>();

        if (edata.DEOJ == SmartMeterEOJ)
        {
            foreach (var opc in edata.OPCList ?? [])
            {
                var value = GetSmartMeterProperty(opc.EPC);
                if (value is null)
                {
                    _logger.LogWarning("不明なプロパティ EPC=0x{EPC:X2} (スマートメーター)", opc.EPC);
                    hasError = true;
                    responseList.Add(new PropertyRequest { EPC = opc.EPC, PDC = 0x00, EDT = null });
                }
                else
                {
                    responseList.Add(new PropertyRequest { EPC = opc.EPC, PDC = (byte)value.Length, EDT = value });
                }
            }
        }
        else if (edata.DEOJ == NodeProfileEOJ)
        {
            foreach (var opc in edata.OPCList ?? [])
            {
                var value = GetNodeProfileProperty(opc.EPC);
                if (value is null)
                {
                    _logger.LogWarning("不明なプロパティ EPC=0x{EPC:X2} (ノードプロファイル)", opc.EPC);
                    hasError = true;
                    responseList.Add(new PropertyRequest { EPC = opc.EPC, PDC = 0x00, EDT = null });
                }
                else
                {
                    responseList.Add(new PropertyRequest { EPC = opc.EPC, PDC = (byte)value.Length, EDT = value });
                }
            }
        }
        else
        {
            _logger.LogDebug("自ノード宛てではない DEOJ={D:X6} — 無視します",
                (edata.DEOJ.ClassGroupCode << 16) | (edata.DEOJ.ClassCode << 8) | edata.DEOJ.InstanceCode);
            return;
        }

        var esv = hasError ? ESV.Get_SNA : ESV.Get_Res;
        var responseFrame = BuildFrame(tid, edata.DEOJ, edata.SEOJ, esv, responseList);

        _logger.LogInformation("GET 応答送信: {Address} ESV={ESV} EPCs=[{EPCs}]",
            senderAddress, esv,
            string.Join(", ", responseList.Select(p => $"0x{p.EPC:X2}")));
        await _wifiClient.RequestAsync(senderAddress, FrameSerializer.Serialize(responseFrame));
    }

    // ---- 通知送信 ----

    /// <summary>インスタンスリスト通知（INF D5）をマルチキャスト送信する。</summary>
    public async Task SendInstanceListNotificationAsync()
    {
        _logger.LogInformation("インスタンスリスト通知を送信します");
        var frame = BuildFrame(
            NextTid(),
            NodeProfileEOJ,
            NodeProfileEOJ,
            ESV.INF,
            [new PropertyRequest { EPC = Epc.ノードプロファイル.インスタンスリスト通知, PDC = (byte)InstanceList.Length, EDT = InstanceList }]);

        await _wifiClient.RequestAsync(null, FrameSerializer.Serialize(frame));
    }

    /// <summary>定時積算電力量計測値通知（INF 0xEA/0xEB）をマルチキャスト送信する。</summary>
    public async Task SendScheduledCumulativeNotificationAsync()
    {
        _simulator.UpdateScheduled();
        var fwd = _simulator.GetScheduledForward();
        var rev = _simulator.GetScheduledReverse();

        _logger.LogInformation("定時積算電力量通知を送信します");
        var frame = BuildFrame(
            NextTid(),
            SmartMeterEOJ,
            NodeProfileEOJ,
            ESV.INF,
            [
                new PropertyRequest { EPC = Epc.低圧スマート電力量メータ.定時積算電力量計測値_正方向, PDC = (byte)fwd.Length, EDT = fwd },
                new PropertyRequest { EPC = Epc.低圧スマート電力量メータ.定時積算電力量計測値_逆方向, PDC = (byte)rev.Length, EDT = rev },
            ]);

        await _wifiClient.RequestAsync(null, FrameSerializer.Serialize(frame));
    }

    // ---- スマートメーター プロパティ値 ----

    private byte[]? GetSmartMeterProperty(byte epc) => epc switch
    {
        Epc.共通.動作状態                                           => [Edt.動作状態_ON],
        Epc.低圧スマート電力量メータ.設置場所                       => [Edt.設置場所_未設定],
        Epc.共通.規格Version情報                                    => [0x00, 0x00, Edt.規格Version_ReleaseR, Edt.規格Version_Revision4],
        Epc.共通.メーカコード                                       => [0x00, 0x00, 0x77],  // ダミーメーカコード
        Epc.低圧スマート電力量メータ.製造番号                       => EncodeSerialNumber(_options.SerialNumber, 12),
        Epc.低圧スマート電力量メータ.現在時刻                       => _simulator.GetCurrentTime(),
        Epc.低圧スマート電力量メータ.現在年月日                     => _simulator.GetCurrentDate(),
        Epc.共通.状変アナウンスプロパティマップ                     => MeterAnnoPropertyMap,
        Epc.共通.Setプロパティマップ                                => MeterSetPropertyMap,
        Epc.共通.Getプロパティマップ                                => MeterGetPropertyMap,
        Epc.低圧スマート電力量メータ.Bルート識別番号                => EncodeBRouteId(_options.SerialNumber),
        Epc.低圧スマート電力量メータ.一分積算電力量計測値           => _simulator.Get1MinCumulative(),
        Epc.低圧スマート電力量メータ.係数                           => [0x00, 0x00, 0x00, 0x01],  // 係数: 1
        Epc.低圧スマート電力量メータ.積算電力量有効桁数             => [0x06],                     // 有効桁数: 6
        Epc.低圧スマート電力量メータ.積算電力量計測値_正方向        => _simulator.GetCumulativeForward(),
        Epc.低圧スマート電力量メータ.積算電力量単位                 => [Edt.積算電力量単位_0_01kWh],
        Epc.低圧スマート電力量メータ.積算電力量計測値_逆方向        => _simulator.GetCumulativeReverse(),
        Epc.低圧スマート電力量メータ.瞬時電力計測値                 => _simulator.GetInstantaneousPower(),
        Epc.低圧スマート電力量メータ.瞬時電流計測値                 => _simulator.GetInstantaneousCurrent(),
        Epc.低圧スマート電力量メータ.定時積算電力量計測値_正方向    => _simulator.GetScheduledForward(),
        Epc.低圧スマート電力量メータ.定時積算電力量計測値_逆方向    => _simulator.GetScheduledReverse(),
        _ => null,
    };

    // ---- ノードプロファイル プロパティ値 ----

    private byte[]? GetNodeProfileProperty(byte epc) => epc switch
    {
        Epc.共通.動作状態                                      => [Edt.動作状態_ON],
        Epc.共通.規格Version情報                               => [0x00, 0x00, Edt.規格Version_ReleaseR, Edt.規格Version_Revision4],
        Epc.ノードプロファイル.識別番号                        => EncodeNodeIdentifier(_options.SerialNumber),
        Epc.共通.メーカコード                                  => [0x00, 0x00, 0x77],  // ダミーメーカコード
        Epc.共通.状変アナウンスプロパティマップ                => NodeProfileAnnoPropertyMap,
        Epc.共通.Setプロパティマップ                           => NodeProfileSetPropertyMap,
        Epc.共通.Getプロパティマップ                           => NodeProfileGetPropertyMap,
        Epc.ノードプロファイル.自ノードインスタンス数          => [0x00, 0x00, 0x01],  // 自ノードインスタンス数: 1
        Epc.ノードプロファイル.自ノードクラス数                => [0x00, 0x01],        // 自ノードクラス数: 1
        Epc.ノードプロファイル.インスタンスリスト通知          => InstanceList,
        Epc.ノードプロファイル.自ノードインスタンスリストS     => InstanceList,
        Epc.ノードプロファイル.自ノードクラスリストS           => [1, Eoj.住宅設備関連機器クラスグループ, Eoj.低圧スマート電力量メータ],
        _ => null,
    };

    // ---- ヘルパー ----

    private static Frame BuildFrame(ushort tid, EOJ seoj, EOJ deoj, ESV esv, List<PropertyRequest> opcList)
        => new()
        {
            EHD1 = EHD1.ECHONETLite,
            EHD2 = EHD2.Type1,
            TID = tid,
            EDATA = new EDATA1
            {
                SEOJ = seoj,
                DEOJ = deoj,
                ESV = esv,
                OPCList = opcList,
            },
        };

    /// <summary>製造番号を ASCII エンコードし、指定バイト数にスペース埋めする。</summary>
    private static byte[] EncodeSerialNumber(string serial, int length)
    {
        var result = new byte[length];
        var bytes = Encoding.ASCII.GetBytes(serial);
        Array.Copy(bytes, result, Math.Min(bytes.Length, length));
        // 残りをスペースで埋める
        for (int i = bytes.Length; i < length; i++)
            result[i] = 0x20;
        return result;
    }

    /// <summary>Bルート識別番号（8 バイト）を生成する。</summary>
    private static byte[] EncodeBRouteId(string serial)
    {
        var result = new byte[8];
        var bytes = Encoding.ASCII.GetBytes(serial);
        Array.Copy(bytes, result, Math.Min(bytes.Length, 8));
        return result;
    }

    /// <summary>ノードプロファイル識別番号（12 バイト: 0xFE + メーカ 3B + 固有 8B）を生成する。</summary>
    private static byte[] EncodeNodeIdentifier(string serial)
    {
        var result = new byte[12];
        result[0] = 0xFE;           // 識別番号種別
        result[1] = 0x00; result[2] = 0x00; result[3] = 0x77; // ダミーメーカコード
        var bytes = Encoding.ASCII.GetBytes(serial);
        Array.Copy(bytes, 0, result, 4, Math.Min(bytes.Length, 8));
        return result;
    }
}
