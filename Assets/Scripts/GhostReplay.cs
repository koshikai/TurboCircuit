using System;
using System.Collections.Generic;
using UnityEngine;

// ゴースト走行データの1フレーム（時刻・座標・向き）
[Serializable]
public struct GhostFrame
{
    public float t;
    public Vector3 pos;
    public float heading;
}

// コース完走時のゴースト走行データ全体
[Serializable]
public class GhostData
{
    public float finishTime;
    public List<GhostFrame> frames = new List<GhostFrame>();
}

// タイムアタック用のゴースト記録・再生システム
public class GhostReplay : MonoBehaviour
{
    GhostData playbackData;
    GameObject ghostObj;
    Transform ghostTransform;
    int currentIndex;

    public bool HasGhost => playbackData != null && playbackData.frames.Count > 1;

    public static GhostData LoadGhost(int course)
    {
        string key = $"tc_ghost_{course}";
        if (!PlayerPrefs.HasKey(key)) return null;
        try
        {
            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<GhostData>(json);
        }
        catch { return null; }
    }

    public static void SaveGhost(int course, GhostData data)
    {
        if (data == null || data.frames.Count == 0) return;
        string key = $"tc_ghost_{course}";
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    // タイムアタック開始時にゴーストカーを生成して準備
    public void InitPlayback(int course, Material ghostMat, Track track)
    {
        ClearPlayback();
        playbackData = LoadGhost(course);
        if (!HasGhost) return;

        ghostObj = new GameObject("GhostKart");
        ghostTransform = ghostObj.transform;

        // 半透明のシンプルなボディ
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(ghostTransform, false);
        body.transform.localPosition = new Vector3(0, 0.38f, 0);
        body.transform.localScale = new Vector3(1.3f, 0.3f, 2.1f);

        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(nose.GetComponent<Collider>());
        nose.transform.SetParent(ghostTransform, false);
        nose.transform.localPosition = new Vector3(0, 0.36f, 1.2f);
        nose.transform.localScale = new Vector3(0.9f, 0.25f, 0.5f);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<Collider>());
        head.transform.SetParent(ghostTransform, false);
        head.transform.localPosition = new Vector3(0, 1.1f, -0.2f);
        head.transform.localScale = Vector3.one * 0.55f;

        // 半透明シアンマテリアル
        var mat = new Material(ghostMat)
        {
            color = new Color(0.3f, 0.85f, 1f, 0.42f)
        };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.15f, 0.5f, 0.8f) * 0.6f);

        body.GetComponent<Renderer>().sharedMaterial = mat;
        nose.GetComponent<Renderer>().sharedMaterial = mat;
        head.GetComponent<Renderer>().sharedMaterial = mat;

        currentIndex = 0;
        var f0 = playbackData.frames[0];
        ghostTransform.position = f0.pos;
        ghostTransform.rotation = Quaternion.Euler(0, f0.heading, 0);
    }

    // 毎フレーム時刻に合わせてゴースト位置を滑らかに補間移動
    public void TickPlayback(float raceTime)
    {
        if (!HasGhost || ghostTransform == null) return;

        var frames = playbackData.frames;
        int count = frames.Count;

        // 過去のインデックスから進める
        while (currentIndex < count - 2 && frames[currentIndex + 1].t < raceTime)
        {
            currentIndex++;
        }

        var fA = frames[currentIndex];
        var fB = frames[Mathf.Min(currentIndex + 1, count - 1)];

        float span = fB.t - fA.t;
        float factor = span > 0.0001f ? Mathf.Clamp01((raceTime - fA.t) / span) : 0f;

        ghostTransform.position = Vector3.Lerp(fA.pos, fB.pos, factor);
        ghostTransform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(fA.heading, fB.heading, factor), 0);

        if (raceTime > playbackData.finishTime + 3f)
        {
            ghostObj.SetActive(false);
        }
    }

    public void ClearPlayback()
    {
        if (ghostObj != null)
        {
            Destroy(ghostObj);
            ghostObj = null;
        }
        playbackData = null;
        currentIndex = 0;
    }

    void OnDestroy()
    {
        ClearPlayback();
    }
}
