---
name: github-workflow
description: >-
  Turbo Circuit の Issue → ブランチ → PR の進め方、ブランチ名・コミットメッセージ規約、リリース（バージョン更新）手順、
  Unity 特有のコミット注意点（.meta / Library / Build）。バグ修正・機能追加・リリース作業・PR作成時に使用する。
---

# GitHub ワークフロー (github-workflow)

詳細な背景は `AGENTS.md` の「6. コントリビューション」と `CONTRIBUTING.md` を参照。ここは実行手順の要約。

## 鉄則
* `main` への直接コミット・直接 push は禁止。必ず作業ブランチ + PR。
* バグ修正は **先に Issue を作る**。その後ブランチを切る。
* PR 本文に `Closes #<番号>` を入れる（`.github/PULL_REQUEST_TEMPLATE.md` に従う）。
* ユーザーから明示された時以外、issue/PR 作成・push・マージはしない（エージェントは確認を取る）。

## 1. Issue 作成（バグ修正は必須）
Issue テンプレートは無い。次の構成で書く。

```powershell
gh issue create --label bug --title "bug: 〇〇で△△になる" --body @'
## 現象
（何が起きるか。コース/キャラ/モード等の条件）
## 再現手順
1. ...
## 原因（判明分）
（関連ファイル・関数名。未確定なら「調査中」）
## 期待する挙動
'@
```
機能追加は `--label enhancement`、ドキュメントは `--label documentation`。使えるラベル: bug / enhancement / documentation / question 等。

## 2. ブランチ
```powershell
git switch main; git pull origin main
git switch -c fix/drift-wall-bounce
```
| 接頭辞 | 用途 |
| :--- | :--- |
| `feat/` | 新機能・新コース・新キャラ |
| `fix/` | バグ修正（Issue 先行） |
| `ui/` | メニュー・HUD・演出 |
| `decor/` | コース装飾調整 |
| `docs/` | ドキュメント・スキル |
| `chore/` | リリース・雑務（例: `chore/release-v2.2.0`） |
| `tune/` | バランス調整（履歴にあるが CONTRIBUTING 未記載） |

## 3. コミットメッセージ
形式: `type(scope): 日本語の要約 (Closes #番号)`（scope は省略可。日本語が多い）。type は feat / fix / tune / chore / docs / merge。
実例:
* `fix(ai): CANYON SPLITの分岐区間におけるCPU挙動異常と見えない分離帯反発を修正 (Closes #64)`
* `feat(audio): Hokkaido CampusのBGMを高品位なトランス楽曲に刷新 (Closes #50)`
* `tune(kart): ドリフト第1段階の蓄積時間...のバランス調整 (Closes #51)`
* `chore(release): bump version to v2.2.0 (Closes #65)`
* Issue に紐付くだけなら `(#62)` も使われている。

## 4. 検証 → コミット → PR
```powershell
pwsh -File .agents/skills/build-and-verify/scripts/verify.ps1 -Build -ShotUi "title,results"   # 変更に合わせて
git status                       # .meta 漏れ・不要ファイル確認
git add <ファイルを明示>          # git add . は Library 等の混入に注意
git commit -m "fix(ai): ... (Closes #64)"
git push -u origin fix/xxx
gh pr create --title "fix(ai): ..." --body-file pr.md   # 本文はテンプレ準拠
```
PR 本文はテンプレの項目（概要 / `Closes #` / 変更の種類 / 検証スクリーンショット / チェックリスト）を埋める。UI・モデル・コース変更は `verify_*.png` を添付。
CI（`.github/workflows/unity-meta-check.yml`）が PR/push to main で走る: Unity バージョン固定(6000.6.5f1)、衝突マーカー、ScriptableObject 参照/ファイル名、.meta 同期、Material、IMGUI/SafeDestroy 規約、Roslyn 構文。確認は `gh pr checks`。

## 4.5 マージ方針
`main` は保護されている（必須チェック `Check .meta files & Conflict markers`、管理者にも適用、force push/削除禁止）。チェックが通るまでマージできない。

- **docs のみの PR**（`*.md`、`.agents/**` だけを触る）: 作成後に `gh pr merge <番号> --auto --merge` で auto-merge を付けてよい（履歴は merge commit 形式）。付ける前に `gh pr diff <番号> --name-only` で docs 以外が混ざっていないか確認する。
- **コード・アセット・asmdef を含む PR**: auto-merge は付けず、ユーザーが GitHub 上で手動マージする。
- 直接マージ（`--auto` なしの `gh pr merge`）は権限判定で拒否されることがある。回避せず、ユーザーにマージを頼む。

## 5. リリース手順
直近の `chore(release): bump version to v2.2.0 (Closes #65)`（コミット 5b1ddd9）が触ったファイル:
1. `Assets/Editor/BuildTool.cs` — `PlayerSettings.bundleVersion = "X.Y.Z"`
2. `Assets/Scripts/AppUpdater.cs` — `CurrentVersion = "vX.Y.Z"`
3. `ProjectSettings/ProjectSettings.asset` — `bundleVersion: X.Y.Z`
4. `README.md` — Version バッジ（`Version-vX.Y.Z-purple`）
5. `version.json` — version / name / published_at / download_url / size（zip の実バイト数）/ notes（英語+日本語、関連 Issue 番号入り）

流れ: リリース用 Issue 作成 → `chore/release-vX.Y.Z` ブランチ → 上記5ファイル更新 → `Tools/package_release.py`（uv で実行: `uv run python Tools/package_release.py`）で `TurboCircuit-win64.zip` を生成し size を反映 → PR（`Closes #番号`）→ マージ後に GitHub Release `vX.Y.Z` へ zip を添付（`gh release create` 等）。
`*.zip` は .gitignore 済みなのでコミットされない。

v2.2.1 で実際に行った順序（検証済み）:
1. Unity を閉じて `verify.ps1 -Build` → `uv run python Tools/package_release.py`。zip は約 65MB 前後が目安（`-AutoShot` の `shot_*.png` は除外される。膨らんだら中身を確認）。
2. `version.json` の `size` に zip の実バイト数、`notes` に英日の更新内容を書き、リリース PR を作成する。
3. **先に GitHub Release をドラフトで作る**: `gh release create vX.Y.Z TurboCircuit-win64.zip --draft --target main --title "Turbo Circuit vX.Y.Z" --notes-file <notes>`。ドラフトの間はタグが作られない。
4. PR をマージする（`version.json` が `main` に入ると `AppUpdater` が参照するため、zip が公開済みでないと 404 になる。マージから公開までは短く）。
5. `gh release edit vX.Y.Z --draft=false --latest` で公開する。タグはマージ後の `main` に付く。`git ls-remote --tags origin vX.Y.Z` が `origin/main` と一致することを確認する。

注意: `--notes-file` は UTF-8（BOM なし）で作る。Windows の Python で `print` すると cp932 で落ちる（`PYTHONIOENCODING=utf-8` か PowerShell の `[IO.File]::WriteAllText` を使う）。コマンドを `;` で繋ぐと失敗後も後続が走るので、リリース作成は `&&` で繋ぐこと。

## 6. Unity 特有の注意
* `Library/ Temp/ Obj/ Build/ Builds/ Logs/ UserSettings/ MemoryCaptures/ Recordings/`、`*.csproj *.sln *.zip` 等は .gitignore 済み。コミットしない（`git status` で現れたら .gitignore を確認）。
* アセット/フォルダの追加・移動・削除は `.meta` も必ず同じコミットに含める。移動は Unity Editor 上で行う。
* Unity は必ず `6000.6.5f1`。別バージョンで開いて `ProjectSettings/` が書き換わった差分はコミットしない。
* `Assets/Resources/Data/` 直下の `.asset` は `BuildTool.Setup()` で上書きされる（手編集不可）。`CourseDecor/Course_XX.asset` のみコミットして良い。
* 新機能・キー操作変更時は `README.md` の操作説明も更新。
