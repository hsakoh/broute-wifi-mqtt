using EchoDotNetLite;
using EchoDotNetLite.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EchoDotNetLiteWiFiBridge;

/// <summary>
/// IPv6 UDP を使用して ECHONET Lite 通信を行う Wi-Fi 方式 Bルートブリッジ。
/// スマートメーターはリンクローカルアドレス (fe80::) を使用し、
/// ECHONET Lite マルチキャストグループ (FF02::1) で一斉同報を行う。
/// </summary>
public class WiFiLowerLayerClient(ILogger<WiFiLowerLayerClient> logger, string networkInterfaceName) : ILowerLayerClient, IDisposable
{
    private const int EchonetLiteUdpPort = 3610;
    // ECHONET Lite では "All nodes" マルチキャストアドレスを使用
    private static readonly IPAddress EchonetMulticastAddress = IPAddress.Parse("FF02::1");
    private Socket? _receiveSocket;
    private Socket? _sendSocket;
    private int _interfaceIndex;
    private readonly List<IPAddress> _selfLinkLocalAddresses = [];
    private CancellationTokenSource? _cts;

    public event EventHandler<(string, byte[])>? OnEventReceived;

    /// <summary>
    /// 指定したネットワークインタフェースで受信待機を開始する。
    /// </summary>
    public void Start()
    {
        var networkInterface = GetNetworkInterface(networkInterfaceName, logger);
        var ipProperties = networkInterface.GetIPProperties();

        logger.LogInformation("使用するネットワークインタフェース: '{Name}' (設定値: '{Configured}')",
            networkInterface.Name, networkInterfaceName);

        // リンクローカルアドレスを取得
        _selfLinkLocalAddresses.Clear();
        foreach (var ua in ipProperties.UnicastAddresses)
        {
            if (ua.Address.AddressFamily == AddressFamily.InterNetworkV6
                && ua.Address.IsIPv6LinkLocal)
            {
                _selfLinkLocalAddresses.Add(ua.Address);
                logger.LogInformation("自ノード リンクローカルアドレス: {Address}", ua.Address);
            }
        }

        if (_selfLinkLocalAddresses.Count == 0)
        {
            throw new InvalidOperationException(
                $"ネットワークインタフェース '{networkInterface.Name}' にリンクローカルアドレスが見つかりません。");
        }

        var interfaceIndex = (int)ipProperties.GetIPv6Properties().Index;
        _interfaceIndex = interfaceIndex;
        logger.LogInformation("インタフェース '{Name}' インデックス={Index} で受信待機を開始します", networkInterface.Name, interfaceIndex);

        // IPv6 UDP 受信ソケットを作成してバインド
        _receiveSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
        _receiveSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
        _receiveSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _receiveSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, EchonetLiteUdpPort));

        // ECHONET Lite マルチキャストグループに参加
        var mcastOption = new IPv6MulticastOption(EchonetMulticastAddress, interfaceIndex);
        _receiveSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, mcastOption);
        logger.LogInformation("マルチキャストグループ {Group} に参加しました", EchonetMulticastAddress);

        // IPv6 UDP 送信ソケットを作成してキャッシュ（送信のたびに開かないように）
        _sendSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
        _sendSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, true);
        _sendSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceIndex);

        _cts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1500];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                SocketReceiveFromResult result;
                try
                {
                    result = await _receiveSocket!.ReceiveFromAsync(
                        buffer, SocketFlags.None,
                        new IPEndPoint(IPAddress.IPv6Any, 0), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (result.ReceivedBytes <= 0)
                    continue;

                var remoteEndpoint = (IPEndPoint)result.RemoteEndPoint;
                var remoteAddress = remoteEndpoint.Address;
                var received = result.ReceivedBytes;

                // 自己送信パケットを無視
                if (_selfLinkLocalAddresses.Any(a => a.Equals(remoteAddress)))
                {
                    logger.LogDebug("自己パケットを無視: {Address}", remoteAddress);
                    continue;
                }

                var data = buffer[..received];
                logger.LogDebug("UDP受信: {Address} {Hex}", remoteAddress, BytesConvert.ToHexString(data));

                // スコープIDを除いた純粋なIPv6アドレス文字列をアドレスとして使用
                var addressStr = remoteAddress.ToString();
                // fe80::xxxx%ifindex の形式の場合、スコープIDを保持（送信時に必要）
                OnEventReceived?.Invoke(this, (addressStr, data));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "受信ループで例外が発生しました");
        }
    }

    /// <summary>
    /// 指定したアドレスへ ECHONET Lite フレームを UDP 送信する。
    /// address が null の場合はマルチキャストアドレスへ送信する。
    /// </summary>
    public async Task RequestAsync(string? address, byte[] request)
    {
        IPEndPoint remote;
        if (address == null)
        {
            // マルチキャスト送信（インタフェーススコープID を付与）
            var multicastAddr = GetScopedMulticastAddress();
            remote = new IPEndPoint(multicastAddr, EchonetLiteUdpPort);
        }
        else
        {
            remote = new IPEndPoint(IPAddress.Parse(address), EchonetLiteUdpPort);
        }

        logger.LogDebug("UDP送信: {Address} {Hex}", remote, BytesConvert.ToHexString(request));

        await _sendSocket!.SendToAsync(request, SocketFlags.None, remote);
    }

    private IPAddress GetScopedMulticastAddress()
    {
        // スコープID付きマルチキャストアドレス（Start()でキャッシュ済みの _interfaceIndex を使用）
        return new IPAddress(EchonetMulticastAddress.GetAddressBytes(), _interfaceIndex);
    }

    private static NetworkInterface GetNetworkInterface(string name, ILogger? logger = null)
        => NetworkInterfaceResolver.Resolve(name, logger);

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _receiveSocket?.Close(); } catch { }
        _receiveSocket?.Dispose();
        try { _sendSocket?.Close(); } catch { }
        _sendSocket?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
