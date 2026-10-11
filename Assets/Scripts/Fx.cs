using UnityEngine;

// パーティクル（火花・煙・爆発など）をまとめて出す
public static class Fx
{
    static ParticleSystem ps;
    static ParticleSystem smokePs;
    static ParticleSystem sparkPs;

    public static void Init(Material glow)
    {
        ps = CreateSystem("Fx", glow, 8000, false, false);
        smokePs = CreateSystem("Fx_Smoke", glow, 3000, true, false);
        sparkPs = CreateSystem("Fx_Sparks", glow, 6000, false, true);
    }

    public static void Clear()
    {
        if (ps != null) ps.Clear();
        if (smokePs != null) smokePs.Clear();
        if (sparkPs != null) sparkPs.Clear();
    }

    static ParticleSystem CreateSystem(string name, Material glow, int maxParticles, bool expanding, bool stretchedSpark)
    {
        var sys = new GameObject(name).AddComponent<ParticleSystem>();
        sys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sys.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.gravityModifier = stretchedSpark ? 0.75f : (expanding ? -0.1f : 0f);
        var emission = sys.emission;
        emission.rateOverTime = 0;
        var shape = sys.shape;
        shape.enabled = false;
        var sol = sys.sizeOverLifetime;
        sol.enabled = true;
        // 煙はモクモクと拡大し、火花は縮小する
        sol.size = new ParticleSystem.MinMaxCurve(1f, expanding
            ? new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f))
            : stretchedSpark
                ? new AnimationCurve(new Keyframe(0, 1.0f), new Keyframe(0.55f, 0.75f), new Keyframe(1, 0f))
                : AnimationCurve.Linear(0, 1, 1, 0));
        var col = sys.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(stretchedSpark ? 0.75f : 0.6f, 0.5f), new GradientAlphaKey(0, 1) });
        col.color = g;

        var mat = new Material(glow)
        {
            mainTexture = TextureGen.SoftDot()
        };
        // 色飛び（白飛び）を防ぎつつ鮮やかな発色を維持するティント設定
        Color tint = stretchedSpark
            ? new Color(0.65f, 0.65f, 0.65f, 0.85f)
            : expanding
                ? new Color(0.50f, 0.50f, 0.50f, 0.50f)
                : new Color(0.60f, 0.60f, 0.60f, 0.80f);
        mat.SetColor("_TintColor", tint);
        mat.SetColor("_Color", tint);
        mat.SetColor("_BaseColor", tint);

        var psr = sys.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        if (stretchedSpark)
        {
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.026f;
            psr.lengthScale = 2.1f;
        }
        sys.Play();
        return sys;
    }

    public static void Emit(Vector3 pos, Vector3 vel, Color color, float size, float life, int count = 1, float spread = 1f)
    {
        if (ps == null) return;
        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + Random.insideUnitSphere * 0.15f,
                velocity = vel + Random.insideUnitSphere * spread,
                startColor = color,
                startSize = size * Random.Range(0.7f, 1.3f),
                startLifetime = life * Random.Range(0.7f, 1.3f),
            };
            ps.Emit(ep, 1);
        }
    }

    // 速度方向に鋭く伸びる火花ストリーク（ドリフト火花・ターボ噴射用）
    public static void Spark(Vector3 pos, Vector3 vel, Color color, float size, float life, int count = 1, float spread = 0.8f)
    {
        if (sparkPs == null) return;
        for (int i = 0; i < count; i++)
        {
            var jitterVel = new Vector3(
                Random.Range(-1f, 1f) * spread,
                Random.Range(-0.2f, 0.6f) * spread,
                Random.Range(-1f, 1f) * spread
            );
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(0f, 0.04f), Random.Range(-0.06f, 0.06f)),
                velocity = vel + jitterVel,
                startColor = color,
                startSize = size * Random.Range(0.80f, 1.20f),
                startLifetime = life * Random.Range(0.80f, 1.20f),
            };
            sparkPs.Emit(ep, 1);
        }
    }

    // ドリフトレベル到達時やターボ解放時の指向性火花バースト（タイヤ接地面から後方へ扇状に弾ける）
    public static void SparkBurst(Vector3 pos, Vector3 baseVel, Color primaryCol, Color coreCol, int count, float spread = 2.8f, float size = 0.18f)
    {
        if (sparkPs == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 jitter = new Vector3(
                Random.Range(-1f, 1f) * spread,
                Random.Range(0f, 0.55f) * spread,
                Random.Range(-1f, 1f) * spread
            );
            Vector3 v = baseVel * Random.Range(0.80f, 1.25f) + jitter;
            Color c = (i % 3 == 0) ? coreCol : Color.Lerp(primaryCol, coreCol, Random.Range(0f, 0.35f));
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0f, 0.05f), Random.Range(-0.08f, 0.08f)),
                velocity = v,
                startColor = c,
                startSize = size * Random.Range(0.85f, 1.25f),
                startLifetime = Random.Range(0.14f, 0.24f),
            };
            sparkPs.Emit(ep, 1);
        }
    }

    public static void Burst(Vector3 pos, Color color, int count, float speed = 8f, float size = 0.6f)
    {
        for (int i = 0; i < count; i++)
            Emit(pos, Random.onUnitSphere * speed * Random.Range(0.3f, 1f) + Vector3.up * 2f, color, size, 0.7f, 1, 0.5f);
    }

    public static void Smoke(Vector3 pos, Vector3 vel, Color color, float size, float life, int count = 1, float spread = 0.8f)
    {
        if (smokePs == null) return;
        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos + Random.insideUnitSphere * 0.2f,
                velocity = vel + Random.insideUnitSphere * spread,
                startColor = color,
                startSize = size * Random.Range(0.8f, 1.2f),
                startLifetime = life * Random.Range(0.8f, 1.3f),
            };
            smokePs.Emit(ep, 1);
        }
    }
}

