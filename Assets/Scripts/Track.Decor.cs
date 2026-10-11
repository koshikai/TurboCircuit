using System.Collections.Generic;
using UnityEngine;

// 装飾物の手動調整（CourseDecorData）の適用。
// BuildScenery / BuildSponsorBoards / BuildCircuitProps が Track 直下に作ったオブジェクトを対象に、
// 生成完了後にまとめてキー付け → 上書き適用する（生成コード側の後処理で姿勢が戻されないようにするため）。
public partial class Track
{
    // エディタのプレビュー用：true の間は DecorTag を付け、削除は非アクティブ化に留める
    public bool BakeMode;
    public readonly Dictionary<string, GameObject> DecorSpawned = new Dictionary<string, GameObject>();

    // Track 内の Destroy をエディタ編集モードでも安全にする（Object.Destroy は編集モードでは例外）
    static new void Destroy(Object obj) => SafeDestroy(obj);

    static string DecorKey(GameObject go)
    {
        string n = go.name;
        int p = n.IndexOf(" (");
        if (p > 0) n = n.Substring(0, p);
        if (n.EndsWith("(Clone)")) n = n.Substring(0, n.Length - 7);
        var v = go.transform.position;
        return $"{n}@{Mathf.RoundToInt(v.x * 10f)},{Mathf.RoundToInt(v.y * 10f)},{Mathf.RoundToInt(v.z * 10f)}";
    }

    // startChild 以降に Track 直下へ追加された装飾物へ、保存済みの調整を適用する
    void ApplyDecorOverrides(int startChild)
    {
        DecorSpawned.Clear();
        var data = Resources.Load<CourseDecorData>(CourseDecorData.ResourcePath(ActiveCourseIndex));
        if (!BakeMode && data == null) return;

        var overrides = new Dictionary<string, DecorEntry>();
        if (data != null)
            foreach (var e in data.entries) overrides[e.key] = e;

        var cands = new List<GameObject>();
        for (int i = startChild; i < transform.childCount; i++)
        {
            var go = transform.GetChild(i).gameObject;
            // アニメーション等のスクリプト付きは位置を握っているため対象外
            if (go.GetComponentInChildren<MonoBehaviour>(true) != null) continue;
            cands.Add(go);
        }

        var keyCount = new Dictionary<string, int>();
        int applied = 0;
        foreach (var go in cands)
        {
            string baseKey = DecorKey(go);
            keyCount.TryGetValue(baseKey, out int n);
            keyCount[baseKey] = n + 1;
            string key = n == 0 ? baseKey : baseKey + "#" + n;
            DecorSpawned[key] = go;

            var t = go.transform;
            if (BakeMode)
            {
                var tag = go.AddComponent<DecorTag>();
                tag.key = key;
                tag.origPosition = t.position;
                tag.origRotation = t.rotation;
                tag.origScale = t.localScale;
            }

            if (!overrides.TryGetValue(key, out var ov)) continue;
            applied++;
            if (ov.deleted)
            {
                go.SetActive(false);
                if (!BakeMode) Destroy(go);
                continue;
            }
            t.SetPositionAndRotation(ov.position, ov.rotation);
            t.localScale = ov.scale;
        }

        if (data != null)
        {
            foreach (var c in data.clones)
            {
                if (!DecorSpawned.TryGetValue(c.sourceKey, out var src) || src == null) continue;
                var copy = Instantiate(src, transform);
                copy.SetActive(true);
                copy.transform.SetPositionAndRotation(c.position, c.rotation);
                copy.transform.localScale = c.scale;
                applied++;
            }
            int expected = data.entries.Count + data.clones.Count;
            if (applied < expected)
                Debug.LogWarning($"[CourseDecor] {data.name}: {expected - applied} 件の調整が現在の生成結果と一致しません（生成コードの変更後は Course Decor Editor で再確認してください）");
        }
    }
}
