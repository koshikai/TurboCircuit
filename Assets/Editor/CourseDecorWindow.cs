using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// コース装飾の手動調整ウィンドウ。
// Open で選択コースの装飾を Scene 上に展開（保存されないプレビュー）→ 動かす/消す/複製 → Save で
// Assets/Resources/Data/CourseDecor/Course_XX.asset に差分を書き出す。実行時は生成直後にそれを適用する。
public class CourseDecorWindow : EditorWindow
{
    const string DataDir = "Assets/Resources/Data/CourseDecor";
    const float PosEps = 0.001f;

    int courseIndex;
    Track preview;

    [MenuItem("Turbo Circuit/Course Decor Editor")]
    static void Open() => GetWindow<CourseDecorWindow>("Course Decor");

    const string PreviewPrefix = "[Course Decor Preview]";

    // 再コンパイルやエディタ再起動で壊れたプレビュー（マテリアル/スクリプト参照切れ）の残骸を掃除する
    [InitializeOnLoadMethod]
    static void RegisterCleanup()
    {
        EditorApplication.delayCall += DestroyAllPreviews;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode) DestroyAllPreviews();
        };
        // シーンの切り替え/新規作成（Setup Scene・Build Windows 含む）でも、DontSave のプレビューは生き残るため消す
        UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += (scene, removing) => DestroyAllPreviews();
        UnityEditor.SceneManagement.EditorSceneManager.newSceneCreated += (scene, setup, mode) => DestroyAllPreviews();    }

    [MenuItem("Turbo Circuit/Clean Course Decor Preview")]
    public static void DestroyAllPreviews()
    {
        // プレビューは全オブジェクトに DontSaveInEditor 単独のフラグを付けている。
        // 再コンパイル後に親を失った孤立オブジェクトも Hierarchy には出ないが、このフラグで拾える。
        int n = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go == null || EditorUtility.IsPersistent(go)) continue;
            bool flagged = go.hideFlags == HideFlags.DontSaveInEditor;
            bool named = go.transform.parent == null && go.name.StartsWith(PreviewPrefix);
            if (!flagged && !named) continue;
            if (go != null) { DestroyImmediate(go); n++; }
        }
        if (n > 0) Debug.Log($"[CourseDecor] 古いプレビューのオブジェクトを {n} 個削除しました");
    }

    void OnEnable()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ClearPreview;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    void OnDisable()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= ClearPreview;
        EditorApplication.playModeStateChanged -= OnPlayMode;
    }

    void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode) ClearPreview();
    }

    void OnGUI()
    {
        var courses = Track.Courses;
        var names = new string[courses.Length];
        for (int i = 0; i < names.Length; i++) names[i] = $"{i}: {courses[i].Name}";
        courseIndex = EditorGUILayout.Popup("Course", Mathf.Clamp(courseIndex, 0, names.Length - 1), names);

        var asset = AssetDatabase.LoadAssetAtPath<CourseDecorData>(AssetPath(courseIndex));
        EditorGUILayout.HelpBox(asset == null
            ? "調整データなし（自動配置のまま）"
            : $"調整データあり: 移動/削除 {asset.entries.Count} 件、複製 {asset.clones.Count} 件", MessageType.None);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(preview == null ? "Open（装飾を Scene に展開）" : "Reload（保存済みの状態から再展開）"))
                OpenPreview();
            using (new EditorGUI.DisabledScope(preview == null))
            {
                if (GUILayout.Button("Save（差分を保存）")) Save();
                if (GUILayout.Button("Close（保存せず閉じる）")) ClearPreview();
            }
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("Reset（調整データを削除し自動配置に戻す）") &&
                    EditorUtility.DisplayDialog("Reset", $"{names[courseIndex]} の調整データを削除します。", "削除", "キャンセル"))
                {
                    AssetDatabase.DeleteAsset(AssetPath(courseIndex));
                    ClearPreview();
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "使い方\n" +
            "1. Open → Hierarchy の [Course Decor Preview] 以下に装飾物が並びます（Scene には保存されません）\n" +
            "2. 移動/回転/拡縮、非アクティブ化または Delete で削除、Ctrl+D で複製\n" +
            "3. Save で保存。プレイ時に自動で反映されます\n" +
            "※ スクリプト再コンパイルやプレイ開始でプレビューは閉じます（未保存の編集は失われます）\n" +
            "※ 路面・壁・地形・トンネル等は対象外（形状は CourseData の制御点で調整）",
            MessageType.Info);
    }

    static string AssetPath(int idx) => $"{DataDir}/Course_{idx:D2}.asset";

    void OpenPreview()
    {
        ClearPreview();
        DestroyAllPreviews();
        var rm = Object.FindAnyObjectByType<RaceManager>();
        if (rm == null)
        {
            EditorUtility.DisplayDialog("Course Decor", "Race.unity を開いてください（RaceManager が見つかりません）。", "OK");
            return;
        }

        var go = new GameObject($"[Course Decor Preview] Course_{courseIndex:D2}");
        preview = go.AddComponent<Track>();
        preview.BakeMode = true;
        preview.BuildPath(courseIndex);
        preview.BuildVisuals(rm, courseIndex);

        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.Frame(new Bounds(preview.Pts[0], Vector3.one * 60f), false);
    }

    void ClearPreview()
    {
        if (preview != null)
        {
            preview.ClearVisuals();
            DestroyImmediate(preview.gameObject);
        }
        preview = null;
    }

    void Save()
    {
        if (preview == null) return;
        var data = ScriptableObject.CreateInstance<CourseDecorData>();
        data.courseName = Track.Courses[courseIndex].Name;

        // 元オブジェクト：移動/削除の差分
        foreach (var kv in preview.DecorSpawned)
        {
            var go = kv.Value;
            string key = kv.Key;
            if (go == null)
            {
                data.entries.Add(new DecorEntry { key = key, deleted = true });
                continue;
            }
            var tag = go.GetComponent<DecorTag>();
            if (tag == null) continue;
            var t = go.transform;
            if (!go.activeSelf)
            {
                data.entries.Add(new DecorEntry { key = key, deleted = true });
            }
            else if ((t.position - tag.origPosition).sqrMagnitude > PosEps * PosEps ||
                     Quaternion.Angle(t.rotation, tag.origRotation) > 0.01f ||
                     (t.localScale - tag.origScale).sqrMagnitude > PosEps * PosEps)
            {
                data.entries.Add(new DecorEntry { key = key, position = t.position, rotation = t.rotation, scale = t.localScale });
            }
        }

        // 複製：同じタグを持つが元インスタンスではないもの
        foreach (var tag in preview.GetComponentsInChildren<DecorTag>(true))
        {
            if (!preview.DecorSpawned.TryGetValue(tag.key, out var orig) || orig == tag.gameObject) continue;
            if (!tag.gameObject.activeSelf) continue;
            var t = tag.transform;
            data.clones.Add(new DecorClone { sourceKey = tag.key, position = t.position, rotation = t.rotation, scale = t.localScale });
        }

        string path = AssetPath(courseIndex);
        if (data.entries.Count == 0 && data.clones.Count == 0)
        {
            DestroyImmediate(data);
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[CourseDecor] 差分なし。調整データを削除しました: {path}");
            return;
        }

        Directory.CreateDirectory(DataDir);
        var existing = AssetDatabase.LoadAssetAtPath<CourseDecorData>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(data, path);
        }
        else
        {
            existing.courseName = data.courseName;
            existing.entries = data.entries;
            existing.clones = data.clones;
            EditorUtility.SetDirty(existing);
            DestroyImmediate(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CourseDecor] 保存しました: {path}（移動/削除 {data.entries.Count} 件、複製 {data.clones.Count} 件）");
    }
}
