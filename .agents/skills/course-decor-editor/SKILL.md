---
name: course-decor-editor
description: >-
  Use this skill when hand-tuning course decorations (trees, rocks, signs, buildings, flags, etc.)
  in the Unity Editor, or when changing Track.BuildScenery/BuildSponsorBoards/BuildCircuitProps
  and needing to keep saved manual adjustments working (CourseDecorData / Course Decor Editor).
---

# コース装飾の手動調整 (Course Decor Editor)

コースの装飾物はランタイムでコード生成される（シーンの Hierarchy には存在しない）。
そのため「生成結果を Scene に展開 → 手で調整 → 差分だけ保存 → 実行時に生成直後へ適用」する仕組みを用意している。

## 使い方（Unity Editor）
1. `Race.unity` を開く
2. メニュー `Turbo Circuit > Course Decor Editor`
3. コースを選び **Open** → Hierarchy に `[Course Decor Preview] Course_XX` が出来る（Scene には保存されない）
4. Scene ビューで移動/回転/拡縮、非アクティブ化 or Delete で削除、Ctrl+D で複製
5. **Save** → `Assets/Resources/Data/CourseDecor/Course_XX.asset`（差分のみ）
6. Play / ビルドで自動反映。**Reset** で自動配置に戻る

注意: スクリプト再コンパイル・Play 開始でプレビューは閉じる（未保存の編集は消える）。

## 仕組み
- `Assets/Scripts/Track.Decor.cs` `ApplyDecorOverrides` が `BuildVisuals` 内で `BuildScenery/BuildSponsorBoards/BuildCircuitProps` の直後に実行される。
- 対象は **その3つの呼び出し中に Track 直下へ作られたスクリプト無しオブジェクト**（アニメーション用 MonoBehaviour 付きは除外）。路面・壁・地形・トンネル・橋脚・ジャンプ台・動物は対象外（形状は `CourseData` の制御点で調整）。
- キーは `名前@(x,y,z を0.1m丸め)`（同キーは `#n` 連番）。生成位置が変わると該当調整は一致せず、実行時に警告ログが出る。
- `Track` 内の `Destroy` は `Track.Decor.cs` で `SafeDestroy` に差し替え済み（編集モードの `BuildVisuals` を成立させるため）。

## 生成コードを変えるとき
- 乱数シード・配置ロジックを変えると既存の調整が一致しなくなる。変更後は対象コースを Open して確認し、必要なら Save し直す。
- 新しい装飾ビルダーを追加する場合は、`BuildVisuals` の `decorStart` 〜 `ApplyDecorOverrides` の範囲内で呼ぶ。
- 生成は決定的であること（`UnityEngine.Random` / `Time` を使わない）。

## ハマりどころ（過去の不具合）
- **プレビューが紫になる**: プレビューは `DontSaveInEditor` のオブジェクト。マテリアルは実行時生成で保存されず、再コンパイルやシーン入れ替え（`NewScene`＝`Build Windows` / `Setup`）で壊れた残骸が Hierarchy に出ないまま残る。`CourseDecorWindow` の `DestroyAllPreviews`（起動時・Play 開始・シーン切替・`Setup()` 冒頭）が `hideFlags == DontSaveInEditor` で拾って消す。手動なら `Turbo Circuit > Clean Course Decor Preview`。
- **`.asset` が「script is missing」**: ScriptableObject をクラス名と別のファイルに置くと発生。`CourseDecorData` 等は同名ファイルに置くこと（`KartData` / `CourseData` も同様に分離済み）。
- 調整データを入れた `.asset` の `m_Script` が `{fileID: 0}` なら、型の入ったファイル名とクラス名を確認する。
- Unity が非フォーカスだと再コンパイルされない。`Assets > Refresh`（Ctrl+R）で促す。

## 検証
`verify.ps1 -Course N -Progress "..." -NoHud`（build-and-verify スキル）で調整前後を撮影して比較する。
