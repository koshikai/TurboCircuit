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

## ビルド

Unity 6000.6.3f1 で開き、メニュー `Turbo Circuit > Setup Scene` の後に `Build Windows` を実行するか、次のコマンドでビルドします。

```
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod BuildTool.BuildWindows
```

出力先は `Build/Windows` です。

## クレジット

カートと小物のモデルは [Kenney](https://kenney.nl/) のアセット（CC0）を使用しています。
