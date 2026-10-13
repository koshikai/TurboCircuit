using System.Collections.Generic;
using UnityEngine;

// 観客（トイ・ポップ調のキュートな3Dサポーターキャラクター）
public class SpectatorFan : MonoBehaviour
{
    public enum CheerType
    {
        WaveFlag,      // 応援旗を振る
        PumpArms,      // 両手を突き上げてジャンプ
        ClapHands,     // 手拍子
        HoldMegaphone, // メガホンで声援
        SwayCheer      // ポンポンを振って横ノリ
    }

    public CheerType cheerType = CheerType.PumpArms;
    public Transform bodyGroup;
    public Transform headGroup;
    public Transform armL;
    public Transform armR;
    public Transform flagTransform;

    Vector3 initialPos;
    Quaternion initialHeadRot;
    Quaternion initialArmLRot;
    Quaternion initialArmRRot;

    float phase;
    float tempo = 5.2f;
    float jumpHeight = 0.25f;
    float swayAngle = 6.0f;
    bool isInit = false;

    public void Init(CheerType type, float phaseOffset, float speedMult = 1f)
    {
        cheerType = type;
        phase = phaseOffset;
        tempo = Random.Range(4.2f, 5.8f) * speedMult;
        jumpHeight = Random.Range(0.28f, 0.45f);
        swayAngle = Random.Range(5.5f, 9.0f);

        initialPos = transform.position;
        if (headGroup != null) initialHeadRot = headGroup.localRotation;
        if (armL != null) initialArmLRot = armL.localRotation;
        if (armR != null) initialArmRRot = armR.localRotation;
        isInit = true;
    }

    void Start()
    {
        if (!isInit)
        {
            initialPos = transform.position;
            if (headGroup != null) initialHeadRot = headGroup.localRotation;
            if (armL != null) initialArmLRot = armL.localRotation;
            if (armR != null) initialArmRRot = armR.localRotation;
            isInit = true;
        }
    }

    void Update()
    {
        float t = Time.time * tempo + phase;

        // 1. 体の上下バウンド＆左右スウェイ（横ノリ）
        float hop = Mathf.Abs(Mathf.Sin(t)) * jumpHeight;
        float sway = Mathf.Sin(t * 0.5f) * swayAngle;

        transform.position = initialPos + Vector3.up * hop;
        if (bodyGroup != null)
        {
            bodyGroup.localRotation = Quaternion.Euler(0f, 0f, sway);
        }

        // 2. 首振り・視線（レースカーを目で追う）
        if (headGroup != null)
        {
            float lookYaw = Mathf.Sin(t * 0.35f) * 16f;
            float nodPitch = Mathf.Sin(t * 1.0f) * 4f;
            headGroup.localRotation = initialHeadRot * Quaternion.Euler(nodPitch, lookYaw, 0f);
        }

        // 3. 腕・応援アイテムのアニメーション
        switch (cheerType)
        {
            case CheerType.WaveFlag:
                if (armR != null)
                {
                    // 旗を頭上で左右・前後に大きくダイナミックに振る
                    float flagWaveZ = Mathf.Sin(t * 1.4f) * 36f;
                    float flagWaveX = Mathf.Cos(t * 1.4f) * 18f;
                    armR.localRotation = initialArmRRot * Quaternion.Euler(flagWaveX, 0f, flagWaveZ);
                }
                if (armL != null)
                {
                    float pumpL = Mathf.Sin(t * 0.7f) * 12f;
                    armL.localRotation = initialArmLRot * Quaternion.Euler(pumpL, 0f, 0f);
                }
                break;

            case CheerType.PumpArms:
                // 両手を高々と突き上げてジャンプのリズムに合わせてプッシュ
                float pump = Mathf.Sin(t) * 18f;
                if (armL != null) armL.localRotation = initialArmLRot * Quaternion.Euler(pump, 0f, 0f);
                if (armR != null) armR.localRotation = initialArmRRot * Quaternion.Euler(pump, 0f, 0f);
                break;

            case CheerType.ClapHands:
                // 手拍子（胸の前でパチパチ）
                float clap = Mathf.Abs(Mathf.Sin(t * 1.8f)) * 26f;
                if (armL != null) armL.localRotation = initialArmLRot * Quaternion.Euler(0f, clap, 0f);
                if (armR != null) armR.localRotation = initialArmRRot * Quaternion.Euler(0f, -clap, 0f);
                break;

            case CheerType.HoldMegaphone:
                // メガホンを口元に向けて上下にコール
                float yell = Mathf.Sin(t * 0.9f) * 14f;
                if (armR != null) armR.localRotation = initialArmRRot * Quaternion.Euler(yell, 0f, 0f);
                if (armL != null) armL.localRotation = initialArmLRot * Quaternion.Euler(-yell * 0.4f, 0f, 0f);
                break;

            case CheerType.SwayCheer:
            default:
                // 両手を広げて左右に揺らす
                float armSway = Mathf.Sin(t * 0.5f) * 16f;
                if (armL != null) armL.localRotation = initialArmLRot * Quaternion.Euler(0f, 0f, armSway);
                if (armR != null) armR.localRotation = initialArmRRot * Quaternion.Euler(0f, 0f, -armSway);
                break;
        }
    }

    // --- プロシージャル観客モデル生成ファクトリ ---

    static GameObject CreatePart(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot = default)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = (localRot == default) ? Quaternion.identity : localRot;
        go.transform.localScale = localScale;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true;
        }
        return go;
    }

    public static GameObject CreateFan(
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        System.Random rng,
        RaceManager rm,
        SpectatorPalette palette)
    {
        var fanRoot = new GameObject("SpectatorFan");
        fanRoot.transform.SetParent(parent, false);
        fanRoot.transform.position = position;
        fanRoot.transform.rotation = rotation;

        var comp = fanRoot.AddComponent<SpectatorFan>();

        // ランダムマテリアル選定
        var skinMat = palette.skinMats[rng.Next(palette.skinMats.Length)];
        var shirtMat = palette.shirtMats[rng.Next(palette.shirtMats.Length)];
        var pantsMat = palette.pantsMats[rng.Next(palette.pantsMats.Length)];
        var shoeMat = palette.shoeMats[rng.Next(palette.shoeMats.Length)];
        var capMat = palette.shirtMats[rng.Next(palette.shirtMats.Length)];
        var hairMat = palette.hairMats[rng.Next(palette.hairMats.Length)];
        var accentMat = palette.accentMat;
        var whiteMat = palette.whiteMat;
        var blackMat = palette.blackMat;
        var mouthMat = palette.mouthMat;

        // 1. 体グループ（上下バウンド・横揺れピボット）
        var bodyGroup = new GameObject("BodyGroup");
        bodyGroup.transform.SetParent(fanRoot.transform, false);
        bodyGroup.transform.localScale = Vector3.one * 2.4f; // スタジアム席（段差2.1m）の迫力に負けない堂々たるビッグスケール
        comp.bodyGroup = bodyGroup.transform;

        // 2. 下半身・スニーカー
        // ズボン脚
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3(-0.11f, 0.11f, 0f), new Vector3(0.13f, 0.16f, 0.14f), pantsMat);
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3( 0.11f, 0.11f, 0f), new Vector3(0.13f, 0.16f, 0.14f), pantsMat);

        // スニーカー（白ソール＋カラフルアッパー）
        // 左足
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3(-0.11f, 0.025f, 0.03f), new Vector3(0.15f, 0.05f, 0.23f), whiteMat);
        CreatePart(PrimitiveType.Sphere, bodyGroup.transform, new Vector3(-0.11f, 0.065f, 0.04f), new Vector3(0.14f, 0.09f, 0.19f), shoeMat);
        // 右足
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3( 0.11f, 0.025f, 0.03f), new Vector3(0.15f, 0.05f, 0.23f), whiteMat);
        CreatePart(PrimitiveType.Sphere, bodyGroup.transform, new Vector3( 0.11f, 0.065f, 0.04f), new Vector3(0.14f, 0.09f, 0.19f), shoeMat);

        // 腰・ズボン
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.35f, 0.12f, 0.26f), pantsMat);

        // 3. 上半身・シャツ
        // 胴体
        CreatePart(PrimitiveType.Capsule, bodyGroup.transform, new Vector3(0f, 0.41f, 0f), new Vector3(0.38f, 0.26f, 0.28f), shirtMat);
        // 白襟
        CreatePart(PrimitiveType.Cylinder, bodyGroup.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.18f, 0.025f, 0.18f), whiteMat);
        // 胸のアクセントマーク
        CreatePart(PrimitiveType.Cube, bodyGroup.transform, new Vector3(0f, 0.42f, 0.145f), new Vector3(0.15f, 0.15f, 0.02f), accentMat);

        // 4. 頭部グループ（首振りピボット）
        var headGroup = new GameObject("HeadGroup");
        headGroup.transform.SetParent(bodyGroup.transform, false);
        headGroup.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        comp.headGroup = headGroup.transform;

        // 頭部本体（球）
        CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.38f, skinMat);

        // つぶらな黒目＋白ハイライト（視認性UPのため拡大）
        CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(-0.080f, 0.20f, 0.170f), Vector3.one * 0.068f, blackMat);
        CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3( 0.080f, 0.20f, 0.170f), Vector3.one * 0.068f, blackMat);
        CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(-0.068f, 0.220f, 0.195f), Vector3.one * 0.028f, whiteMat);
        CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3( 0.092f, 0.220f, 0.195f), Vector3.one * 0.028f, whiteMat);

        // にっこり笑顔
        CreatePart(PrimitiveType.Cube, headGroup.transform, new Vector3(0f, 0.130f, 0.182f), new Vector3(0.065f, 0.025f, 0.02f), mouthMat);

        // ヘッドギア（帽子・カチューシャ等：6タイプ）
        int hatType = rng.Next(6);
        switch (hatType)
        {
            case 0: // ベースボール / レーシングキャップ
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.39f, 0.29f, 0.39f), capMat);
                CreatePart(PrimitiveType.Cube, headGroup.transform, new Vector3(0f, 0.23f, 0.24f), new Vector3(0.26f, 0.028f, 0.18f), capMat, Quaternion.Euler(14f, 0f, 0f));
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.40f, 0f), Vector3.one * 0.05f, accentMat);
                break;

            case 1: // サンバイザー
                CreatePart(PrimitiveType.Cylinder, headGroup.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.40f, 0.045f, 0.40f), capMat);
                CreatePart(PrimitiveType.Cube, headGroup.transform, new Vector3(0f, 0.23f, 0.24f), new Vector3(0.26f, 0.028f, 0.18f), capMat, Quaternion.Euler(14f, 0f, 0f));
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.23f, -0.04f), new Vector3(0.38f, 0.25f, 0.36f), hairMat);
                break;

            case 2: // ポンポン付きニット帽
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.26f, 0f), new Vector3(0.40f, 0.34f, 0.40f), capMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.44f, 0f), Vector3.one * 0.12f, whiteMat);
                break;

            case 3: // クマ耳/ネコ耳カチューシャ
                CreatePart(PrimitiveType.Cylinder, headGroup.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.39f, 0.035f, 0.39f), blackMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(-0.15f, 0.37f, 0.02f), Vector3.one * 0.11f, capMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3( 0.15f, 0.37f, 0.02f), Vector3.one * 0.11f, capMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(-0.15f, 0.37f, 0.06f), Vector3.one * 0.06f, mouthMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3( 0.15f, 0.37f, 0.06f), Vector3.one * 0.06f, mouthMat);
                break;

            case 4: // レーシングヘッドホン
                CreatePart(PrimitiveType.Cylinder, headGroup.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.40f, 0.04f, 0.40f), blackMat);
                CreatePart(PrimitiveType.Cylinder, headGroup.transform, new Vector3(-0.21f, 0.21f, 0f), new Vector3(0.12f, 0.07f, 0.12f), capMat, Quaternion.Euler(0f, 0f, 90f));
                CreatePart(PrimitiveType.Cylinder, headGroup.transform, new Vector3( 0.21f, 0.21f, 0f), new Vector3(0.12f, 0.07f, 0.12f), capMat, Quaternion.Euler(0f, 0f, 90f));
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.25f, -0.04f), new Vector3(0.38f, 0.23f, 0.36f), hairMat);
                break;

            case 5: // サングラス＋ヘアスタイル
                CreatePart(PrimitiveType.Cube, headGroup.transform, new Vector3(0f, 0.21f, 0.18f), new Vector3(0.26f, 0.07f, 0.04f), blackMat);
                CreatePart(PrimitiveType.Cube, headGroup.transform, new Vector3(0f, 0.21f, 0.198f), new Vector3(0.24f, 0.055f, 0.02f), accentMat);
                CreatePart(PrimitiveType.Sphere, headGroup.transform, new Vector3(0f, 0.27f, -0.04f), new Vector3(0.39f, 0.27f, 0.38f), hairMat);
                break;
        }

        // 5. 腕と応援アイテム
        // 応援タイプの決定
        int cheerRand = rng.Next(100);
        CheerType cType;
        if (cheerRand < 35) cType = CheerType.WaveFlag;
        else if (cheerRand < 60) cType = CheerType.PumpArms;
        else if (cheerRand < 75) cType = CheerType.ClapHands;
        else if (cheerRand < 90) cType = CheerType.HoldMegaphone;
        else cType = CheerType.SwayCheer;

        // 腕のピボット
        var armLGo = new GameObject("ArmL");
        armLGo.transform.SetParent(bodyGroup.transform, false);
        armLGo.transform.localPosition = new Vector3(-0.23f, 0.46f, 0f);
        comp.armL = armLGo.transform;

        var armRGo = new GameObject("ArmR");
        armRGo.transform.SetParent(bodyGroup.transform, false);
        armRGo.transform.localPosition = new Vector3( 0.23f, 0.46f, 0f);
        comp.armR = armRGo.transform;

        switch (cType)
        {
            case CheerType.WaveFlag:
                // 右腕：頭上に掲げて旗を振る
                armRGo.transform.localRotation = Quaternion.Euler(-55f, 0f, -40f);
                CreatePart(PrimitiveType.Capsule, armRGo.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.09f, 0.20f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armRGo.transform, new Vector3(0f, 0.24f, 0f), Vector3.one * 0.10f, whiteMat);

                // 旗竿とフラッグ（遠くからでも目立つ大型サイズ）
                var flagMat = (rng.Next(2) == 0 && palette.checkerMat != null) ? palette.checkerMat : palette.flagMats[rng.Next(palette.flagMats.Length)];
                CreatePart(PrimitiveType.Cylinder, armRGo.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.026f, 0.65f, 0.026f), palette.chromeMat);
                var flagGo = CreatePart(PrimitiveType.Cube, armRGo.transform, new Vector3(0.32f, 0.72f, 0f), new Vector3(0.60f, 0.40f, 0.015f), flagMat);
                comp.flagTransform = flagGo.transform;

                // 左腕：腰に当てる・少し前
                armLGo.transform.localRotation = Quaternion.Euler(-25f, 0f, 15f);
                CreatePart(PrimitiveType.Capsule, armLGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.20f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armLGo.transform, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, whiteMat);
                break;

            case CheerType.PumpArms:
                // 両腕を高々と突き上げてガッツポーズ
                armLGo.transform.localRotation = Quaternion.Euler(-130f, 0f, -18f);
                CreatePart(PrimitiveType.Capsule, armLGo.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.09f, 0.20f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armLGo.transform, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.10f, whiteMat);

                armRGo.transform.localRotation = Quaternion.Euler(-130f, 0f, 18f);
                CreatePart(PrimitiveType.Capsule, armRGo.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.09f, 0.20f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armRGo.transform, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.10f, whiteMat);
                break;

            case CheerType.ClapHands:
                // 胸の前で手を叩くポーズ
                armLGo.transform.localRotation = Quaternion.Euler(-55f, 32f, 0f);
                CreatePart(PrimitiveType.Capsule, armLGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armLGo.transform, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, whiteMat);

                armRGo.transform.localRotation = Quaternion.Euler(-55f, -32f, 0f);
                CreatePart(PrimitiveType.Capsule, armRGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armRGo.transform, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, whiteMat);
                break;

            case CheerType.HoldMegaphone:
                // 右手にメガホン
                armRGo.transform.localRotation = Quaternion.Euler(-75f, -20f, 0f);
                CreatePart(PrimitiveType.Capsule, armRGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armRGo.transform, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, whiteMat);

                var megaMat = palette.megaMats[rng.Next(palette.megaMats.Length)];
                CreatePart(PrimitiveType.Cylinder, armRGo.transform, new Vector3(0f, 0.36f, 0.08f), new Vector3(0.16f, 0.22f, 0.16f), megaMat, Quaternion.Euler(75f, 0f, 0f));

                // 左腕：腰に当てる
                armLGo.transform.localRotation = Quaternion.Euler(-20f, 0f, 20f);
                CreatePart(PrimitiveType.Capsule, armLGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armLGo.transform, new Vector3(0f, 0.20f, 0f), Vector3.one * 0.10f, whiteMat);
                break;

            case CheerType.SwayCheer:
            default:
                // ポンポンを持って横に広げる
                armLGo.transform.localRotation = Quaternion.Euler(-25f, 0f, -30f);
                CreatePart(PrimitiveType.Capsule, armLGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armLGo.transform, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.20f, accentMat);

                armRGo.transform.localRotation = Quaternion.Euler(-25f, 0f, 30f);
                CreatePart(PrimitiveType.Capsule, armRGo.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.18f, 0.09f), shirtMat);
                CreatePart(PrimitiveType.Sphere, armRGo.transform, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.20f, accentMat);
                break;
        }

        comp.Init(cType, (float)rng.NextDouble() * 6.28f);
        return fanRoot;
    }
}

// 観客用共有マテリアルパレット（ドローコール削減＆メモリ最適化）
public class SpectatorPalette
{
    public Material[] skinMats;
    public Material[] shirtMats;
    public Material[] pantsMats;
    public Material[] shoeMats;
    public Material[] hairMats;
    public Material[] flagMats;
    public Material[] megaMats;
    public Material checkerMat;
    public Material chromeMat;
    public Material whiteMat;
    public Material blackMat;
    public Material mouthMat;
    public Material accentMat;

    public static SpectatorPalette Create(RaceManager rm, List<Material> trackMatList)
    {
        var p = new SpectatorPalette();

        Material MakeMat(Color col, Texture2D tex = null, float smoothness = 0.2f)
        {
            var m = new Material(rm.wallMaterial) { color = col };
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", smoothness);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MainTex", tex);
            }
            if (trackMatList != null) trackMatList.Add(m);
            return m;
        }

        // 肌色（ライト、ナチュラル、小麦色）
        p.skinMats = new[]
        {
            MakeMat(new Color(1.00f, 0.88f, 0.76f)),
            MakeMat(new Color(0.96f, 0.80f, 0.68f)),
            MakeMat(new Color(0.85f, 0.66f, 0.52f))
        };

        // シャツカラー（ポップで鮮やかな8色）
        p.shirtMats = new[]
        {
            MakeMat(new Color(0.98f, 0.22f, 0.26f)), // レッド
            MakeMat(new Color(1.00f, 0.55f, 0.10f)), // オレンジ
            MakeMat(new Color(1.00f, 0.84f, 0.12f)), // イエロー
            MakeMat(new Color(0.18f, 0.82f, 0.42f)), // エメラルド
            MakeMat(new Color(0.10f, 0.72f, 0.98f)), // シアン
            MakeMat(new Color(0.15f, 0.45f, 0.95f)), // コバルト
            MakeMat(new Color(0.68f, 0.25f, 0.95f)), // パープル
            MakeMat(new Color(0.98f, 0.35f, 0.65f))  // ピンク
        };

        // ズボンカラー（4色）
        p.pantsMats = new[]
        {
            MakeMat(new Color(0.18f, 0.24f, 0.38f)), // インディゴ
            MakeMat(new Color(0.22f, 0.22f, 0.24f)), // チャコール
            MakeMat(new Color(0.92f, 0.92f, 0.94f)), // ホワイト
            MakeMat(new Color(0.72f, 0.64f, 0.50f))  // カーキ
        };

        // 靴アッパー（4色）
        p.shoeMats = new[]
        {
            MakeMat(new Color(0.95f, 0.20f, 0.25f)),
            MakeMat(new Color(0.12f, 0.55f, 0.95f)),
            MakeMat(new Color(1.00f, 0.80f, 0.12f)),
            MakeMat(new Color(0.18f, 0.18f, 0.20f))
        };

        // 髪の毛（3色）
        p.hairMats = new[]
        {
            MakeMat(new Color(0.25f, 0.18f, 0.12f)), // ダークブラウン
            MakeMat(new Color(0.88f, 0.72f, 0.32f)), // ブロンド
            MakeMat(new Color(0.12f, 0.12f, 0.14f))  // ブラック
        };

        // 応援旗カラー
        p.flagMats = new[]
        {
            MakeMat(new Color(0.98f, 0.22f, 0.26f)),
            MakeMat(new Color(0.15f, 0.55f, 0.98f)),
            MakeMat(new Color(1.00f, 0.85f, 0.15f)),
            MakeMat(new Color(0.18f, 0.82f, 0.42f))
        };

        // メガホン
        p.megaMats = new[]
        {
            MakeMat(new Color(0.98f, 0.22f, 0.26f)),
            MakeMat(new Color(1.00f, 0.82f, 0.10f))
        };

        // チェッカーフラッグテクスチャ
        var checkerTex = Resources.Load<Texture2D>("Props/Textures/checkers");
        p.checkerMat = MakeMat(Color.white, checkerTex);
        if (p.checkerMat.mainTexture != null)
        {
            p.checkerMat.mainTextureScale = new Vector2(2f, 2f);
        }

        p.chromeMat = rm.chromeMaterial;
        p.whiteMat = MakeMat(new Color(0.96f, 0.96f, 0.98f));
        p.blackMat = MakeMat(new Color(0.10f, 0.10f, 0.12f), null, 0.8f);
        p.mouthMat = MakeMat(new Color(0.95f, 0.35f, 0.45f));
        p.accentMat = MakeMat(new Color(1.00f, 0.86f, 0.18f), null, 0.4f);

        return p;
    }
}
