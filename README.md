# Turbo Circuit

Unity 製の 3D カートレーシングゲームです。4 コース・5 キャラ、アイテム戦、ドリフト、ジャンプ台、**2 人対戦（画面分割）** を備えています。

## ダウンロード (Windows)

[Releases](../../releases) から `TurboCircuit-win64.zip` をダウンロードして展開し、`TurboCircuit.exe` を起動してください。

## コース

| # | 名前 | 特徴 |
|---|------|------|
| 1 | TURBO CIRCUIT | 丘陵と高架橋の立体交差、終盤のビッグジャンプ |
| 2 | SUNSET DUNES | 砂漠の巨大デューンとジェットコースター |
| 3 | FROST PEAK | 標高差 35m の雪山クライム |
| 4 | NEON METROPOLIS | 摩天楼の谷間とビルを貫通するトンネル |

## 操作

タイトル画面: `A` `D` コース選択 / `W` `S` キャラ選択 / `TAB` 1P・2P 切り替え / `Enter` スタート

| | 1 人プレイ | 2P 対戦 P1 | 2P 対戦 P2 |
|---|---|---|---|
| 加速・ブレーキ | `W` `S` または `↑` `↓` | `W` `S` | `↑` `↓` |
| ステアリング | `A` `D` または `←` `→` | `A` `D` | `←` `→` |
| ドリフト / ホップ | `Space` / `Shift` | `Space` / `L-Shift` | `R-Shift` / `.` |
| アイテム | `E` / `Ctrl` | `E` / `L-Ctrl` | `R-Ctrl` / `/` |

`Esc` でポーズ（`R` リスタート、`Q` 終了）。

## オンライン対戦（別PC・1 対 1）

タイトル画面で `O` を押すとオンライン画面が開きます。ホストがコースを選んでレースを開始し、CPU カートも同じレースに参加します。`Esc` で切断してタイトルに戻ります。

- **IP 直接接続（追加設定なしで使えます）**: ホストが「HOST (LISTEN)」、相手が「JOIN」にホストの IP（例 `192.168.0.10`、ポート指定は `IP:7777`）を入力します。同じ LAN か VPN（Tailscale など）なら、そのまま繋がります。インターネット越しの場合はホスト側の UDP 7777 番ポートの開放が必要です。
- **ルームコード（Unity Relay）**: ホストが「HOST ROOM」で発行したコードを相手が「JOIN」に入力します。ポート開放は不要ですが、開発者側で Unity Cloud プロジェクトとのリンクが必要です（下記）。

### Unity Relay を有効にする（開発者向け）

1. [Unity Dashboard](https://cloud.unity.com/) でプロジェクトを作り、Relay を有効にする
2. Unity エディターの `Edit > Project Settings > Services` でそのプロジェクトにリンクする
3. `Turbo Circuit > Build Windows` でビルドする

リンクしていないビルドでは、Relay のボタンを押すとエラーが表示されます（IP 直接接続は使えます）。

## ビルド

Unity 6000.6.3f1 で開き、メニュー `Turbo Circuit > Setup Scene` の後に `Build Windows` を実行するか、次のコマンドでビルドします。

```
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod BuildTool.BuildWindows
```

出力先は `Build/Windows` です。

## クレジット

カートと小物のモデルは [Kenney](https://kenney.nl/) のアセット（CC0）を使用しています。

効果音は [Kenney](https://kenney.nl/) の Interface / Impact / Sci-fi Sounds（CC0）、BGM は OpenGameArt の CC0 楽曲（cynicmusic、Julie Damsgaard、Centurion_of_war、wipics）を使用しています。曲名とリンクは [Assets/Resources/Audio/CREDITS.md](Assets/Resources/Audio/CREDITS.md) に記載しています。
