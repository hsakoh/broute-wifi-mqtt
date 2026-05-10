# ホームアシスタント アドオン BRoute-WiFi-Mqtt
第2世代低圧スマート電力量メータ（Wi-Fi Bルート）を Home Assistant の MQTT 統合にデバイス/センサーとして統合するアドオン

ECHONET Lite プロトコル（Bルート Wi-Fi 方式）を経由して情報を取得します。<br>
Wi-SUN USBスティック等の追加ハードウェアは不要で、HA-OS を搭載した機器が同一 Wi-Fi ネットワーク（または接続可能なネットワーク）に存在していれば利用可能です。

> [!WARNING]
> 現時点では実機（Wi-Fi Bルート対応スマートメーター）での動作確認を行っていません。

## 対象機器

対象は**第2世代（次世代）スマートメーター**の **Wi-Fi 補完方式**（2.4GHz帯 IEEE 802.11n）に対応した低圧スマート電力量メータです。

| 世代 | 無線方式 | 本アドオン |
|------|----------|-----------|
| 第1世代 | Wi-SUN (920MHz帯) | 非対応（[broute-mqtt](https://github.com/hsakoh/broute-mqtt) を使用） |
| 第2世代 | Wi-SUN 主 ＋ **Wi-Fi 補完** (2.4GHz) | **本アドオン対応** |

## 機能概要

次の情報を取得し、MQTT 統合のデバイス/センサー情報として通知します ([MQTT Sensor - Home Assistant](https://www.home-assistant.io/integrations/sensor.mqtt/))

* 起動時 / 手動要求時 / 指定周期での取得
  * 瞬時電流計測値（R相）A（アンペア）
  * 瞬時電流計測値（T相）A（アンペア）
  * 瞬時電力計測値 W（ワット）
* 起動時 / 手動要求時 / 30分毎の定期通知受信
  * 積算電力量計測値（正方向）kWh
  * 積算電力量計測値（逆方向）kWh
* 起動時 / 手動要求時での取得（1分積算電力量）
  * 1分積算電力量計測値（正方向）kWh
  * 1分積算電力量計測値（逆方向）kWh
* 起動時（定性情報）
  * メーカコード
  * 規格 Version 情報
  * 製造番号
  * 設置場所
  * Bルート識別番号

積算電力量・瞬時値・1分積算電力量それぞれを即時取得するボタンを提供します ([MQTT Button - Home Assistant](https://www.home-assistant.io/integrations/button.mqtt/))

複数のスマートメーターが同一ネットワーク上に存在する場合、それぞれ自動的に検出・登録されます。

## ネットワーク要件

| 項目 | 内容 |
|------|------|
| IPバージョン | IPv6（リンクローカルアドレス使用） |
| 通信ポート | UDP 3610（ECHONET Lite 標準） |
| ネットワーク | HA-OS とスマートメーターが同一 Wi-Fi セグメントに接続していること |
| ホストネットワーク | アドオンは `host_network: true` で動作します（IPv6 リンクローカルアドレス取得のため） |

## 導入方法

### 手順 1: Mosquitto MQTT ブローカーのインストール

本アドオンはスマートメーターの情報を Home Assistant の [MQTT 統合](https://www.home-assistant.io/integrations/mqtt/)へ送信します。  
まず MQTT ブローカーをインストールしてください。

推奨は [Mosquitto MQTT broker アドオン](https://github.com/home-assistant/addons/blob/master/mosquitto/DOCS.md) を使用する方法です。

### 手順 2: MQTT 統合の構成

[こちらのページ](https://www.home-assistant.io/integrations/mqtt/#broker-configuration) の手順に従い、MQTT 統合がどのブローカーと連携するかを設定してください。

### 手順 3: アドオンのインストール

アドオンのインストール方法は 3 種類あります。

#### 3-1. GitHub Container Registry に登録された Docker イメージを参照する（推奨）

[![Open your Home Assistant instance and show the add add-on repository dialog with a specific repository URL pre-filled.](https://my.home-assistant.io/badges/supervisor_add_addon_repository.svg)](https://my.home-assistant.io/redirect/supervisor_add_addon_repository/?repository_url=https%3A%2F%2Fgithub.com%2Fhsakoh%2Fha-addon)

上のボタンが機能しない場合は、以下の手順でリポジトリを追加してください。

1. ホームアシスタント UI でアドオンストアに移動します（左側のメニューで「スーパーバイザー」、上部タブで「アドオンストア」）
2. 右上隅にある 3 つの縦のドットを選択し、「リポジトリ」を選択します
3. 「アドオンリポジトリの管理」画面で `https://github.com/hsakoh/ha-addon` を入力し、「追加」をクリックします
4. リストの一番下までスクロールするか、検索を使用してアドオンを見つけます
5. アドオンを選択し、「インストール」をクリックします

#### 3-2. 事前に .NET アプリをコンパイル・発行してから HAOS 上で Docker イメージをビルドする

1. リポジトリのルートで `./_compile_self/dotnet_publish.ps1` を実行します
2. `_compile_self` フォルダの中身一式を HA-OS の `/addons/broute-wifi-mqtt` に配置します

#### 3-3. HA-OS 上で Docker イメージをビルドする際に .NET アプリもコンパイル・発行する

1. `src` フォルダと `_build_on_haos` フォルダの中身一式を HA-OS の `/addons/broute-wifi-mqtt` に配置します
2. HA-OS 搭載のマシンが非力な場合、ビルド（インストール）に非常に時間がかかります。**推奨しません。**

## 設定項目

| 設定キー | 既定値 | 説明 |
|--|--|--|
| BRoute:NetworkInterfaceName | `auto` | スマートメーターと通信するネットワークインターフェース名を指定します<br>`auto` を指定すると自動検出します（ループバック・Docker仮想インタフェースを除く、IPv6リンクローカルアドレスを持つ物理NIC）<br>自動検出が失敗する場合は `eth0` や `wlan0`（HA-OS）、`Wi-Fi`（Windows）等を明示的に指定してください |
| BRoute:InstantaneousValueInterval | `00:01:10` | 瞬時値の周期的な取得間隔を指定します<br>TimeSpan（`HH:mm:ss`）形式で記述します |
| BRoute:PropertyReadTimeout | `00:00:05` | プロパティ値読み出しのタイムアウトを指定します<br>TimeSpan（`HH:mm:ss`）形式で記述します |
| BRoute:PropertyReadMaxRetryAttempts | `3` | プロパティ値読み出しの最大再試行回数を指定します |
| BRoute:PropertyReadRetryDelay | `00:00:05` | プロパティ値読み出しの再試行間隔を指定します<br>TimeSpan（`HH:mm:ss`）形式で記述します |
| BRoute:PropertyReadIntervalDelay | `00:00:02` | プロパティ値読み出し1件ごとの待機時間を指定します<br>TimeSpan（`HH:mm:ss`）形式で記述します |
| BRoute:ContinuePollingOnError | `true` | ポーリングでタイムアウト等エラー発生時にアドオンの処理を継続する場合、`true` を指定します |
| Mqtt:AutoConfig | `true` | デフォルトの Home Assistant Mosquitto 統合を使用している場合、`true` に設定すると接続詳細を自動検出します |
| Mqtt:Host | - | MQTT ブローカーのホスト名を指定します |
| Mqtt:Port | `1883` | ポート番号を指定します |
| Mqtt:Id | - | 認証がある場合、ID を指定します |
| Mqtt:Pw | - | 認証がある場合、パスワードを指定します |
| Mqtt:Tls | `false` | TLS 接続を使用する場合、`true` を指定します |
| LogLevel | `Information` | ログレベルを設定します<br>`Trace`,`Debug`,`Information`,`Warning`,`Error`,`Critical`,`None` |

## 開発者向けの情報

* アドオンとしては、Home Assistant ベースイメージに .NET ランタイムを組み込み、.NET コンソールアプリケーションを起動しているだけです。
* アプリケーション単体は Windows 上でも実行可能です。
  * `NetworkInterfaceName` に `Wi-Fi` 等、Windows 上のインターフェース名を設定してください。
  * slnx ファイルを Visual Studio で開き、デバッグ可能です。
  * Windows 上では AddOn の設定ファイル `/data/options.json` にアクセスできないため、`appsettings.Development.json` に設定を行ってください。
* [.NET での汎用ホスト 既定の builder 設定](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/generic-host#default-builder-settings)の通り、環境変数やコマンドライン引数からも読み込み可能です<br>（階層は `BRoute:NetworkInterfaceName` 等コロンを含めて表現が必要です）
* 実機なしで動作確認・開発を行うためのスマートメーター **エミュレーター**を用意しています。詳細は [SmartMeterEmulator/README.md](src/SmartMeterEmulator/README.md) を参照してください。

## 関連リポジトリ

| リポジトリ | 説明 |
|------------|------|
| [broute-mqtt](https://github.com/hsakoh/broute-mqtt) | 第1世代スマートメーター（Wi-SUN）向けアドオン |

## 参考資料

* [EMS・アグリゲーションコントローラー スマートメーターBルート(低圧スマート電力量メーター) 運用ガイドライン 第5.1版](https://www.enecho.meti.go.jp/category/electricity_and_gas/electric/summary/regulations/teiatsu_smartmeter_rev5.1_20260327.pdf)
* [ECHONET Lite 規格書 Ver.1.14](https://echonet.jp/spec_v114_lite/)
* [AIF仕様書（低圧スマート電力量メータ・コントローラ間）](https://echonet.jp/wp/wp-content/uploads/pdf/General/Standard/AIF/lvsm/lvsm_aif_ver1.10.pdf)
