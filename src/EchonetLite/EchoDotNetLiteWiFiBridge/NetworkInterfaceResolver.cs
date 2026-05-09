using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace EchoDotNetLiteWiFiBridge;

/// <summary>
/// ネットワークインタフェースを名前または自動検出で解決するクラス。
/// </summary>
public static class NetworkInterfaceResolver
{
    /// <summary>
    /// 自動検出を示す設定値。
    /// </summary>
    public const string AutoValue = "auto";

    /// <summary>
    /// インタフェース名を解決する。<see cref="AutoValue"/> の場合は自動検出を行う。
    /// </summary>
    /// <param name="nameOrAuto">インタフェース名、または "auto"。</param>
    /// <param name="logger">候補一覧・選択結果の出力先ロガー（省略可）。</param>
    /// <returns>解決された <see cref="NetworkInterface"/>。</returns>
    /// <exception cref="InvalidOperationException">インタフェースが見つからない場合。</exception>
    public static NetworkInterface Resolve(string nameOrAuto, ILogger? logger = null)
    {
        if (!string.Equals(nameOrAuto, AutoValue, StringComparison.OrdinalIgnoreCase))
        {
            return FindByName(nameOrAuto);
        }

        return AutoDetect(logger);
    }

    private static NetworkInterface FindByName(string name)
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == name || n.Id == name)
            ?? throw new InvalidOperationException(
                $"ネットワークインタフェース '{name}' が見つかりません。" +
                $"利用可能: {string.Join(", ", NetworkInterface.GetAllNetworkInterfaces().Select(n => n.Name))}");
    }

    private static NetworkInterface AutoDetect(ILogger? logger)
    {
        var all = NetworkInterface.GetAllNetworkInterfaces();
        var candidates = all.Where(IsCandidate).ToList();

        if (logger is not null)
        {
            logger.LogDebug("インタフェース自動検出: 全インタフェース数={Total}, 候補数={Count}",
                all.Length, candidates.Count);
            foreach (var c in candidates)
            {
                var gw = c.GetIPProperties().GatewayAddresses
                    .Where(g => !g.Address.Equals(System.Net.IPAddress.Any)
                             && !g.Address.Equals(System.Net.IPAddress.IPv6Any))
                    .Select(g => g.Address.ToString());
                var ipv6 = c.GetIPProperties().UnicastAddresses
                    .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(ua => ua.Address.ToString());
                logger.LogDebug("  候補: {Name} | GW=[{GW}] | IPv6=[{IPv6}]",
                    c.Name,
                    string.Join(", ", gw),
                    string.Join(", ", ipv6));
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "自動検出できるネットワークインタフェースが見つかりませんでした。" +
                $"利用可能インタフェース: {string.Join(", ", all.Select(n => $"{n.Name}({n.NetworkInterfaceType}, {n.OperationalStatus})"))}");
        }

        NetworkInterface selected;
        string reason;

        // 優先順位 1: デフォルトゲートウェイ（デフォルトルート）が設定されているインタフェース
        // NetworkInterface.GetIPProperties().GatewayAddresses は ip route の
        // "default via ... dev <name>" に相当する情報を OS API 経由で取得する。
        // 外部へ実際にパケットを送出するインタフェースをほぼ確実に特定できる。
        var withDefaultGateway = candidates.Where(HasDefaultGateway).ToList();
        if (withDefaultGateway.Count > 0)
        {
            selected = withDefaultGateway[0];
            reason = "デフォルトゲートウェイあり";
        }
        // 優先順位 2: グローバル IPv6 アドレスを持つインタフェース（ルーターと接続済み）
        else if (candidates.Where(HasGlobalIPv6Address).ToList() is { Count: > 0 } withGlobalIPv6)
        {
            selected = withGlobalIPv6[0];
            reason = "グローバル IPv6 アドレスあり";
        }
        // 優先順位 3: 残った候補の先頭
        else
        {
            selected = candidates[0];
            reason = "フォールバック（先頭候補）";
        }

        logger?.LogInformation(
            "インタフェース自動検出: '{Name}' を選択しました（理由: {Reason}）",
            selected.Name, reason);

        return selected;
    }

    /// <summary>
    /// インタフェースにデフォルトゲートウェイが設定されているかを判定する。
    /// <para>
    /// <see cref="IPInterfaceProperties.GatewayAddresses"/> は、
    /// Linux の <c>ip route</c> / Windows のルーティングテーブルから
    /// そのインタフェースに割り当てられたゲートウェイアドレスを返す。
    /// ループバックアドレス (0.0.0.0 / ::) を除いた有効なエントリが存在すれば
    /// デフォルトルートあり、と判断する。
    /// </para>
    /// </summary>
    private static bool HasDefaultGateway(NetworkInterface ni)
        => ni.GetIPProperties().GatewayAddresses
            .Any(g => !g.Address.Equals(System.Net.IPAddress.Any)        // 0.0.0.0 を除外
                   && !g.Address.Equals(System.Net.IPAddress.IPv6Any));  // :: を除外

    private static bool IsCandidate(NetworkInterface ni)
    {
        // ループバック・トンネルは除外
        if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            return false;

        // 動作中でなければ除外
        if (ni.OperationalStatus != OperationalStatus.Up)
            return false;

        // マルチキャスト非対応は除外
        if (!ni.SupportsMulticast)
            return false;

        // IPv6 リンクローカルアドレスがなければ除外（ECHONET Lite 通信に必須）
        if (!HasIPv6LinkLocalAddress(ni))
            return false;

        // Linux の場合: 仮想インタフェースを除外し、物理 NIC に絞り込む
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!IsPhysicalInterfaceOnLinux(ni.Name))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Linux 上で物理 NIC かどうかを判定する。
    /// <list type="bullet">
    /// <item>名前パターン（veth*, docker*, br-*, hassio, lo）で Docker/ブリッジ系を除外</item>
    /// <item>/sys/class/net/&lt;name&gt;/device の存在で物理 NIC であることを確認</item>
    /// </list>
    /// </summary>
    private static bool IsPhysicalInterfaceOnLinux(string name)
    {
        // Docker 仮想ペアインタフェース
        if (name.StartsWith("veth", StringComparison.Ordinal))
            return false;
        // Docker ブリッジ
        if (name.StartsWith("docker", StringComparison.Ordinal))
            return false;
        // Home Assistant Supervisor ブリッジ
        if (name == "hassio")
            return false;
        // 汎用ブリッジ (br-xxxx)
        if (name.StartsWith("br-", StringComparison.Ordinal))
            return false;
        // ループバック
        if (name == "lo")
            return false;

        // /sys/class/net/<name>/device が存在する = 物理 NIC または USB NIC
        // 仮想インタフェース（veth, bridge, loopback）にはこのパスが存在しない
        return Directory.Exists($"/sys/class/net/{name}/device");
    }

    private static bool HasIPv6LinkLocalAddress(NetworkInterface ni)
        => ni.GetIPProperties().UnicastAddresses
            .Any(ua => ua.Address.AddressFamily == AddressFamily.InterNetworkV6
                       && ua.Address.IsIPv6LinkLocal);

    private static bool HasGlobalIPv6Address(NetworkInterface ni)
        => ni.GetIPProperties().UnicastAddresses
            .Any(ua => ua.Address.AddressFamily == AddressFamily.InterNetworkV6
                       && !ua.Address.IsIPv6LinkLocal
                       && !ua.Address.IsIPv6SiteLocal
                       && !ua.Address.IsIPv6Multicast
                       && !ua.Address.Equals(System.Net.IPAddress.IPv6Loopback)
                       && !ua.Address.Equals(System.Net.IPAddress.IPv6None));
}
