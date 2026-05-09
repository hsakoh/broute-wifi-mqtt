# SmartMeterEmulator

低圧スマート電力量メータ（Wi-Fi Bルート）のエミュレーターです。  
実機のスマートメーターがなくても、`BRouteWifiMqttApp` の動作確認・開発ができます。

## 概要

ECHONET Lite プロトコル（UDP 3610ポート / IPv6）のサーバーとして振る舞い、  
コントローラ（`BRouteWifiMqttApp`）からの Get 要求に応答します。

| クラス | EOJ |
|--------|-----|
| 低圧スマート電力量メータ | `0x02 0x88 0x01` |
| ノードプロファイル | `0x0E 0xF0 0x01` |

## 対応プロパティ

### スマートメーター（0x028801）

| EPC | プロパティ名 | 備考 |
|-----|------------|------|
| 0x80 | 動作状態 | 固定: ON |
| 0x81 | 設置場所 | 固定: 未設定 |
| 0x82 | 規格 Version 情報 | 固定: Release R, Revision 4 |
| 0x8A | メーカコード | 固定: 0x000077 (ダミー) |
| 0x8D | 製造番号 | `SerialNumber` 設定値 (12バイトASCII) |
| 0x97 | 現在時刻 | 実時刻 |
| 0x98 | 現在年月日 | 実日付 |
| 0x9D | 状変アナウンスプロパティマップ | |
| 0x9E | Set プロパティマップ | |
| 0x9F | Get プロパティマップ | |
| 0xC0 | Bルート識別番号 | `SerialNumber` 設定値 (8バイト) |
| 0xD0 | 1分積算電力量計測値 | 正・逆方向 |
| 0xD3 | 係数 | 固定: 1 |
| 0xD7 | 積算電力量有効桁数 | 固定: 6 |
| 0xE0 | 積算電力量計測値（正方向） | 毎分更新 |
| 0xE1 | 積算電力量単位 | 固定: 0.01 kWh |
| 0xE3 | 積算電力量計測値（逆方向） | 固定 |
| 0xE7 | 瞬時電力計測値 | 毎分更新（±15%変動） |
| 0xE8 | 瞬時電流計測値（R相・T相） | 毎分更新（±15%変動） |
| 0xEA | 定時積算電力量計測値（正方向） | 30分毎に通知・更新 |
| 0xEB | 定時積算電力量計測値（逆方向） | 30分毎に通知・更新 |

### ノードプロファイル（0x0EF001）

`0x80` / `0x82` / `0x83` / `0x8A` / `0x9D` / `0x9E` / `0x9F` / `0xD3` / `0xD4` / `0xD5` / `0xD6` / `0xD7`

## 動作フロー

```
起動
  └─ UDP 3610 受信待機開始
  └─ インスタンスリスト通知 (INF/0xD5) をマルチキャスト送信
  └─ 5秒・30秒・60秒後に再通知（コントローラの遅延起動対応）

コントローラからの INF (インスタンスリスト通知) 受信
  └─ インスタンスリスト通知を返送してコントローラにメーターを認識させる

コントローラからの Get 要求
  └─ 対応プロパティを返答 (Get_Res)
  └─ 非対応プロパティは Get_SNA で応答

毎分
  └─ 瞬時電力・電流をランダムに変動
  └─ 積算電力量に瞬時電力分を加算

30分毎
  └─ 定時積算電力量計測値通知 (INF/0xEA + 0xEB) をマルチキャスト送信
```

## 設定項目

`appsettings.json` の `Emulator` セクションで設定します。

| キー | 既定値 | 説明 |
|------|--------|------|
| `NetworkInterfaceName` | `auto` | 使用するネットワークインターフェース名。`auto` で自動検出（IPv6リンクローカルアドレスを持つNIC）。Windows では `Wi-Fi` 等を指定 |
| `InitialCumulativeForwardKWh` | `5000.0` | 起動時の積算電力量（正方向）kWh |
| `InitialCumulativeReverseKWh` | `0.0` | 起動時の積算電力量（逆方向）kWh |
| `InstantaneousPowerW` | `1000` | 基準瞬時電力 W（毎分 ±15% 変動） |
| `CurrentRA` | `5.0` | 基準瞬時電流 R相 A（毎分 ±15% 変動） |
| `CurrentTA` | `5.0` | 基準瞬時電流 T相 A（毎分 ±15% 変動） |
| `SerialNumber` | `EMULATOR001` | 製造番号（ASCII 最大12文字）。Bルート識別番号にも使用される |

## 実行方法

### Windows

```powershell
cd src/SmartMeterEmulator
dotnet run
```

`NetworkInterfaceName` を自動検出できない場合は明示的に指定してください。

```powershell
dotnet run -- --Emulator:NetworkInterfaceName=Wi-Fi
```

または `appsettings.Development.json` で設定します（`DOTNET_ENVIRONMENT=Development` 時に読み込まれます）。

```json
{
  "Emulator": {
    "NetworkInterfaceName": "Wi-Fi"
  }
}
```

### Linux

```bash
cd src/SmartMeterEmulator
dotnet run -- --Emulator:NetworkInterfaceName=eth0
```

## 同一 Windows 端末でエミュレーターとアドオンを同時実行する場合

実機スマートメーターがない場合、同じ Windows PC でエミュレーターとアドオンを同時に起動して動作確認できます。  
ただし ECHONET Lite 通信は **IPv6 リンクローカルスコープ（同一 L2 セグメント内限定）** を使用するため、  
両プロセスが **同じネットワークセグメント** に存在しないとパケットが届きません。

> **なぜ単純に両方 Windows 起動するだけではダメか**  
> アドオンとエミュレーターが同一 NIC の同じリンクローカルアドレスを持つと、  
> `WiFiLowerLayerClient` の「自己パケット無視」ロジックにより相手のパケットが破棄されます。

### 推奨手順：WSL 上でエミュレーターを動かす

WSL2 の `eth0` は Windows ホストの `vEthernet (WSL)` 仮想スイッチと同一 L2 セグメントを共有しています。  
エミュレーターを WSL 上で動かし、アドオンは Windows 側で `vEthernet (WSL)` を使うように設定することで通信できます。

#### 1. WSL 上でエミュレーターを起動

```bash
# .NET SDK がない場合はインストール
sudo apt install dotnet-sdk-10.0

cd src/SmartMeterEmulator
dotnet run
```

`auto` 設定のまま起動すると WSL の `eth0` が自動選択されます。

#### 2. アドオン側のネットワークインターフェースを設定

アドオン（`BRouteWifiMqttApp`）の `appsettings.Development.json` で WSL 仮想スイッチ側の NIC を指定します。  
Windows のネットワークアダプター一覧で確認した名前をそのまま記載してください（環境により異なります）。

```json
{
  "BRoute": {
    "NetworkInterfaceName": "vEthernet (WSL)"
  }
}
```

> **インターフェース名の例**（環境により異なります）
> - `vEthernet (WSL)` — WSL2 標準構成の場合
> - `vEthernet (WSL (Hyper-V firewall))` — Hyper-V ファイアウォールが有効な場合

#### 3. VS 等でアドオンをデバッグ実行

環境変数 `DOTNET_ENVIRONMENT=Development`（Visual Studio のデバッグ構成では既定で設定済み）の状態で起動すれば  
`appsettings.Development.json` が自動で読み込まれます。

```
WSL eth0 (fe80::xxxx%2) ──┐
                           ├── Hyper-V vSwitch (WSL) ── 同一 L2 セグメント
vEthernet (WSL) on Windows ┘ (fe80::yyyy%N)
```

## 注意事項

- UDP 3610 はシステムポートのため、**管理者権限**が必要な場合があります。
  - Windows: 管理者として実行するか、事前に `netsh` でポート権限を付与してください。
  - Linux: `sudo` で実行するか、`CAP_NET_BIND_SERVICE` を付与してください。
- エミュレーターと `BRouteWifiMqttApp` は**同一の IPv6 ネットワークセグメント**（または同一マシン）に存在する必要があります。
- このプロジェクトはローカル開発・テスト専用です。Docker アドオンのイメージには含まれません。
