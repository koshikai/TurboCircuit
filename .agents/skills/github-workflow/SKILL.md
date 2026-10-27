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

## 5. リリース手順
直近の `chore(release): bump version to v2.2.0 (Closes #65)`（コミット 5b1ddd9）が触ったファイル:
1. `Assets/Editor/BuildTool.cs` — `PlayerSettings.bundleVersion = "X.Y.Z"`
2. `Assets/Scripts/AppUpdater.cs` — `CurrentVersion = "vX.Y.Z"`
3. `ProjectSettings/ProjectSettings.asset` — `bundleVersion: X.Y.Z`
4. `README.md` — Version バッジ（`Version-vX.Y.Z-purple`）
5. `version.json` — version / name / published_at / download_url / size（zip の実バイト数）/ notes（英語+日本語、関連 Issue 番号入り）

流れ: リリース用 Issue 作成 → `chore/release-vX.Y.Z` ブランチ → 上記5ファイル更新 → `Tools/package_release.py`（uv で実行: `uv run python Tools/package_release.py`）で `TurboCircuit-win64.zip` を生成し size を反映 → PR（`Closes #番号`）→ マージ後に GitHub Release `vX.Y.Z` へ zip を添付（`gh release create` 等）。
`*.zip` は .gitignore 済みなのでコミットされない。

## 6. Unity 特有の注意
* `Library/ Temp/ Obj/ Build/ Builds/ Logs/ UserSettings/ MemoryCaptures/ Recordings/`、`*.csproj *.sln *.zip` 等は .gitignore 済み。コミットしない（`git status` で現れたら .gitignore を確認）。
* アセット/フォルダの追加・移動・削除は `.meta` も必ず同じコミットに含める。移動は Unity Editor 上で行う。
* Unity は必ず `6000.6.5f1`。別バージョンで開いて `ProjectSettings/` が書き換わった差分はコミットしない。
* `Assets/Resources/Data/` 直下の `.asset` は `BuildTool.Setup()` で上書きされる（手編集不可）。`CourseDecor/Course_XX.asset` のみコミットして良い。
* 新機能・キー操作変更時は `README.md` の操作説明も更新。
