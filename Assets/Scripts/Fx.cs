using UnityEngine;

// パーティクル（火花・煙・爆発など）をまとめて出す
public static class Fx
{
    static ParticleSystem ps;

    public static void Init(Material glow)
    {
        ps = new GameObject("Fx").AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 6000;
        main.gravityModifier = 0f;
        var emission = ps.emission;
        emission.rateOverTime = 0;
        var shape = ps.shape;
        shape.enabled = false;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        col.color = g;
        var mat = new Material(glow) { mainTexture = TextureGen.SoftDot() };
        ps.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
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
}
