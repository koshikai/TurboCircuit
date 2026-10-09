# AGENTS.md

Turbo Circuit プロジェクトにおける AI エージェント（Antigravity, Claude Code, Cursor 等）向けの開発指示・アーキテクチャ規約・検証手順ドキュメントです。

---

## 1. プロジェクト概要

* **プロジェクト名**: Turbo Circuit
* **エンジン**: Unity 6000.6.3f1 (Universal Render Pipeline / URP)
* **プラットフォーム**: Windows (64-bit)
* **ジャンル**: 3D カートレーシングゲーム（1Pソロ / 2P画面分割対戦 / 最大8人オンライン対戦）
* **特徴**:
  * 5コース（TURBO CIRCUIT, SUNSET DUNES, FROST PEAK, NEON METROPOLIS, HOKKAIDO CAMPUS）
  * 5キャラクター/カート（OOBI, OODI, OOLI, OOPI, OOZI）
  * ドリフトブースト、アイテムバトル、ジャンプ台、ゴースト再生
  * Unity Relay / Direct IP によるオンラインマルチプレイ対応

---

## 2. 開発環境 & 基本ルール

* **Unity エディタ実行パス**:
  `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`
* **Python の実行**:
  Python スクリプトを実行する際は、**必ず `uv` を使用すること**（例: `uv run python ...`）。
* **パッケージマネージャ**:
  npm ではなく**なるべく `bun` を使用すること**。
* **マシン環境**:
  ホスト名 `DESKTOP-S12GI1G` (Windows)

---

## 3. アーキテクチャ & コードベース構成

スクリプトは `Assets/Scripts/` 配下に配置され、関心事ごとに `partial class` で機能分割されています。

### 3.1 レース進行 & UI (`RaceManager`)
* **`RaceManager.cs`**:
  * ゲームステート管理 (`State.Title`, `State.Countdown`, `State.Racing`, `State.Results`)
  * レース進行、周回数（ラップ）、タイム計測、順位ソート
* **`RaceManager.UI.cs`**:
  * IMGUI ベースの全メニューUI描画（タイトル画面、ポーズ画面、リザルト画面、設定画面）
  * UIデザイン共通部品（`DrawModernButton`, `DrawPopCard`, `DrawPopRibbon`, `DrawPopPill`）
* **`RaceManager.HUD.cs`**:
  * レース中HUD描画（順位表示、速度メーター、ミニマップ、アイテム枠、画面分割2P対応バッジ）
* **`RaceManager.Online.cs`**:
  * オンラインメニュー (`DrawNetMenu`)、ロビー待機画面 (`DrawLobby`)、ルームコード管理
* **`RaceManager.Camera.cs`**:
  * レース中およびタイトル/ロビーの動的カメラ追従ロジック
* **`RaceManager.Assets.cs`**:
  * テクスチャ、フォント、オーディオクリップ等の動的リソース読み込み

### 3.2 カート挙動 & 操作 (`Kart`)
* **`Kart.cs`**:
  * カートの基本プロパティ、接地判定、衝突処理、ステータス
* **`Kart.Input.cs`**:
  * キーボード / ゲームパッド入力処理、キーコンフィグの適用
* **`Kart.AI.cs`**:
  * CPU対戦相手の経路追従、ウェイポイント走破、アイテム使用判断
* **`Kart.Visual.cs`**:
  * 車体メッシュ、ホイール回転・接地傾き、ドリフト火花エフェクト、ドライバー頭部アニメーション
* **`Kart.Net.cs`**:
  * ネットワーク補間同期ロジック

### 3.3 その他重要システム
* **`Track.cs` / `Track.Visuals.cs`**: コース生成、路面メッシュ、ウェイポイント、環境装飾
* **`Items.cs`**: アイテム取得・発砲・効果ロジック（ロケット、バナナ、スター等）
* **`NetSession.cs`**: Unity Netcode for GameObjects (NGO) & Unity Relay 接続基盤
* **`RaceAudio.cs`**: BGM、エンジン音、スキール音、環境効果音の管理
* **`TextureGen.cs`**: プロシージャルUIテクスチャ生成ヘルパー
* **`GhostReplay.cs`**: ベストラップ走行ゴーストの記録・再生

---

## 4. UIデザインシステム (Design Guidelines)

ゲーム全体の世界観は、**「明るくポップで親しみやすいクリーンなトイ・アーケード調」**で統一されています。

### 統一コンポーネント & スタイル
1. **カード背景 (`DrawPopCard`)**:
   * タイトル、設定、ポーズ、リザルト、オンラインメニューすべてで白ベースの角丸カードを使用。
   * 暗いSF/サイバー調の背景は使用せず、一貫したホワイトカード調を保つこと。
2. **ヘッダーリボン (`DrawPopRibbon`)**:
   * カード上部に丸みのあるカラフルなリボンバーを配置し、白太文字＋黒アウトラインでタイトルを表示。
3. **ボタン (`DrawModernButton`)**:
   * マウスクリック判定とキーボード/パッドショートカット表記（`[ESC]`, `[ENTER]` 等）を両立。
   * 発色の良いアクセントカラー、下部シャドウ、引き締まった外枠、マウスホバー時の発光を備えた立体ポップボタンを使用。
4. **パフォーマンス上の注意 (IMGUI)**:
   * `OnGUI` 内での `new GUIStyle()` の毎フレーム生成はGC負荷の原因となるため厳禁。
   * スタイル生成には必ず `St(...)` キャッシュメソッド（`styleCache`）を利用すること。

---

## 5. ビルド & 実機検証手順 (Build & Test Workflow)

コード変更やUI修正を行った際は、**必ずヘッドレスビルドを実行し、`-autoshot` によるスクリーンショット撮影で視覚的な検証**を行ってください。

### 5.1 ヘッドレスバッチビルド
以下のコマンドで Unity エディタをバッチモードで実行し、エラーなくビルドが通ることを確認します。

```powershell
cmd.exe /c "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -nographics -projectPath "." -executeMethod BuildTool.BuildWindows -logFile -
```

### 5.2 自動スクリーンショット撮影 (`-autoshot`)
ビルド生成物の `TurboCircuit.exe` に `-autoshot` 引数を渡すと、タイトル・設定・ポーズ・リザルト・ロビー等の全画面スクリーンショットが自動生成されます。

```powershell
cmd.exe /c "pushd Build\Windows && TurboCircuit.exe -autoshot && popd"
```

* **生成先**: `Build/Windows/TurboCircuit_Data/shot_*.png`
  * `shot_01_title.png` (タイトル画面)
  * `shot_02_settings_general.png` (設定: 一般)
  * `shot_03_settings_keys.png` (設定: キーコンフィグ)
  * `shot_06_paused.png` (ポーズ画面)
  * `shot_07_results.png` (リザルト画面)
  * `shot_08_racing_2p.png` (2P画面分割HUD)
  * `shot_10_lobby_4p.png` / `shot_11_lobby_8p.png` (オンラインロビー)
* 画像ファイルを確認し、UI要素の文字被り・レイアウト崩れ・デザインのトーン＆マナー不一致がないか必ず目視検証すること。

---

## 6. コントリビューション・作業時の留意事項

1. **既存ロジックの保護**:
   * カートのドリフト物理挙動、壁との衝突反発計算、オンラインパケット同期の基本構造を安易に変更・破壊しないこと。
2. **ドキュメンテーション**:
   * 新しい機能追加やキーバインド変更時は、[`README.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/README.md) の操作説明表もあわせて更新すること。
