using UnityEngine;

// 激流・川の水面テクスチャを滑らかにスクロールさせるアニメーションコンポーネント
public class RiverFlowAnimator : MonoBehaviour
{
    public Material riverMat;

    void Update()
    {
        if (riverMat != null)
        {
            float vOffset = -Mathf.Repeat(Time.time * 0.75f, 1f);
            var offset = new Vector2(0f, vOffset);
            riverMat.mainTextureOffset = offset;
            riverMat.SetTextureOffset("_BaseMap", offset);
            riverMat.SetTextureOffset("_EmissionMap", offset);
        }
    }
}

// ジャンプ台のシェブロン矢印推進アニメーション＆警告ビーコンのストロボ点滅制御
public class JumpRampAnimator : MonoBehaviour
{
    Material slopeMat;
    Material beaconMat;

    public void Init(Material sMat, Material bMat)
    {
        slopeMat = sMat;
        beaconMat = bMat;
    }

    void Update()
    {
        // 1. スロープ面の矢印推進アニメーション (手前から奥・テイクオフ側へ滑らかに高速スクロール)
        if (slopeMat != null)
        {
            float vOffset = -Mathf.Repeat(Time.time * 1.6f, 1f);
            var offset = new Vector2(0f, vOffset);
            slopeMat.mainTextureOffset = offset;
            slopeMat.SetTextureOffset("_BaseMap", offset);
            slopeMat.SetTextureOffset("_EmissionMap", offset);
        }

        // 2. 警告ビーコンのストロボパルス点滅 (0.4秒周期でピカッと強く発光し、余韻を持って減衰)
        if (beaconMat != null)
        {
            float t = Mathf.Repeat(Time.time * 2.5f, 1f);
            float flash = Mathf.Pow(Mathf.Clamp01(1f - t), 2.2f);
            beaconMat.color = Color.Lerp(new Color(0.7f, 0.4f, 0.05f), new Color(1.8f, 1.4f, 0.25f), flash);
        }
    }
}
