using UnityEngine;
using UnityEditor;
using System.IO;

public static class InspectKart
{
    [MenuItem("Turbo Circuit/Inspect Karts (Render All)")]
    public static void RenderAllKarts()
    {
        string[] karts = { "kart-oobi", "kart-oodi", "kart-ooli", "kart-oopi", "kart-oozi" };
        var wheelPrefab = Resources.Load<GameObject>("Karts/wheel-racing");

        var camGo = new GameObject("TestCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.25f, 0.3f);
        cam.fieldOfView = 45f;

        var lightGo = new GameObject("Light");
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        int res = 512;
        var rt = new RenderTexture(res, res, 24);
        cam.targetTexture = rt;

        Vector3[] wheelPos = new[]
        {
            new Vector3(-0.76f, 0.32f, 0.60f),  // 前左
            new Vector3(0.76f, 0.32f, 0.60f),   // 前右
            new Vector3(-0.78f, 0.34f, -0.52f), // 後左
            new Vector3(0.78f, 0.34f, -0.52f)  // 後右
        };

        for (int k = 0; k < karts.Length; k++)
        {
            var kartPrefab = Resources.Load<GameObject>("Karts/" + karts[k]);
            if (kartPrefab == null) continue;

            var kartGo = new GameObject("TestKart_" + k);
            var model = new GameObject("Model").transform;
            model.SetParent(kartGo.transform, false);

            var body = Object.Instantiate(kartPrefab, model);
            body.transform.localPosition = new Vector3(0, 0.15f, 0);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * 1.5f;

            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("WheelPivot_" + i).transform;
                pivot.SetParent(model, false);
                pivot.localPosition = wheelPos[i];
                var spin = new GameObject("WheelSpin_" + i).transform;
                spin.SetParent(pivot, false);

                var w = Object.Instantiate(wheelPrefab, spin);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = i % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                w.transform.localScale = Vector3.one * 1.35f;
            }

            // 前方斜め
            cam.transform.position = new Vector3(2.5f, 2.0f, 3.5f);
            cam.transform.LookAt(new Vector3(0, 0.5f, 0));
            cam.Render();
            SaveRT(rt, $"kart_{karts[k]}_front.png");

            // 後方斜め（プレイヤー視点）
            cam.transform.position = new Vector3(0, 2.5f, -4.5f);
            cam.transform.LookAt(new Vector3(0, 0.5f, 1f));
            cam.Render();
            SaveRT(rt, $"kart_{karts[k]}_rear.png");

            Object.DestroyImmediate(kartGo);
        }

        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        rt.Release();

        Debug.Log("[InspectKart] All 5 karts rendered successfully!");
    }

    static void SaveRT(RenderTexture rt, string filename)
    {
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        string path = Path.Combine(Application.dataPath, "../Logs", filename);
        File.WriteAllBytes(path, bytes);
        Debug.Log("Saved preview: " + path);
    }
}
