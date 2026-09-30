using UnityEngine;

// パーティクル（火花・煙・爆発など）をまとめて出す
public static class Fx
{
    static ParticleSystem ps;
    static ParticleSystem smokePs;

    public static void Init(Material glow)
    {
        ps = CreateSystem("Fx", glow, 6000, false);
        smokePs = CreateSystem("Fx_Smoke", glow, 3000, true);
    }

    static ParticleSystem CreateSystem(string name, Material glow, int maxParticles, bool expanding)
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
        main.gravityModifier = expanding ? -0.1f : 0f; // 煙は少し上へ昇る
        var emission = sys.emission;
        emission.rateOverTime = 0;
        var shape = sys.shape;
        shape.enabled = false;
        var sol = sys.sizeOverLifetime;
        sol.enabled = true;
        // 煙はモクモクと拡大し、火花は縮小する
        sol.size = new ParticleSystem.MinMaxCurve(1f, expanding
            ? new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f))
            : AnimationCurve.Linear(0, 1, 1, 0));
        var col = sys.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        col.color = g;
        var mat = new Material(glow) { mainTexture = TextureGen.SoftDot() };
        sys.GetComponent<ParticleSystemRenderer>().material = mat;
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
