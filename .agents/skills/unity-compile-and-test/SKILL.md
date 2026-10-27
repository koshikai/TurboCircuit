---
name: unity-compile-and-test
description: Use this skill to check C# compile errors via Unity batch mode (compile-check.ps1), run EditMode/PlayMode tests and summarize result XML (run-tests.ps1), call Editor static methods with -executeMethod (execute-method.ps1), or hand-edit .asset/.unity/.meta YAML safely in Turbo Circuit.
---

# Unity コンパイル確認 / テスト / バッチ操作

スクリーンショット検証やビルドは [`build-and-verify`](../build-and-verify/SKILL.md) の `verify.ps1` を使う。本スキルは **「ビルドせずにコンパイルエラーだけ確認したい」「テストを回したい」「Editorメソッドをバッチで叩きたい」** 場合用。

## 0. 共通の前提: エディタが開いていると実行できない

Unity は同一プロジェクトを二重に開けない（`Temp/UnityLockfile` が保持される）。各スクリプトは `Unity.exe` プロセスを検出し、起動中なら案内を出して **終了コード 2** で終わる。
- Unity プロセスが無いのに `Temp/UnityLockfile` だけ残っている場合は、スクリプトが自動削除する（クラッシュ後の残骸）。
- エディタを開いたまま確認したい場合は、エディタの Console を見るか、`%LOCALAPPDATA%\Unity\Editor\Editor.log` を `Select-String -Pattern 'error CS'` で検索する。
- 実行時間の目安: 初回/アセット変更後は数分、通常は1〜2分程度。各スクリプトの `-TimeoutSec` で上限を指定可能。

## 1. コンパイルエラー確認 `compile-check.ps1`

```powershell
pwsh -File .agents/skills/unity-compile-and-test/scripts/compile-check.ps1
```
内部で `Unity.exe -batchmode -nographics -quit -projectPath . -logFile Logs/compile_check.log` を実行し、`error CS` 行のみ重複排除して表示する。
終了コード: `0` OK / `1` コンパイルエラーあり / `2` エディタ起動中 / `3` 異常終了・タイムアウト。
- `ILPPTrigger: Can't find file` 等の一過性エラーで exit 3 になる場合は、`Get-Process "Unity.ILPP*" | Stop-Process -Force` してから再実行（verify.ps1 と同じ対処）。
- フルビルドまで通したい場合は `verify.ps1 -Build`（`BuildTool.BuildWindows`）を使う。

## 2. テスト実行 `run-tests.ps1`

```powershell
pwsh -File .agents/skills/unity-compile-and-test/scripts/run-tests.ps1 -Platform EditMode
pwsh -File .agents/skills/unity-compile-and-test/scripts/run-tests.ps1 -Platform PlayMode -Filter "MyNamespace.KartTests"
```
`-runTests -testPlatform <P> -testResults Logs/tests_<P>.xml` を実行し、NUnit XML から total/passed/failed と失敗テスト名・先頭メッセージを要約する。終了コード: `0` 全成功 / `1` 失敗あり / `2` ロック / `3` 異常・0件 / `4` テスト基盤なし。
`-runTests` では `-quit` を付けない（付けるとテストが走らない）。PlayMode は `-nographics` を付けない（スクリプトが自動で制御）。

### テストが無い場合（現状 `Assets/` にテストは無く、manifest に `com.unity.test-framework` も無い。asmdef は `TurboCircuit.Runtime` と `TurboCircuit.Editor` が導入済み）
最小手順:
1. `Packages/manifest.json` の dependencies に `"com.unity.test-framework"` を追加（バージョンは Package Manager で確認。Unity 6 同梱版が使える）。
2. `Assets/Tests/EditMode/` を作り、Create > Testing > Test Assembly Folder で asmdef を作成（Editor のみ・`UnityEngine.TestRunner`/`UnityEditor.TestRunner` 参照・`overrideReferences` で `nunit.framework.dll`・`defineConstraints: ["UNITY_INCLUDE_TESTS"]`）。
3. **本体は `Assets/Scripts/TurboCircuit.Runtime.asmdef`（`TurboCircuit.Runtime`）に入っているので、テスト asmdef の `references` に `TurboCircuit.Runtime` を追加すれば参照できる。** 型は `public` が前提（`internal` は見えない）。`Track` / `RaceManager` / `Kart` は `partial class` で結合が強く、Runtime はこれ以上細分化しない。テスト導入は別Issue/PRで行うこと。
4. `[Test]` メソッドを書き、上記 `run-tests.ps1` で実行。

## 3. Editor メソッドのバッチ実行 `execute-method.ps1`

```powershell
pwsh -File .agents/skills/unity-compile-and-test/scripts/execute-method.ps1 -Method "BuildTool.Setup"
```
対象は `public static void`（Editor フォルダ内、または `#if UNITY_EDITOR`）。`-executeMethod Class.Method` で呼ばれ、`-quit` で終了する。例外が出ると終了コード非0。ログは `Logs/execute_method.log`。
既存の主なエントリ: `BuildTool.Setup`（シーン再生成＋`ExportAllData`）、`BuildTool.BuildWindows`（Setup＋ビルド）、`DataAssetGenerator.ExportAllData`（いずれも `Assets/Editor/BuildTool.cs`）。
- 新規ツールを書く時は [`unity-editor-tools`](../unity-editor-tools/SKILL.md) の規約（Undo、ドメインリロード等）に従う。バッチでは `AssetDatabase.SaveAssets()` を忘れず、`Application.isBatchMode` なら `EditorApplication.Exit(code)` で終了コードを返す。
- `-nographics` 下では `Camera.Render`/スクリーンショット系は動かない場合がある（そのような処理は verify.ps1 のプレイヤー側を使う）。

## 4. YAML アセット (.asset / .unity / .prefab / .meta) を直接編集する際の注意

- **`.meta` の `guid:` は絶対に変更・再生成しない。** 変わると全参照が切れる。ファイル移動/改名は `.meta` も同時に（`git mv` で両方）。`.meta` 無しで新規ファイルを置いた場合は Unity が生成するので、それをコミットに含める。
- ScriptableObject（`KartData`/`KartMachineData`/`CourseData`）は **クラス名と同名の .cs ファイル** に置く。別名ファイルだと `.asset` の `m_Script` が `{fileID: 0}` になり `Resources.LoadAll` が "referenced script is missing" で読み込まない。
- **`BuildTool.Setup()`（`BuildWindows` 経由も）は `DataAssetGenerator.ExportAllData` で `Assets/Resources/Data/{Karts,Machines,Courses}/*.asset` をコード側既定値（`RaceManager.DefaultKartCharacters` / `Track.DefaultCourses` 等）で毎回上書きする。** `.asset` を手編集してもビルドで消えるので、値を変えたいときはコード側の既定値を直す。例外は `Assets/Resources/Data/CourseDecor/Course_XX.asset`（Course Decor Editor の差分保存、上書きされない）。
- `.unity` は `Setup()` が `Assets/Scenes/Race.unity` を開いて `RaceManager` 以外のルートを作り直す。シーン直編集はほぼ無意味なので、生成コード（`BuildTool.cs`）を直す。
- YAML 編集は Unity が閉じている状態で行う（開いていると上書き/リロード競合）。インデント（スペース2）・`fileID` を保ち、`%YAML 1.1` / `%TAG` ヘッダーを消さない。Force Text シリアライズ前提。
- 編集後は `compile-check.ps1`（または `execute-method.ps1 -Method BuildTool.Setup`）でインポートエラーが出ないか確認し、`git diff` で意図しない差分（GUID・順序変化）が無いか見る。
- `Library/ Temp/ Logs/ Build/` はコミット対象外。
