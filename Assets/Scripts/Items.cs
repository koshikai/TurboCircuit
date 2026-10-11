using UnityEngine;

// アイテムボックス：触れるとランダムなアイテムがもらえる。数秒で復活。
public class ItemBox : MonoBehaviour
{
    public bool Active => respawn <= 0;

    float respawn, phase;
    Vector3 basePos;
    Renderer rend;
    MaterialPropertyBlock mpb;
    Transform label;
    Transform cube;

    Renderer[] faceRends;
    Transform[] questionLabels;

    public void Init(Vector3 pos, Material mat, Font font)
    {
        basePos = pos + Vector3.up * 1.1f;
        transform.position = basePos;
        phase = Random.value * 10f;

        var root = new GameObject("BoxRoot").transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * 1.15f;
        cube = root;

        mpb = new MaterialPropertyBlock();

        // 1. 内部コアブロック（隙間から見える引き締まったダークネイビー芯）
        var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(root, false);
        core.transform.localScale = Vector3.one * 0.84f;
        var coreRend = core.GetComponent<Renderer>();
        coreRend.sharedMaterial = mat;
        var coreMpb = new MaterialPropertyBlock();
        Color darkCore = new Color(0.10f, 0.14f, 0.26f, 1f);
        coreMpb.SetColor("_Color", darkCore);
        coreMpb.SetColor("_BaseColor", darkCore);
        coreMpb.SetColor("_EmissionColor", Color.black);
        coreRend.SetPropertyBlock(coreMpb);

        // 2. 6面のレインボーキャンディパネル（各面が異なる虹色に輝き、どの角度からもカラフルに見える）
        Vector3[] faceNormals =
        {
            Vector3.forward, Vector3.back,
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down
        };
        faceRends = new Renderer[6];
        for (int i = 0; i < 6; i++)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(panel.GetComponent<Collider>());
            panel.name = "RainbowFace_" + i;
            panel.transform.SetParent(root, false);
            panel.transform.localPosition = faceNormals[i] * 0.425f;
            panel.transform.localRotation = Quaternion.LookRotation(faceNormals[i]);
            panel.transform.localScale = new Vector3(0.76f, 0.76f, 0.04f);
            faceRends[i] = panel.GetComponent<Renderer>();
            faceRends[i].sharedMaterial = mat;
        }

        // 3. 12本のゴールドエッジフレーム＆8箇所のコーナーバンパー球（突起飛び出しゼロのクリーンなトイフレーム）
        var frameMpb = new MaterialPropertyBlock();
        Color frameGold = new Color(1.00f, 0.82f, 0.14f, 1f);
        frameMpb.SetColor("_Color", frameGold);
        frameMpb.SetColor("_BaseColor", frameGold);
        frameMpb.SetColor("_EmissionColor", new Color(0.12f, 0.08f, 0.01f));

        float edgeOffset = 0.43f;
        float barRadius = 0.095f;
        float barHalfLen = 0.43f;

        // X軸方向の4辺、Y軸方向の4辺、Z軸方向の4辺
        foreach (float s1 in new[] { -1f, 1f })
        foreach (float s2 in new[] { -1f, 1f })
        {
            // Y軸平行エッジ
            var barY = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barY.GetComponent<Collider>());
            barY.transform.SetParent(root, false);
            barY.transform.localPosition = new Vector3(s1 * edgeOffset, 0f, s2 * edgeOffset);
            barY.transform.localScale = new Vector3(barRadius, barHalfLen, barRadius);
            var ry = barY.GetComponent<Renderer>();
            ry.sharedMaterial = mat;
            ry.SetPropertyBlock(frameMpb);

            // X軸平行エッジ
            var barX = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barX.GetComponent<Collider>());
            barX.transform.SetParent(root, false);
            barX.transform.localPosition = new Vector3(0f, s1 * edgeOffset, s2 * edgeOffset);
            barX.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            barX.transform.localScale = new Vector3(barRadius, barHalfLen, barRadius);
            var rx = barX.GetComponent<Renderer>();
            rx.sharedMaterial = mat;
            rx.SetPropertyBlock(frameMpb);

            // Z軸平行エッジ
            var barZ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(barZ.GetComponent<Collider>());
            barZ.transform.SetParent(root, false);
            barZ.transform.localPosition = new Vector3(s1 * edgeOffset, s2 * edgeOffset, 0f);
            barZ.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barZ.transform.localScale = new Vector3(barRadius, barHalfLen, barRadius);
            var rz = barZ.GetComponent<Renderer>();
            rz.sharedMaterial = mat;
            rz.SetPropertyBlock(frameMpb);
        }

        // 8箇所の角にポップなゴールドコーナーキャップ
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sy in new[] { -1f, 1f })
        foreach (float sz in new[] { -1f, 1f })
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(root, false);
            cap.transform.localPosition = new Vector3(sx * edgeOffset, sy * edgeOffset, sz * edgeOffset);
            cap.transform.localScale = Vector3.one * 0.175f;
            var rr = cap.GetComponent<Renderer>();
            rr.sharedMaterial = mat;
            rr.SetPropertyBlock(frameMpb);
        }

        // 4. 4つの側面すべてに3Dメッシュの立体「?」エンブレム（白＋濃紺ドロップシャドウ）を配置
        // （TextMesh の GUI/Text シェーダーは ZTest Always で裏面が透けて見えるため、Litマテリアルの3Dメッシュで描画）
        var labelsRoot = new GameObject("QuestionLabels").transform;
        labelsRoot.SetParent(root, false);
        label = labelsRoot;

        Vector2[] qHookPts =
        {
            new Vector2( 0.105f, 0.095f),
            new Vector2( 0.095f, 0.205f),
            new Vector2(-0.005f, 0.245f),
            new Vector2(-0.110f, 0.190f),
            new Vector2(-0.110f, 0.085f),
            new Vector2(-0.015f, 0.005f),
            new Vector2( 0.000f, -0.085f)
        };
        Mesh qHookMesh = Banana.BuildLoftedTube(
            20, 12,
            t =>
            {
                Vector2 p = Banana.EvalSplinePublic(qHookPts, t);
                return new Vector3(p.x, p.y, 0f);
            },
            (t, ang) =>
            {
                float endTaper = Mathf.Min(Mathf.Clamp01(t / 0.10f), Mathf.Clamp01((1f - t) / 0.10f));
                float r = 0.046f * Mathf.Lerp(0.55f, 1.0f, endTaper);
                // Z方向（厚み）は少しフラットにしてパネルに密着させる
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                float rx = r;
                float rz = r * 0.45f;
                return 1f / Mathf.Sqrt((c * c) / (rx * rx) + (s * s) / (rz * rz));
            },
            true, true
        );

        var whiteMpb = new MaterialPropertyBlock();
        whiteMpb.SetColor("_Color", Color.white);
        whiteMpb.SetColor("_BaseColor", Color.white);
        whiteMpb.SetColor("_EmissionColor", new Color(0.22f, 0.22f, 0.22f));

        var shadowMpb = new MaterialPropertyBlock();
        Color navyShadow = new Color(0.06f, 0.09f, 0.20f, 1f);
        shadowMpb.SetColor("_Color", navyShadow);
        shadowMpb.SetColor("_BaseColor", navyShadow);
        shadowMpb.SetColor("_EmissionColor", Color.black);

        float[] faceYaws = { 0f, 90f, 180f, 270f };
        for (int i = 0; i < 4; i++)
        {
            var faceAnchor = new GameObject("QFace_" + i).transform;
            faceAnchor.SetParent(labelsRoot, false);
            faceAnchor.localRotation = Quaternion.Euler(0f, faceYaws[i], 0f);
            faceAnchor.localPosition = faceAnchor.localRotation * new Vector3(0f, 0f, 0.448f);

            // ドロップシャドウ「?」上部フック
            var shHook = new GameObject("QShadowHook");
            shHook.transform.SetParent(faceAnchor, false);
            shHook.transform.localPosition = new Vector3(-0.018f, -0.022f, -0.004f);
            shHook.transform.localScale = new Vector3(1.08f, 1.08f, 0.7f);
            shHook.AddComponent<MeshFilter>().sharedMesh = qHookMesh;
            var shHookR = shHook.AddComponent<MeshRenderer>();
            shHookR.sharedMaterial = mat;
            shHookR.SetPropertyBlock(shadowMpb);

            // ドロップシャドウ「?」下部ドット
            var shDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(shDot.GetComponent<Collider>());
            shDot.transform.SetParent(faceAnchor, false);
            shDot.transform.localPosition = new Vector3(-0.018f, -0.228f, -0.004f);
            shDot.transform.localScale = new Vector3(0.112f, 0.112f, 0.040f);
            var shDotR = shDot.GetComponent<Renderer>();
            shDotR.sharedMaterial = mat;
            shDotR.SetPropertyBlock(shadowMpb);

            // メイン白「?」上部フック
            var mHook = new GameObject("QMainHook");
            mHook.transform.SetParent(faceAnchor, false);
            mHook.transform.localPosition = new Vector3(0f, 0f, 0.008f);
            mHook.AddComponent<MeshFilter>().sharedMesh = qHookMesh;
            var mHookR = mHook.AddComponent<MeshRenderer>();
            mHookR.sharedMaterial = mat;
            mHookR.SetPropertyBlock(whiteMpb);

            // メイン白「?」下部ドット
            var mDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(mDot.GetComponent<Collider>());
            mDot.transform.SetParent(faceAnchor, false);
            mDot.transform.localPosition = new Vector3(0f, -0.208f, 0.008f);
            mDot.transform.localScale = new Vector3(0.102f, 0.102f, 0.048f);
            var mDotR = mDot.GetComponent<Renderer>();
            mDotR.sharedMaterial = mat;
            mDotR.SetPropertyBlock(whiteMpb);
        }

        ApplyBoxColor(0f);
    }

    void ApplyBoxColor(float timeVal)
    {
        if (faceRends == null) return;
        float[] hueOffsets = { 0.00f, 0.50f, 0.16f, 0.68f, 0.33f, 0.84f };
        for (int i = 0; i < faceRends.Length; i++)
        {
            if (faceRends[i] == null) continue;
            float h = Mathf.Repeat(timeVal * 0.32f + phase * 0.07f + hueOffsets[i], 1f);
            Color vivid = Color.HSVToRGB(h, 0.82f, 0.98f);
            mpb.SetColor("_Color", vivid);
            mpb.SetColor("_BaseColor", vivid);
            mpb.SetColor("_EmissionColor", vivid * 0.14f);
            faceRends[i].SetPropertyBlock(mpb);
        }
    }

    public void Break()
    {
        respawn = 2.5f;
        Fx.Burst(transform.position, Color.HSVToRGB(Random.value, 0.6f, 1f), 18, 6f, 0.5f);
    }

    void Update()
    {
        respawn -= Time.deltaTime;
        float grow = Mathf.Clamp01(-respawn / 0.4f);
        cube.gameObject.SetActive(respawn <= 0);
        if (respawn > 0) return;

        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * 0.2f;
        cube.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.8f + phase) * 14f, Time.time * 75f + phase * 20f, Mathf.Cos(Time.time * 1.5f + phase) * 12f);
        cube.localScale = Vector3.one * 1.15f * grow;

        ApplyBoxColor(Time.time);
    }
}

// バナナ：踏んだカートはスピンする（2Dアイコン item_banana_pop と完全統一されたプロシージャルむき身バナナ造形）
public class Banana : MonoBehaviour
{
    public Kart Owner;
    public int NetId;
    float age;
    public bool Armed => age > 0.4f;

    static GameObject CreateMeshPart(Transform parent, string name, Mesh mesh, Material mat, Color col, Quaternion? rot = null, Color? emission = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", col);
        mpb.SetColor("_BaseColor", col);
        // Banana マテリアル既定の強い黄色エミッションで白飛びしないよう明示的に上書きする
        mpb.SetColor("_EmissionColor", emission ?? Color.black);
        mr.SetPropertyBlock(mpb);
        return go;
    }

    static Vector2 EvalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    public static Vector2 EvalSplinePublic(Vector2[] pts, float t01) => EvalSpline(pts, t01);

    static Vector2 EvalSpline(Vector2[] pts, float t01)
    {
        int segs = pts.Length - 1;
        float f = Mathf.Clamp01(t01) * segs;
        int i = Mathf.Min(Mathf.FloorToInt(f), segs - 1);
        float lt = f - i;
        Vector2 p0 = pts[Mathf.Max(0, i - 1)];
        Vector2 p1 = pts[i];
        Vector2 p2 = pts[i + 1];
        Vector2 p3 = pts[Mathf.Min(segs, i + 2)];
        return EvalCatmullRom(p0, p1, p2, p3, lt);
    }

    // 中心軸カーブに沿った滑らかな回転体メッシュを生成
    public static Mesh BuildLoftedTube(int rings, int sides, System.Func<float, Vector3> centerFunc, System.Func<float, float, float> radiusFunc, bool capBottom, bool capTop)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float dt = 0.01f;
            Vector3 c = centerFunc(t);
            Vector3 cNext = centerFunc(Mathf.Min(1f, t + dt));
            Vector3 cPrev = centerFunc(Mathf.Max(0f, t - dt));
            Vector3 tangent = (cNext - cPrev).normalized;
            if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.up;

            // Z軸と平行な場合でも安定した直交基底を作る
            Vector3 refUp = Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) > 0.92f ? Vector3.up : Vector3.forward;
            Vector3 right = Vector3.Cross(tangent, refUp).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 forward = Vector3.Cross(right, tangent).normalized;

            for (int s = 0; s < sides; s++)
            {
                float ang = (s / (float)sides) * Mathf.PI * 2f;
                float rad = radiusFunc(t, ang);
                Vector3 offset = (right * Mathf.Cos(ang) + forward * Mathf.Sin(ang)) * rad;
                verts.Add(c + offset);
            }
        }

        for (int r = 0; r < rings; r++)
        {
            int row0 = r * sides;
            int row1 = (r + 1) * sides;
            for (int s = 0; s < sides; s++)
            {
                int nextS = (s + 1) % sides;
                tris.Add(row0 + s);
                tris.Add(row1 + s);
                tris.Add(row1 + nextS);

                tris.Add(row0 + s);
                tris.Add(row1 + nextS);
                tris.Add(row0 + nextS);
            }
        }

        if (capBottom)
        {
            int centerIdx = verts.Count;
            verts.Add(centerFunc(0f));
            for (int s = 0; s < sides; s++)
            {
                int nextS = (s + 1) % sides;
                tris.Add(centerIdx);
                tris.Add(nextS);
                tris.Add(s);
            }
        }

        if (capTop)
        {
            int centerIdx = verts.Count;
            int topRow = rings * sides;
            verts.Add(centerFunc(1f));
            for (int s = 0; s < sides; s++)
            {
                int nextS = (s + 1) % sides;
                tris.Add(centerIdx);
                tris.Add(topRow + s);
                tris.Add(topRow + nextS);
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 1枚のふっくらとした黄金色のバナナの皮メッシュを生成（上面も側面も鮮やかな黄色で、地面に安定して広がる形状）
    static Mesh BuildGoldenPeelMesh(Vector2[] spinePts, int steps, int crossSegs, float maxHalfWidth, float maxThick)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float dt = 0.015f;
            Vector2 p = EvalSpline(spinePts, t);
            Vector2 pNext = EvalSpline(spinePts, Mathf.Min(1f, t + dt));
            Vector2 pPrev = EvalSpline(spinePts, Mathf.Max(0f, t - dt));
            Vector2 tan2D = (pNext - pPrev).normalized;
            Vector2 norm2D = new Vector2(-tan2D.y, tan2D.x);

            // 根元から中腹にかけてふっくら広く、先端で丸くまとまるシルエット
            float widthCurve = Mathf.Sin(Mathf.Pow(t, 0.62f) * Mathf.PI * 0.92f);
            float rootBlend = Mathf.Lerp(0.68f, 1.0f, Mathf.Clamp01(t / 0.25f));
            float halfWidth = maxHalfWidth * Mathf.Max(0.12f, widthCurve * rootBlend);
            float thick = maxThick * Mathf.Lerp(1.05f, 0.42f, t * t);

            for (int c = 0; c < crossSegs; c++)
            {
                float ang = (c / (float)crossSegs) * Mathf.PI * 2f;
                float lx = Mathf.Cos(ang) * halfWidth;
                float ln = Mathf.Sin(ang) * thick;
                // 皮の中央がふっくら盛り上がるドーム状の断面（アイコンの立体感に合わせる）
                float dome = (1f - Mathf.Cos(ang) * Mathf.Cos(ang)) * 0.018f * (1f - t * 0.5f);
                ln += dome;

                float vz = p.x + norm2D.x * ln;
                float vy = Mathf.Max(0.008f, p.y + norm2D.y * ln);
                verts.Add(new Vector3(lx, vy, vz));
            }
        }

        for (int i = 0; i < steps; i++)
        {
            int r0 = i * crossSegs;
            int r1 = (i + 1) * crossSegs;
            for (int c = 0; c < crossSegs; c++)
            {
                int nc = (c + 1) % crossSegs;
                tris.Add(r0 + c);
                tris.Add(r0 + nc);
                tris.Add(r1 + nc);

                tris.Add(r0 + c);
                tris.Add(r1 + nc);
                tris.Add(r1 + c);
            }
        }

        int tipCenterIdx = verts.Count;
        Vector2 tipP = EvalSpline(spinePts, 1f);
        verts.Add(new Vector3(0f, Mathf.Max(0.015f, tipP.y), tipP.x));
        int lastRow = steps * crossSegs;
        for (int c = 0; c < crossSegs; c++)
        {
            int nc = (c + 1) % crossSegs;
            tris.Add(tipCenterIdx);
            tris.Add(lastRow + nc);
            tris.Add(lastRow + c);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public void Init(Vector3 pos, Kart owner, Material mat)
    {
        Owner = owner;
        transform.position = pos;

        // 2Dアイコン (item_banana_pop.png) に合わせた鮮やかでメリハリのある4色パレット
        Color peelGold   = new Color(1.00f, 0.76f, 0.04f); // 外皮の鮮やかなゴールデンイエロー
        Color cuffCream  = new Color(1.00f, 0.89f, 0.36f); // 腰の折り返しカフスのクリームイエロー
        Color fruitCream = new Color(0.99f, 0.93f, 0.54f); // むき身の実の優しいアイボリークリーム
        Color stemBrown  = new Color(0.42f, 0.22f, 0.06f); // ヘタ・双葉のチョコレートブラウン

        Color emitGold  = new Color(0.06f, 0.03f, 0.00f);
        Color emitCream = new Color(0.05f, 0.04f, 0.01f);

        // 1. 中央のむき身バナナ本体（ふっくらとした弾頭ドーム型で、少しだけ優雅に反る）
        Mesh coreMesh = BuildLoftedTube(
            20, 18,
            t =>
            {
                float y = Mathf.Lerp(0.10f, 0.66f, t);
                float curveX = Mathf.Sin(t * Mathf.PI * 0.65f) * 0.038f;
                return new Vector3(curveX, y, 0f);
            },
            (t, ang) =>
            {
                float profile = t < 0.55f
                    ? Mathf.Lerp(0.150f, 0.168f, Mathf.Sin((t / 0.55f) * Mathf.PI * 0.5f))
                    : Mathf.Lerp(0.168f, 0.048f, Mathf.Pow((t - 0.55f) / 0.45f, 1.45f));
                float ridge = 1f + 0.022f * Mathf.Cos(ang * 5f);
                return profile * ridge;
            },
            true, true
        );
        CreateMeshPart(transform, "BananaFruitCore", coreMesh, mat, fruitCream, null, emitCream);

        // 2. 下部の黄金色ベースカップ（地面 Y=0.01m から腰 Y=0.30m までしっかり接地し、実を包み込む）
        Mesh cupMesh = BuildLoftedTube(
            12, 18,
            t => new Vector3(0f, Mathf.Lerp(0.01f, 0.30f, t), 0f),
            (t, ang) =>
            {
                float baseCurve = Mathf.Lerp(0.125f, 0.192f, Mathf.Pow(t, 0.55f));
                float lobe = 1f + 0.05f * Mathf.Cos((ang - Mathf.PI * 0.25f) * 4f);
                return baseCurve * lobe;
            },
            true, true
        );
        CreateMeshPart(transform, "BananaBaseCup", cupMesh, mat, peelGold, null, emitGold);

        // 3. 上部の茶色いヘタ＋アイコンと同じ可愛い横の新芽（双葉アクセント）
        Mesh stemMesh = BuildLoftedTube(
            9, 12,
            t =>
            {
                float y = Mathf.Lerp(0.64f, 0.79f, t);
                float x = Mathf.Lerp(0.038f, 0.088f, t * t);
                return new Vector3(x, y, 0f);
            },
            (t, ang) =>
            {
                float r = t < 0.85f
                    ? Mathf.Lerp(0.045f, 0.066f, (t / 0.85f) * (t / 0.85f))
                    : Mathf.Lerp(0.066f, 0.025f, (t - 0.85f) / 0.15f);
                return r * (1f + 0.04f * Mathf.Cos(ang * 5f));
            },
            false, true
        );
        CreateMeshPart(transform, "BananaStem", stemMesh, mat, stemBrown);

        // ヘタ横の小さな茶色い芽（item_banana_pop.png の右上にある特徴的な小葉）
        Mesh leafMesh = BuildLoftedTube(
            7, 10,
            t => new Vector3(Mathf.Lerp(0.055f, 0.145f, t), Mathf.Lerp(0.645f, 0.705f + Mathf.Sin(t * Mathf.PI) * 0.02f, t), 0f),
            (t, ang) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.032f * (1f + 0.25f * Mathf.Cos(ang * 2f)),
            true, true
        );
        CreateMeshPart(transform, "BananaStemLeaf", leafMesh, mat, stemBrown);

        // 4. 4枚の黄金色バナナの皮（45°, 135°, 225°, 315° の対角X配置で、正面・側面から見ても美しいシルエット）
        Vector2[] peelSpine =
        {
            new Vector2(0.135f, 0.26f),
            new Vector2(0.205f, 0.21f),
            new Vector2(0.295f, 0.105f),
            new Vector2(0.395f, 0.045f),
            new Vector2(0.490f, 0.042f),
            new Vector2(0.555f, 0.062f)
        };
        Mesh goldenPeelMesh = BuildGoldenPeelMesh(peelSpine, 22, 14, 0.162f, 0.044f);

        // 腰の折り返し部分にあるぷっくり丸まったクリーム色のカフス（尖りのない滑らかなロール襟）
        Mesh cuffMesh = BuildLoftedTube(
            12, 12,
            t =>
            {
                float ang = Mathf.Lerp(-0.92f, 0.92f, t);
                float r = 0.195f + Mathf.Cos(ang * 1.4f) * 0.028f;
                float y = 0.262f - Mathf.Cos(ang * 1.5f) * 0.022f;
                return new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r);
            },
            (t, ang) =>
            {
                float taper = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                return (0.016f + 0.030f * taper) * (1f + 0.15f * Mathf.Sin(ang));
            },
            true, true
        );

        // 皮先端の小さな茶色い石づき
        Mesh tipMesh = BuildLoftedTube(
            5, 8,
            t => new Vector3(0f, Mathf.Lerp(0.056f, 0.070f, t), Mathf.Lerp(0.540f, 0.580f, t)),
            (t, ang) => Mathf.Lerp(0.034f, 0.012f, t),
            true, true
        );

        for (int i = 0; i < 4; i++)
        {
            Quaternion rot = Quaternion.Euler(0f, 45f + i * 90f, 0f);
            CreateMeshPart(transform, "PeelGold_" + i, goldenPeelMesh, mat, peelGold, rot, emitGold);
            CreateMeshPart(transform, "PeelCuff_" + i, cuffMesh, mat, cuffCream, rot, emitCream);
            CreateMeshPart(transform, "PeelTip_" + i, tipMesh, mat, stemBrown, rot);
        }

        transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.Rotate(0, 40f * Time.deltaTime, 0);
    }
}

// ミサイル：前にいるカートを追いかける（継ぎ目のない流線型ロケット胴体＆スイープ尾翼＆シャークマウス）
public class Missile : MonoBehaviour
{
    public Kart Owner;
    public Kart Target => target;
    Kart target;
    Track track;
    RaceManager rm;
    int index;
    float age, lateral;
    Vector3 dir;
    Transform flameCone;

    static GameObject AddPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Color? col = null, Quaternion? rot = null, Color? emission = null)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        if (col.HasValue)
        {
            mpb.SetColor("_Color", col.Value);
            mpb.SetColor("_BaseColor", col.Value);
            mpb.SetColor("_TintColor", col.Value);
        }
        mpb.SetColor("_EmissionColor", emission ?? Color.black);
        r.SetPropertyBlock(mpb);
        return go;
    }

    static GameObject AddMeshPart(Transform parent, string name, Mesh mesh, Material mat, Color col, Quaternion? rot = null, Color? emission = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", col);
        mpb.SetColor("_BaseColor", col);
        mpb.SetColor("_EmissionColor", emission ?? Color.black);
        mr.SetPropertyBlock(mpb);
        return go;
    }

    // Z座標 (-0.46 〜 +0.46) に対するロケット胴体の正確な半径関数
    static float EvalFuselageRadius(float z)
    {
        float t = Mathf.InverseLerp(-0.46f, 0.46f, z);
        if (t > 0.50f)
        {
            float nt = (t - 0.50f) / 0.50f;
            return 0.265f * Mathf.Sqrt(Mathf.Max(0f, 1f - nt * nt));
        }
        if (t < 0.24f)
        {
            float tt = t / 0.24f;
            return Mathf.Lerp(0.175f, 0.265f, Mathf.Sin(tt * Mathf.PI * 0.5f));
        }
        return 0.265f;
    }

    // Z座標と極角 deg (0=真上, +90=右, -90=左) と表面からのオフセット dr から正確な3D座標と法線回転を返す
    static Vector3 FuselageSurfacePoint(float z, float degFromTop, float dr, out Quaternion surfRot)
    {
        float r = EvalFuselageRadius(z) + dr;
        float rad = degFromTop * Mathf.Deg2Rad;
        float x = Mathf.Sin(rad) * r;
        float y = Mathf.Cos(rad) * r;

        float dz = 0.01f;
        float rNext = EvalFuselageRadius(Mathf.Min(0.455f, z + dz));
        float rPrev = EvalFuselageRadius(Mathf.Max(-0.455f, z - dz));
        float drdz = (rNext - rPrev) / (2f * dz);
        Vector3 normal = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), -drdz).normalized;
        surfRot = Quaternion.LookRotation(normal, Vector3.up);
        return new Vector3(x, y, z);
    }

    // 1枚の滑らかな後退翼（スイープフィン）メッシュを生成（ブロックの突き抜けゼロ）
    static Mesh BuildSweptFinMesh(float innerY, float outerY, float zFrontRoot, float zBackRoot, float zFrontTip, float zBackTip, float thickness)
    {
        return Banana.BuildLoftedTube(
            8, 10,
            t =>
            {
                float y = Mathf.Lerp(innerY, outerY, t);
                float zMid = Mathf.Lerp((zFrontRoot + zBackRoot) * 0.5f, (zFrontTip + zBackTip) * 0.5f, t);
                return new Vector3(0f, y, zMid);
            },
            (t, ang) =>
            {
                float halfChord = Mathf.Lerp((zFrontRoot - zBackRoot) * 0.5f, (zFrontTip - zBackTip) * 0.5f, t);
                float halfThick = thickness * 0.5f * Mathf.Lerp(1.0f, 0.55f, t);
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);
                return 1f / Mathf.Sqrt((c * c) / (halfThick * halfThick) + (s * s) / (halfChord * halfChord));
            },
            true, true
        );
    }

    public void Init(RaceManager rm, Track track, Kart owner, Kart target, Material mat)
    {
        this.rm = rm;
        this.track = track;
        Owner = owner;
        this.target = target;
        index = owner != null ? owner.Index : 0;
        dir = owner != null ? owner.Forward : Vector3.forward;
        lateral = owner != null ? owner.Lateral : 0f;
        transform.position = (owner != null ? owner.transform.position + owner.Forward * 2.2f : Vector3.zero) + Vector3.up * 0.7f;

        Material chromeMat = (rm != null && rm.chromeMaterial != null) ? rm.chromeMaterial : mat;
        Material darkMat   = (rm != null && rm.tireMaterial != null) ? rm.tireMaterial : mat;
        Material glowMat   = (rm != null && rm.glowMaterial != null) ? rm.glowMaterial : mat;

        Color redBody    = new Color(0.94f, 0.14f, 0.18f);
        Color finYellow  = new Color(1.00f, 0.76f, 0.08f);
        Color finOrange  = new Color(0.96f, 0.44f, 0.04f);
        Color darkNavy   = new Color(0.06f, 0.09f, 0.16f);
        Color eyeWhite   = new Color(0.98f, 0.99f, 1.00f);
        Color redEmit    = new Color(0.08f, 0.01f, 0.01f);
        Color finEmit    = new Color(0.06f, 0.03f, 0.00f);

        // 1. 継ぎ目ゼロの流線型ロケット胴体メッシュ (Z = -0.46 〜 +0.46)
        Mesh fuselageMesh = Banana.BuildLoftedTube(
            28, 24,
            t => new Vector3(0f, 0f, Mathf.Lerp(-0.46f, 0.46f, t)),
            (t, ang) => EvalFuselageRadius(Mathf.Lerp(-0.46f, 0.46f, t)),
            true, true
        );
        AddMeshPart(transform, "RocketFuselage", fuselageMesh, mat, redBody, null, redEmit);

        // 2. クローム金属ベルト＆リベット鋲（アイコン中央の銀色リング帯）
        AddPart(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.04f), new Vector3(0.555f, 0.068f, 0.555f), chromeMat, new Color(0.84f, 0.89f, 0.96f), Quaternion.Euler(90f, 0f, 0f));
        AddPart(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.04f), new Vector3(0.542f, 0.086f, 0.542f), darkMat, darkNavy, Quaternion.Euler(90f, 0f, 0f));
        for (int r = 0; r < 8; r++)
        {
            float ang = r * 45f * Mathf.Deg2Rad;
            Vector3 rPos = new Vector3(Mathf.Sin(ang) * 0.278f, Mathf.Cos(ang) * 0.278f, -0.04f);
            AddPart(transform, PrimitiveType.Sphere, rPos, Vector3.one * 0.052f, chromeMat, Color.white);
        }

        // 3. 4枚の滑らかなスイープ尾翼（ブロック角の飛び出しがない一体型後退翼メッシュ）
        Mesh mainFinMesh = BuildSweptFinMesh(0.18f, 0.43f, -0.14f, -0.42f, -0.24f, -0.45f, 0.068f);
        Mesh rimFinMesh  = BuildSweptFinMesh(0.20f, 0.445f, -0.26f, -0.43f, -0.32f, -0.465f, 0.052f);
        for (int i = 0; i < 4; i++)
        {
            Quaternion finRot = Quaternion.Euler(0f, 0f, i * 90f);
            AddMeshPart(transform, "FinMain_" + i, mainFinMesh, mat, finYellow, finRot, finEmit);
            AddMeshPart(transform, "FinRim_" + i, rimFinMesh, mat, finOrange, finRot, finEmit);
        }

        // 4. 後部ジェット噴射ノズル＆発光アフターバーナー炎コーン
        AddPart(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.46f), new Vector3(0.33f, 0.06f, 0.33f), chromeMat, new Color(0.75f, 0.82f, 0.90f), Quaternion.Euler(90f, 0f, 0f));
        AddPart(transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.50f), new Vector3(0.27f, 0.035f, 0.27f), darkMat, darkNavy, Quaternion.Euler(90f, 0f, 0f));

        var flRoot = new GameObject("JetFlame").transform;
        flRoot.SetParent(transform, false);
        flRoot.localPosition = new Vector3(0f, 0f, -0.51f);
        AddPart(flRoot, PrimitiveType.Capsule, new Vector3(0f, 0f, -0.13f), new Vector3(0.21f, 0.20f, 0.21f), glowMat, new Color(1.0f, 0.48f, 0.08f, 0.92f), Quaternion.Euler(90f, 0f, 0f), new Color(1.4f, 0.55f, 0.08f));
        AddPart(flRoot, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.05f), new Vector3(0.14f, 0.14f, 0.16f), glowMat, new Color(1.0f, 0.92f, 0.35f, 0.96f), null, new Color(1.8f, 1.5f, 0.5f));
        flameCone = flRoot;

        // 5. FuselageSurfacePoint によりノーズ曲面 R(z) 上へ数学的に100%密着配置した目玉・眉毛・シャークマウス
        foreach (float sx in new[] { -1f, 1f })
        {
            float eyeZ = 0.305f;
            float eyeDeg = sx * 44f;

            // 黒縁ベース（表面 dr = +0.004m）
            Vector3 pOutline = FuselageSurfacePoint(eyeZ, eyeDeg, 0.004f, out Quaternion rotEye);
            AddPart(transform, PrimitiveType.Sphere, pOutline, new Vector3(0.150f, 0.136f, 0.060f), darkMat, darkNavy, rotEye);

            // 白目（表面 dr = +0.015m）
            Vector3 pWhite = FuselageSurfacePoint(eyeZ + 0.005f, eyeDeg, 0.015f, out _);
            AddPart(transform, PrimitiveType.Sphere, pWhite, new Vector3(0.128f, 0.114f, 0.056f), mat, eyeWhite, rotEye);

            // 黒目（表面 dr = +0.030m、前方・内側を睨む）
            Vector3 pPupil = FuselageSurfacePoint(eyeZ + 0.018f, sx * 39f, 0.030f, out Quaternion rotPupil);
            AddPart(transform, PrimitiveType.Sphere, pPupil, new Vector3(0.066f, 0.066f, 0.042f), darkMat, darkNavy, rotPupil);

            // 瞳の白ハイライト（表面 dr = +0.042m）
            Vector3 pSpec = FuselageSurfacePoint(eyeZ + 0.014f, sx * 34f, 0.042f, out _);
            AddPart(transform, PrimitiveType.Sphere, pSpec, new Vector3(0.025f, 0.025f, 0.028f), mat, Color.white, rotPupil);

            // 怒り眉毛（表面 dr = +0.018m、ノーズ曲面に沿って密着しつつ中央へつり下がるV字）
            Vector3 pBrow = FuselageSurfacePoint(eyeZ - 0.008f, sx * 29f, 0.018f, out Quaternion rotBrow);
            Quaternion browTilt = rotBrow * Quaternion.Euler(0f, 0f, sx * 28f) * Quaternion.Euler(0f, 0f, 90f);
            AddPart(transform, PrimitiveType.Capsule, pBrow, new Vector3(0.035f, 0.080f, 0.035f), darkMat, darkNavy, browTilt);
        }

        // シャークマウス：正面からも側面からもクッキリ見えるノーズ前方下部 (Z = 0.34〜0.39) の曲面 R(z) に密着配置
        for (int s = -6; s <= 6; s++)
        {
            float deg = 180f + s * 7.2f;
            float mz = 0.340f + Mathf.Cos(s * 0.22f) * 0.045f;
            Vector3 pLip = FuselageSurfacePoint(mz, deg, 0.005f, out Quaternion rotLip);
            AddPart(transform, PrimitiveType.Sphere, pLip, new Vector3(0.065f, 0.098f, 0.042f), darkMat, darkNavy, rotLip);

            if (Mathf.Abs(s) <= 5)
            {
                Vector3 pTeeth = FuselageSurfacePoint(mz + 0.005f, deg, 0.015f, out _);
                AddPart(transform, PrimitiveType.Sphere, pTeeth, new Vector3(0.054f, 0.072f, 0.038f), mat, eyeWhite, rotLip);
            }
        }
        // 歯のジグザグ仕切りライン（表面 dr = +0.028m）
        for (int t = -2; t <= 2; t++)
        {
            float deg = 180f + t * 13.5f;
            float mz = 0.345f + Mathf.Cos(t * 0.44f) * 0.045f;
            Vector3 pSep = FuselageSurfacePoint(mz, deg, 0.028f, out Quaternion rotSep);
            AddPart(transform, PrimitiveType.Cube, pSep, new Vector3(0.012f, 0.068f, 0.022f), darkMat, darkNavy, rotSep * Quaternion.Euler(0f, 0f, t % 2 == 0 ? 15f : -15f));
        }

        var audio = gameObject.AddComponent<AudioSource>();
        audio.spatialBlend = 1.0f;
        audio.rolloffMode = AudioRolloffMode.Logarithmic;
        audio.minDistance = 2.5f;
        audio.maxDistance = 36f;
        audio.dopplerLevel = 1.0f;
        audio.loop = true;
        var clip = Resources.Load<AudioClip>("Audio/sfx_missile");
        if (clip != null)
        {
            audio.clip = clip;
            audio.volume = 0.30f * RaceAudio.MasterSfxVolume;
            audio.pitch = 1.05f;
            audio.Play();
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt == 0) return;
        age += dt;
        if (age > 9f) { Explode(); return; }

        Vector3 aim;
        bool homing = target != null && (target.transform.position - transform.position).magnitude < 35f && Mathf.Abs(target.transform.position.y - transform.position.y) < 7f;
        if (homing) aim = target.transform.position + Vector3.up * 0.7f;
        else
        {
            lateral = Mathf.MoveTowards(lateral, 0, 4f * dt);
            aim = track.PointAt(index + 7, lateral) + Vector3.up * 0.7f;
        }
        dir = Vector3.RotateTowards(dir, (aim - transform.position).normalized, (homing ? 5f : 8f) * dt, 0f);
        transform.position += dir * 50f * dt;
        transform.rotation = Quaternion.LookRotation(dir);
        track.Locate(transform.position, ref index, out _, out _);

        Fx.Emit(transform.position - dir * 0.6f, -dir * 4f, new Color(1f, 0.5f, 0.2f), 0.5f, 0.25f, 1, 0.8f);
        Fx.Emit(transform.position - dir * 0.6f, -dir * 2f + Vector3.up, new Color(0.6f, 0.6f, 0.6f, 0.5f), 0.7f, 0.5f, 1, 0.5f);

        foreach (var k in rm.Karts)
        {
            if (k == Owner) continue;
            if (k.IsRemote) continue; // オンライン時は被弾する側（ローカル操作者またはホストのCPU）が判定する！
            if ((k.transform.position + Vector3.up * 0.6f - transform.position).magnitude < 1.9f)
            {
                if (k.Shielded || k.Spin())
                {
                    Explode();
                    return;
                }
            }
        }
        if (rm.MissileHitsBanana(transform.position)) Explode();
    }

    void Explode()
    {
        Fx.Burst(transform.position, new Color(1f, 0.45f, 0.1f), 30, 9f, 0.8f);
        Fx.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 15, 5f, 0.6f);
        float atten = 1f;
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            float dist = Vector3.Distance(mainCam.transform.position, transform.position);
            atten = Mathf.Clamp01(1f - dist / 65f);
        }
        if (atten > 0.05f) rm.Audio.Explode(atten);
        rm.RemoveMissile(this);
        Destroy(gameObject);
    }
}
