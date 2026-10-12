# Contributing to Turbo Circuit 🏎️🏁

**Turbo Circuit** の共同開発ガイドです。
新しく参加するメンバーの環境構築手順、ブランチ運用ルール、ファイル分担、および Unity 特有の注意点をまとめています。

---

## 1. 環境構築（オンボーディング手順）

### 必須ツール
1. **Unity Hub & Unity `6000.6.5f1`**
   * **必ず全員同じバージョン (`6000.6.5f1`) を使用してください**（別バージョンで開くと `ProjectSettings` 等が書き換わり、競合の原因になります）。
   * Unity Hub から `6000.6.5f1`（Windows Build Support (IL2CPP / Mono) 含む）をインストールします。
   * 標準インストールパス: `C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe`
2. **Git**（Windows の場合は Git for Windows）
3. **PowerShell 7 (`pwsh`)**（ビルド＆自動スクリーンショット検証スクリプト用）

### クローン〜初回起動手順
```powershell
# 1. リポジトリをクローン
git clone https://github.com/koshikai/TurboCircuit.git
cd TurboCircuit

# 2. コマンドラインから初回ビルド＆検証が通るかテスト（約1〜2分）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title"
```
Unity Editor で開く場合は、Unity Hub の **Add > Add project from disk** から `TurboCircuit` フォルダを選択し、`Assets/Scenes/Race.unity` を開いて ▶ (Play) を押すとゲームが起動します。

---

## 2. ブランチ運用ルール（GitHub Flow）

チーム開発でのコンフリクトやビルド破損を防ぐため、**GitHub Flow** で運用します。

### 基本方針
* **`main` ブランチは常にビルドが通り、正常に遊べる状態に保つ**
* **`main` ブランチへの直接コミット・直接プッシュは禁止**
* **バグ修正を行う際は、必ず先に GitHub Issue を起票してから修正ブランチ（`fix/〇〇`）を切り、Pull Request (PR) を出す**
* 機能追加や修正を行う際は、必ず `main` から作業用ブランチを切り、**Pull Request (PR)** 経由でマージする

### ブランチ命名規則
| プレフィックス | 用途 | ブランチ名の例 |
| :--- | :--- | :--- |
| `feat/〇〇` | 新機能・新コース・新キャラの追加 | `feat/course-7-volcano`, `feat/item-lightning` |
| `fix/〇〇` | バグ修正・挙動修正（先に Issue を作成） | `fix/drift-wall-bounce`, `fix/2p-hud-overlap` |
| `ui/〇〇` | メニュー・HUD・演出の調整 | `ui/title-card-animation`, `ui/minimap-icons` |
| `decor/〇〇` | コース装飾（木・看板・建物等）の配置調整 | `decor/course-4-trees`, `decor/course-1-signs` |
| `docs/〇〇` | ドキュメント・ツールの整備 | `docs/update-readme` |

### 作業の流れ（コマンド手順）

```powershell
# ⓪ バグ修正の場合は、先に GitHub Issue を作成する（Web UI または gh CLI）
gh issue create --title "bug: 〇〇の不具合" --body "再現手順と原因..."

# ① 作業を始める前に、必ず最新の main を取得する
git switch main
git pull origin main

# ② 作業ブランチを作成して切り替える
git switch -c fix/my-bugfix

# ③ コード編集・アセット調整を行う
# 変更後は verify.ps1 でビルド＆画面確認！
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title,results"

# ④ 変更ファイルを確認（.meta ファイルの漏れがないかチェック）
git status

# ⑤ ステージング＆コミット＆プッシュ
git add .
git commit -m "fix: 〇〇を修正 (#Issue番号)"
git push -u origin fix/my-bugfix

# ⑥ Pull Request を作成（Issue 番号を Closes #番号 で紐付け）
gh pr create --title "fix: 〇〇を修正" --body "Closes #<Issue番号>"
```
プッシュ後、GitHub リポジトリページまたは `gh pr create` で **Pull Request** を作成してください。
GitHub Actions による自動チェック（`.meta` ファイルの過不足やコンフリクトマーカー検査）が通り、レビュー完了後に `main` へマージします。

---

## 3. コンフリクトを防ぐファイル分担ガイド

本作のコードは `Assets/Scripts/` 配下に配置され、関心事ごとに `partial class` で分割されています。
**同時に同じファイルを触らないように担当を分ける**ことで、マージ時の競合（コンフリクト）をほぼゼロにできます。

### 担当領域とファイル対応表

| 担当領域 | 主に編集するファイル | 備考 |
| :--- | :--- | :--- |
| **ゲーム進行・ルール** | [`Assets/Scripts/RaceManager.cs`](Assets/Scripts/RaceManager.cs) | ステート管理・周回・順位計算 |
| **メニューUI** | [`Assets/Scripts/RaceManager.UI.cs`](Assets/Scripts/RaceManager.UI.cs) | タイトル・ポーズ・リザルト・設定画面 |
| **レース中HUD** | [`Assets/Scripts/RaceManager.HUD.cs`](Assets/Scripts/RaceManager.HUD.cs) | 速度計・ミニマップ・順位・アイテム枠 |
| **カメラワーク** | [`Assets/Scripts/RaceManager.Camera.cs`](Assets/Scripts/RaceManager.Camera.cs) | 追従カメラ・リザルトカメラ |
| **オンラインUI・通信** | [`Assets/Scripts/RaceManager.Online.cs`](Assets/Scripts/RaceManager.Online.cs), [`Assets/Scripts/NetSession.cs`](Assets/Scripts/NetSession.cs), [`Assets/Scripts/Kart.Net.cs`](Assets/Scripts/Kart.Net.cs) | ロビー・Relay接続・パケット同期 |
| **カート物理・入力** | [`Assets/Scripts/Kart.cs`](Assets/Scripts/Kart.cs), [`Assets/Scripts/Kart.Input.cs`](Assets/Scripts/Kart.Input.cs) | ドリフト・接地・衝突・パッド入力 |
| **CPU AI挙動** | [`Assets/Scripts/Kart.AI.cs`](Assets/Scripts/Kart.AI.cs) | ウェイポイント追従・アイテム判断 |
| **カート3D見た目** | [`Assets/Scripts/Kart.Visual.cs`](Assets/Scripts/Kart.Visual.cs) | 車体生成・タイヤ回転・火花エフェクト |
| **コース形状・装飾生成** | [`Assets/Scripts/Track.cs`](Assets/Scripts/Track.cs), [`Assets/Scripts/Track.Visuals.cs`](Assets/Scripts/Track.Visuals.cs) | コース制御点・路面生成・プロシージャル装飾 |
| **コース装飾の手動調整** | `Assets/Resources/Data/CourseDecor/Course_00.asset` 〜 `Course_05.asset` | コースごとにファイルが独立しているため、**コース番号で分担すれば競合しません** |
| **アイテム・サウンド** | [`Assets/Scripts/Items.cs`](Assets/Scripts/Items.cs), [`Assets/Scripts/RaceAudio.cs`](Assets/Scripts/RaceAudio.cs) | アイテム効果・BGM/SE管理 |

---

## 4. 必ず守るべきプロジェクト規約（重要！）

### ① `.meta` ファイルは必ずセットでコミットする
* Unity でファイルやフォルダを追加・移動・削除すると、同名の `.meta` ファイルが生成・変更されます。
* `.meta` には Unity 内部の GUID が記録されているため、コミットし忘れると他のメンバーの環境で参照が壊れます。
* **ファイルを移動・リネームする際は、エクスプローラーではなく Unity Editor 上の Project ウィンドウ内で行ってください**（`.meta` も自動で追従します）。

### ② `Assets/Resources/Data/` 直下の `.asset` を直接手編集しない
* `Assets/Resources/Data/Karts/`、`Machines/`、`Courses/` 内の `.asset` ファイルは、ビルド時（`BuildTool.Setup()`）に **C# コード側の既定値で毎回上書き再生成** されます。
* そのため、Unity の Inspector 上でステータスやコース座標を手編集しても、次回ビルド時に消えてしまいます。
* ステータスやコース形状を変更したい場合は、C# コード側（`Track.DefaultCourses` など）を編集してください。
* **例外**: `Assets/Resources/Data/CourseDecor/Course_XX.asset`（コース装飾の手動配置データ）は上書きされないため、Unity メニュー `Turbo Circuit > Course Decor Editor` から編集・保存してコミットして問題ありません。

### ③ UI デザインの統一と IMGUI パフォーマンス
* トーン＆マナーは **「明るくポップで親しみやすいクリーンなトイ・アーケード調」** で統一しています。
* 新しいメニューやダイアログを作る際は、`RaceManager.UI.cs` の共通部品（`DrawPopCard`, `DrawPopRibbon`, `DrawModernButton`, `DrawPopPill`）を使用してください。
* `OnGUI` 内で毎フレーム `new GUIStyle()` を呼ぶと GC 負荷が発生するため、必ず `St(...)` キャッシュメソッドを使用してください。

---

## 5. ピンポイント検証ツール (`verify.ps1`) の使い方

変更を加えた後は、以下のコマンドで素早くビルド＆スクリーンショット確認ができます。

```powershell
# 1. 特定UI画面の確認（例: タイトル画面とリザルト画面）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title,results"

# 2. コースの特定地点の確認（例: コース4の4地点を2x2シートで撮影）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -Course 4 -Progress "0.1,0.3,0.6,0.8" -NoHud

# 3. 3Dモデルの4面図確認（例: ドライバー5 × カート2）
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotModel "kart:5:2"
```
出力画像は `Build/Windows/TurboCircuit_Data/verify_*.png` に保存されます。PR 作成時に添付するとレビューがスムーズになります！

---

## 6. リポジトリ管理者向け：GitHub 初期設定手順

共同開発を始める際、リポジトリオーナー（管理者）は以下の設定を GitHub 上で行うことを推奨します。

1. **メンバー（Collaborators）の招待**
   * GitHub リポジトリの **Settings > Collaborators > Add people** から、開発メンバーの GitHub アカウントを招待します。
2. **`main` ブランチの保護（Branch Protection Rule）**
   * **Settings > Branches > Add branch ruleset (または Add branch protection rule)** を開きます。
   * Branch name pattern に `main` を指定し、以下を有効化します：
     * ✅ **Require a pull request before merging**（PR なしでの直接 push を防止）
     * ✅ **Require status checks to pass before merging**（`Check .meta files & Conflict markers` の自動検査パスを必須化）
