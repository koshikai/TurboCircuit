---
name: unity-editor-tools
description: >-
  Use this skill when developing or modifying custom Unity Editor tools, EditorWindows,
  inspectors, or automated build/pipeline scripts (e.g., CourseDecorWindow, BuildTool, CourseDecorData).
---

# Unity Editor ツール開発・自動化ガイド (unity-editor-tools)

Turbo Circuit におけるエディタ拡張ツール（`CourseDecorWindow`、`BuildTool`、カスタムインスペクター、自動化パイプライン）を開発・保守する際の標準規約とベストプラクティスです。

---

## 1. エディタ/ランタイムの完全分離（最重要）

* **フォルダ配置**:
  * エディタ専用コードは必ず `Assets/Editor/` 配下に配置すること。
  * ランタイムスクリプト内でエディタ機能に触れる必要がある場合は、必ず `#if UNITY_EDITOR` ... `#endif` でガードすること。
* **ビルド失敗の防止**:
  * `UnityEditor` 名前空間の API（`EditorUtility`, `Selection`, `Undo`, `AssetDatabase` 等）をスタンドアロンビルド（`verify.ps1 -Build`）に含まれるコードから直接呼び出さないこと。
  * `AssetDatabase.LoadAssetAtPath` はエディタ専用。実行時は `Resources.Load` やシリアライズ済み参照を使用する。

---

## 2. Undo（元に戻す）とデータ更新の作法

エディタツールでシーン内オブジェクトやアセットを変更する際は、必ず Unity の Undo システムに統合し、安全にダーティマーク（変更保存フラグ）を付けること。

### 基本パターン
```csharp
// 1. 変更検出の開始
EditorGUI.BeginChangeCheck();

// 2. UIコントロールの描画
var newPos = EditorGUILayout.Vector3Field("Position", targetObj.transform.position);

// 3. 変更があった場合のみ Undo 登録とプロパティ更新
if (EditorGUI.EndChangeCheck())
{
    // 変更直前に RecordObject を呼ぶ（変更前状態が Undo スタックに保存される）
    Undo.RecordObject(targetObj.transform, "Move Object");
    targetObj.transform.position = newPos;

    // シーンやアセットに変更フラグを立てる（無条件に毎フレーム呼ばない）
    EditorUtility.SetDirty(targetObj);
}
```

### オブジェクトの生成・削除
* **生成時**: `Undo.RegisterCreatedObjectUndo(newGameObject, "Create Object");`
* **削除時**: `Undo.DestroyObjectImmediate(targetGameObject);`（通常の `Destroy` や `DestroyImmediate` は Undo できない）

---

## 3. ドメインリロード耐性（State Persistence）

スクリプトが再コンパイルされたり、Play モードに出入りするとエディタの C# ドメインリロードが発生し、通常のフィールド変数はリセットされます。

* **永続化ルール**:
  * 保持したいウィンドウ状態は `[SerializeField] private ...` を付与するか、`EditorPrefs` を使用する。
  * 一時プレビュー用オブジェクトには `hideFlags = HideFlags.DontSaveInEditor` を適切に設定し、ドメインリロード時やシーン破棄時に残骸が残らないよう `DestroyAllPreviews` クリーンアップを用意する（`CourseDecorWindow.cs` の設計を踏襲）。

---

## 4. ScriptableObject の命名・分離規約

* **ファイル名とクラス名の一致**:
  * ScriptableObject クラス（`KartData.cs`, `CourseDecorData.cs` 等）は **必ずクラス名と同名の単独ファイルに配置すること**。
  * 別ファイル内に同居させると `.asset` の `m_Script` が `{fileID: 0}` になり、「referenced script is missing」警告が出て `Resources.LoadAll` が失敗する。

---

## 5. パフォーマンスと UX

* **重い処理のプログレスバー**:
  * 0.5 秒以上かかる一括処理やアセット生成には `EditorUtility.DisplayProgressBar` / `EditorUtility.ClearProgressBar` を使用し、`try ... finally` で確実にプログレスバーを閉じること。
* **GUI レイアウトの安定性**:
  * `Event.current.type == EventType.Layout` と `EventType.Repaint` の間で GUI コントロールの数や順序を変えないこと（`ArgumentException: Getting control N's position in a group with only M controls` 例外を防ぐ）。
