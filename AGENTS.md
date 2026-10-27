# AGENTS.md

Turbo Circuit プロジェクトにおける AI エージェント（Antigravity, Claude Code, Cursor 等）向けの開発指示・アーキテクチャ規約・検証手順ドキュメントです。

---

## 1. プロジェクト概要

* **プロジェクト名**: Turbo Circuit
* **エンジン**: Unity 6000.6.5f1 (Universal Render Pipeline / URP)
* **プラットフォーム**: Windows (64-bit)
* **ジャンル**: 3D カートレーシングゲーム（1Pソロ / 2P画面分割対戦 / 最大8人オンライン対戦）
* **特徴**:
  * 7コース（TURBO CIRCUIT, SUNSET DUNES, FROST PEAK, NEON METROPOLIS, HOKKAIDO CAMPUS, SKYLINE OVERPASS, CANYON SPLIT）
  * 10ドライバー（OOBI, OODI, OOLI, OOPI, OOZI, PANDA, PENGUIN, FOX, BUNNY, LION）× 8カートマシン（STANDARD, PIPE BUGGY, MACH FORMULA, MONSTER, CYBER GT, ROADSTER, MUSCLE V8, JET POD）の自由組み合わせ
  * ドリフトブースト、アイテムバトル、ジャンプ台、ゴースト再生
  * Unity Relay / Direct IP によるオンラインマルチプレイ対応

---

## 2. 開発環境 & 基本ルール

* **Unity エディタ実行パス**:
  `C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe`
* **Python の実行**:
  Python スクリプトを実行する際は、**必ず `uv` を使用すること**（例: `uv run python ...`）。
* **パッケージマネージャ**:
  npm ではなく**なるべく `bun` を使用すること**。
* **マシン環境**:
  ホスト名 `DESKTOP-S12GI1G` (Windows)

---

## 3. アーキテクチャ & コードベース構成

スクリプトは `Assets/Scripts/` 配下に配置され、関心事ごとに `partial class` で機能分割されています。

**アセンブリ構成 (asmdef)**: `Assets/Scripts/` は `TurboCircuit.Runtime`、`Assets/Editor/` は `TurboCircuit.Editor`（Editor 専用・Runtime を参照）に分かれている。新しい外部パッケージの型を Runtime で使うときは `TurboCircuit.Runtime.asmdef` の `references` にそのパッケージの asmdef 名を追加すること。Runtime から `UnityEditor` を参照しないこと。`Track` / `RaceManager` / `Kart` は `partial class` で強く結合しているため、Runtime をこれ以上細分化しない。

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
  * 入力は **Input System** のみ（`ProjectSettings` の Active Input Handling は新方式のみ）。キーボード / マウス / ゲームパッド補助経路は **`GameInput`（`GameInput.GetKey` など）経由**で読み、旧 `Input.*` は使わない（新方式のみのため例外になる）。キーコンフィグは `KeyCode` のまま保存し、`KeyCodeMap` で `Key` に変換する。新しいキーを扱うときは `KeyCodeMap` の対応表と `KeyCodeMapTests` を更新すること。ゲームパッドは `GamepadHaptics`（Input System の `Gamepad` ベース。P1 = `Gamepad.all[0]`、P2 = `Gamepad.all[1]`）が XInput 互換の値（ボタンのビットマスク、トリガー 0〜255、スティック ±32767）に変換して返す。振動は `Gamepad.SetMotorSpeeds`。変換は `GamepadHapticsTests`（`InputTestFixture`）で固定している。
* **`Kart.AI.cs`**:
  * CPU対戦相手の経路追従、ウェイポイント走破、アイテム使用判断
* **`Kart.Visual.cs`**:
  * 車体メッシュ、ホイール回転・接地傾き、ドリフト火花エフェクト、ドライバー頭部アニメーション
* **`Kart.Net.cs`**:
  * ネットワーク補間同期ロジック

### 3.3 データ定義・エディタ拡張 & コース装飾の調整
* **`KartData.cs` / `KartMachineData.cs` / `CourseData.cs`**: ScriptableObject 定義（`Assets/Resources/Data/` の `.asset` の型）。
  * **ScriptableObject は必ずクラス名と同名のファイルに置くこと**。別ファイルに置くと `.asset` の `m_Script` が `{fileID: 0}` になり、`Resources.LoadAll` が「referenced script is missing」警告を出して読み込まれない。
  * `BuildTool` の `Setup()`（`Build Windows` 経由を含む）は `ExportAllData` で、これらの `.asset` をコード側の既定値（`Track.DefaultCourses` など）で毎回上書きする。手編集した値は残らない。
* **`CourseDecor.cs` / `CourseDecorData.cs` / `DecorTag.cs` / `Track.Decor.cs`**: コース装飾（`BuildScenery` / `BuildSponsorBoards` / `BuildCircuitProps` が作る Track 直下のオブジェクト）の手動調整。`Assets/Editor/CourseDecorWindow.cs`（メニュー `Turbo Circuit > Course Decor Editor`）で編集し、`Assets/Resources/Data/CourseDecor/Course_XX.asset` に差分保存、実行時に生成直後へ適用する。詳細は [`.agents/skills/course-decor-editor/SKILL.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/.agents/skills/course-decor-editor/SKILL.md)。
  * `Track` 内の `Destroy` は `Track.Decor.cs` で `SafeDestroy` に差し替えてある（編集モードで `BuildVisuals` を動かすため）。
* **エディタ拡張ツールの開発・保守指針**:
  * エディタスクリプト（`EditorWindow`、インスペクター、自動化）の開発ルール（Undo登録、`EditorGUI.BeginChangeCheck`、ドメインリロード対応、ランタイム分離）は [`.agents/skills/unity-editor-tools/SKILL.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/.agents/skills/unity-editor-tools/SKILL.md) を参照。

### 3.4 その他重要システム
* **`Track.cs` / `Track.Visuals.cs`**: コース生成、路面メッシュ、ウェイポイント、環境装飾。URP描画負荷やプロシージャルメッシュの最適化指針は [`.agents/skills/urp-graphics-optimization/SKILL.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/.agents/skills/urp-graphics-optimization/SKILL.md) を参照。
* **`Items.cs`**: アイテム取得・発砲・効果ロジック（ロケット、バナナ、スター等）
* **`NetSession.cs`**: Unity Netcode for GameObjects (NGO) & Unity Relay 接続基盤。通信同期、帯域削減、RPC/NetworkVariableの設計規約は [`.agents/skills/unity-multiplayer-patterns/SKILL.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/.agents/skills/unity-multiplayer-patterns/SKILL.md) を参照。
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

コード変更・UI修正・3Dモデル調整を行った際は、スキル [`.agents/skills/build-and-verify/SKILL.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/.agents/skills/build-and-verify/SKILL.md) の **`verify.ps1`** を用いて視覚的な検証を行ってください。

### 5.1 ピンポイント高速検証（推奨：約1〜2秒）
フル撮影（`-autoshot`）を回さず、変更対象のみを即座に撮影・合成（2×2コンタクトシート化）して確認できます。

```powershell
# 1. 3Dモデル4面図スタジオ（1m基準グリッド＆高さルーラー＋正確なBounds数値出力）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotModel "kart:5:2"
# 他対象例: "kart:all", "driver:all", "animal:cow", "fan:0", "item:all"

# 2. コース任意地点のピンポイント確認（複数地点を2x2シートに自動合成）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Course 4 -Progress "0.106,0.29,0.555,0.82" -NoHud

# 3. 特定UI画面のみの高速確認
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -ShotUi "title,results"
```

* **生成先**: `Build/Windows/TurboCircuit_Data/verify_*.png` および `verify_model_bounds.txt`

### 5.2 フル回帰スクリーンショット撮影 (`-autoshot`)
全体的な回帰確認時は `-AutoShot`（または `TurboCircuit.exe -autoshot`）で全画面スクリーンショットを一括生成します。

```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -AutoShot
```

* **生成先**: `Build/Windows/TurboCircuit_Data/shot_*.png`
* 画像ファイルを確認し、UI要素の文字被り・レイアウト崩れ・デザインのトーン＆マナー不一致がないか必ず目視検証すること。

### 5.3 ユニットテスト (EditMode)
純粋ロジック（ネット同期のパック形式、既定データの整合性、`Loc` など）は `Assets/Tests/EditMode/`（`TurboCircuit.Tests.EditMode`）の NUnit テストで固定している。Unity エディタを閉じた状態で次を実行する。

```powershell
pwsh -File .agents/skills/unity-compile-and-test/scripts/run-tests.ps1 -Platform EditMode
```

* コース / ドライバー / マシンを追加・削除したら、`DefaultDataTests` の期待件数（`ExpectedCourses` など）も更新すること。
* テストを追加するときは [`unity-compile-and-test`](.agents/skills/unity-compile-and-test/SKILL.md) の手順に従う。

---

## 6. コントリビューション・作業時の留意事項

1. **GitHub フロー（Issue → ブランチ → PR）の徹底**:
   * **バグ修正を行う際は、必ず先に GitHub Issue を作成（`gh issue create`）し、原因・再現状況を整理してから作業に入ること**。
   * `main` ブランチへ直接コミット・直接 Push せず、作業ブランチ（`fix/〇〇`, `feat/〇〇`, `docs/〇〇` 等）を切って修正・検証し、Issue を紐付けた **Pull Request (`Closes #<番号>`)** を作成すること（詳細は [`CONTRIBUTING.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/CONTRIBUTING.md) を参照）。
2. **既存ロジックの保護**:
   * カートのドリフト物理挙動、壁との衝突反発計算、オンラインパケット同期の基本構造を安易に変更・破壊しないこと。
3. **ドキュメンテーション**:
   * 新しい機能追加やキーバインド変更時は、[`README.md`](file:///c:/Users/kaito/UnityProjects/TurboCircuit/README.md) の操作説明表もあわせて更新すること。
4. **スキル（Skills）への知識・手順の切り出し**:
   * 新たな作業・検証手順・アセット生成フロー・コース/キャラ追加手順・トラブルシューティングなどを行った際は、次回以降も高い再現性で実行できるよう、積極的に **`.agents/skills/<skill-name>/SKILL.md`** へスキルとして切り出し・更新を行うこと。
   * スキルは YAML フロントマター（`name`, `description`）を記載し、再利用可能なスクリプトがある場合は `.agents/skills/<skill-name>/scripts/` に格納すること。



---

## 7. スキル一覧 (`.agents/skills/`)

作業内容に合うスキルの `SKILL.md` を先に読んでから着手すること。

| スキル | 使う場面 |
|---|---|
| [`build-and-verify`](.agents/skills/build-and-verify/SKILL.md) | ビルド、スクリーンショット検証、3Dモデル4面図、コース地点確認 |
| [`unity-compile-and-test`](.agents/skills/unity-compile-and-test/SKILL.md) | バッチモードのコンパイルエラー確認、テスト実行、`-executeMethod`、`.asset`/`.meta` の直接編集 |
| [`add-game-content`](.agents/skills/add-game-content/SKILL.md) | コース・ドライバー・カート・アイテムの追加手順と落とし穴 |
| [`imgui-ui-design`](.agents/skills/imgui-ui-design/SKILL.md) | IMGUI の画面・メニュー・HUD の追加/変更、入力処理、デザイン規約 |
| [`github-workflow`](.agents/skills/github-workflow/SKILL.md) | Issue → ブランチ → PR、コミット規約、リリース手順 |
| [`course-decor-editor`](.agents/skills/course-decor-editor/SKILL.md) | コース装飾の手動調整 |
| [`unity-editor-tools`](.agents/skills/unity-editor-tools/SKILL.md) | エディタ拡張・ビルドツールの開発 |
| [`unity-multiplayer-patterns`](.agents/skills/unity-multiplayer-patterns/SKILL.md) | オンライン同期まわり |
| [`urp-graphics-optimization`](.agents/skills/urp-graphics-optimization/SKILL.md) | URP・マテリアル・プロシージャルメッシュの最適化 |

Claude Code では `.claude/skills` から `.agents/skills` へのリンクを作ると直接呼び出せる（Windows では開発者モードが必要。リンク自体はコミットしない運用でもよい）。
