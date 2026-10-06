using UnityEngine;

// 効果音・エンジン音・BGM。Resources/Audio の CC0 素材（Kenney / OpenGameArt）を使い、
// 素材が無い場合はコードで合成した音にフォールバックする
public class RaceAudio : MonoBehaviour
{
    const int Rate = 44100;
    const float MusicGain = 0.6f; // BGM 全体の音量倍率（効果音より控えめにする）

    AudioSource[] pool;
    int next;
    AudioSource engine, driftLoop, music;
    AudioClip beep, go, pickup, tick, gotItem, boost, hit, bump, missile, drop, lap, finalLap, finish, pop, shield, select, explosion;
    AudioClip[] bumps;
    readonly AudioClip[] bgm = new AudioClip[4];

    static AudioClip Load(string name, AudioClip fallback)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        return clip != null ? clip : fallback;
    }

    public static float MasterBgmVolume { get; private set; } = 0.7f;
    public static float MasterSfxVolume { get; private set; } = 1.0f;
    float currentMusicLevel = 0.3f;

    public void SetMasterBgm(float v)
    {
        MasterBgmVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("Audio_BgmVolume", MasterBgmVolume);
        RefreshMusicVolume();
    }

    public void SetMasterSfx(float v)
    {
        MasterSfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("Audio_SfxVolume", MasterSfxVolume);
    }

    void RefreshMusicVolume()
    {
        if (music != null) music.volume = currentMusicLevel * MusicGain * MasterBgmVolume;
    }

    void Awake()
    {
        MasterBgmVolume = PlayerPrefs.GetFloat("Audio_BgmVolume", 0.7f);
        MasterSfxVolume = PlayerPrefs.GetFloat("Audio_SfxVolume", 1.0f);

        pool = new AudioSource[12];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
        }

        beep = Make(0.3f, (t, d) => Sq(440, t) * 0.4f * Env(t, d));
        go = Make(0.7f, (t, d) => (Sq(880, t) * 0.3f + Sq(1320, t) * 0.15f) * Env(t, d));
        pickup = Make(0.25f, (t, d) => Sq(Arp(t, 0.05f, 523, 659, 784, 1047, 1319), t) * 0.3f * Env(t, d));
        tick = Make(0.04f, (t, d) => Sq(1200, t) * 0.2f * Env(t, d));
        gotItem = Make(0.3f, (t, d) => (Mathf.Sin(2 * Mathf.PI * 1568 * t) + Mathf.Sin(2 * Mathf.PI * 2093 * t)) * 0.25f * Env(t, d));
        boost = Make(0.8f, (t, d) => (Noise() * 0.5f + Saw(Mathf.Lerp(120, 400, t / d), t) * 0.3f) * Mathf.Sin(Mathf.PI * t / d) * 0.6f);
        hit = Make(0.6f, (t, d) => (Sq(Mathf.Lerp(600, 80, t / d), t) * 0.4f + Noise() * 0.3f) * Env(t, d));
        bump = Make(0.15f, (t, d) => (Noise() * 0.4f + Mathf.Sin(2 * Mathf.PI * 90 * t) * 0.6f) * Env(t, d));
        missile = Make(0.5f, (t, d) => (Noise() * 0.4f + Saw(Mathf.Lerp(300, 900, t / d), t) * 0.2f) * Env(t, d));
        drop = Make(0.2f, (t, d) => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(500, 200, t / d) * t) * 0.5f * Env(t, d));
        lap = Make(0.5f, (t, d) => Sq(Arp(t, 0.12f, 784, 988, 1175, 1568), t) * 0.25f * Env(t, d));
        finalLap = Make(1.2f, (t, d) => Sq(Arp(t, 0.15f, 523, 523, 784, 784, 1047, 1047, 1319, 1568), t) * 0.25f * Env(t, d));
        finish = Make(2.2f, (t, d) => (Sq(Arp(t, 0.14f, 523, 659, 784, 1047, 784, 1047, 1319, 1568, 1568, 1568, 1568, 1568, 1568, 1568, 1568), t) * 0.22f
                                     + Saw(Arp(t, 0.56f, 131, 175, 196, 262), t) * 0.15f) * Env(t, d));
        pop = Make(0.3f, (t, d) => (Noise() * 0.5f + Sq(Mathf.Lerp(200, 800, t / d), t) * 0.3f) * Env(t, d));
        shield = Make(1.0f, (t, d) => Sq(Arp(t, 0.06f, 523, 659, 784, 1047, 1319, 1568, 2093), t) * 0.2f * Env(t, d));

        // 実素材があるものは差し替える（lap / finalLap / finish のジングルと drift ループは合成のまま）
        beep = Load("sfx_beep", beep);
        go = Load("sfx_go", go);
        pickup = Load("sfx_pickup", pickup);
        tick = Load("sfx_tick", tick);
        gotItem = Load("sfx_gotitem", gotItem);
        boost = Load("sfx_boost", boost);
        hit = Load("sfx_hit", hit);
        missile = Load("sfx_missile", missile);
        drop = Load("sfx_drop", drop);
        pop = Load("sfx_pop", pop);
        shield = Load("sfx_shield", shield);
        select = Load("sfx_select", beep);
        explosion = Load("sfx_explosion", hit);
        bumps = new[] { Load("sfx_bump_0", bump), Load("sfx_bump_1", bump), Load("sfx_bump_2", bump) };

        var engineClip = Resources.Load<AudioClip>("Audio/sfx_engine");
        useRealEngine = engineClip != null;
        engine = Loop(engineClip != null ? engineClip : MakeEngine(), 0f);
        driftLoop = Loop(Make(1f, (t, d) => Noise() * 0.5f + Mathf.Sin(2 * Mathf.PI * 1800 * t) * 0.15f), 0f);

        // コースごとの BGM。1 曲でも欠けていたら合成 BGM で補う
        bool missing = false;
        for (int i = 0; i < bgm.Length; i++)
        {
            bgm[i] = Resources.Load<AudioClip>("Audio/bgm_" + i);
            if (bgm[i] == null) missing = true;
        }
        if (missing)
        {
            var synth = MakeMusic();
            for (int i = 0; i < bgm.Length; i++) if (bgm[i] == null) bgm[i] = synth;
        }
        music = Loop(bgm[0], currentMusicLevel * MusicGain * MasterBgmVolume);
    }

    bool useRealEngine;

    // コースに合わせて BGM を切り替える（同じ曲なら再生を続ける）
    public void SetCourseMusic(int course)
    {
        var clip = bgm[Mathf.Clamp(course, 0, bgm.Length - 1)];
        if (music.clip == clip) return;
        music.clip = clip;
        music.time = 0;
        music.Play();
    }

    public void RestartMusic(int course = -1)
    {
        if (course >= 0)
        {
            var clip = bgm[Mathf.Clamp(course, 0, bgm.Length - 1)];
            music.clip = clip;
        }
        if (music != null && music.clip != null)
        {
            music.time = 0;
            music.pitch = 1f;
            currentMusicLevel = 0.3f;
            RefreshMusicVolume();
            music.Play();
        }
    }

    AudioSource Loop(AudioClip clip, float vol)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.volume = vol;
        s.Play();
        return s;
    }

    public void SetEngine(float speed01, bool active, bool drifting)
    {
        engine.volume = (active ? 0.18f : 0f) * MasterSfxVolume;
        engine.pitch = useRealEngine ? 0.7f + Mathf.Abs(speed01) * 1.1f : 0.55f + Mathf.Abs(speed01) * 1.25f;
        driftLoop.volume = Mathf.MoveTowards(driftLoop.volume, drifting ? 0.12f * MasterSfxVolume : 0f, Time.unscaledDeltaTime * 2f);
    }

    public void SetMusicTempo(float pitch) => music.pitch = pitch;
    public void SetMusicVolume(float v)
    {
        currentMusicLevel = v;
        RefreshMusicVolume();
    }

    public void Select() => Play(select, 0.5f);
    public void Explode() => Play(explosion, 0.8f);
    public void Beep() => Play(beep, 0.6f);
    public void Go() => Play(go, 0.6f);
    public void Pickup() => Play(pickup, 0.5f);
    public void Tick() => Play(tick, 0.4f);
    public void GotItem() => Play(gotItem, 0.5f);
    public void Boost() => Play(boost, 0.5f);
    public void Hit() => Play(hit, 0.7f);
    public void Bump() => Play(bumps[Random.Range(0, bumps.Length)], 0.5f, Random.Range(0.9f, 1.15f));
    public void Missile() => Play(missile, 0.6f);
    public void Drop() => Play(drop, 0.6f);
    public void Lap() => Play(lap, 0.6f);
    public void FinalLap() => Play(finalLap, 0.6f);
    public void Finish() => Play(finish, 0.7f);
    public void Pop(int level) => Play(pop, 0.5f, 0.8f + level * 0.2f);
    public void Shield() => Play(shield, 0.5f);

    void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null) return;
        var s = pool[next];
        next = (next + 1) % pool.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, vol * MasterSfxVolume);
    }

    delegate float Gen(float t, float dur);

    static AudioClip Make(float dur, Gen gen)
    {
        int n = (int)(dur * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = gen(i / (float)Rate, dur);
        var clip = AudioClip.Create("sfx", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 継ぎ目なくループするエンジン音（基本周波数を整数周期にそろえる）
    static AudioClip MakeEngine()
    {
        const float f = 60f;
        int n = Rate; // 1秒 = 60周期
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Saw(f, t) * 0.4f + Sq(f * 0.5f, t) * 0.3f + Mathf.Sin(2 * Mathf.PI * f * 2f * t) * 0.2f;
            v *= 0.8f + 0.2f * Mathf.Sin(2 * Mathf.PI * 15f * t);
            data[i] = v * 0.6f;
        }
        var clip = AudioClip.Create("engine", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 明るいレース BGM（I–V–vi–IV、150bpm、8小節ループ）
    static AudioClip MakeMusic()
    {
        const float bpm = 150f;
        float beat = 60f / bpm;
        float[] roots = { 130.81f, 196f, 220f, 174.61f, 130.81f, 196f, 220f, 174.61f }; // C G Am F
        int[][] chords = { new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 0, 4, 7 } };
        int[] melody = { 12, 14, 16, 19, 16, 14, 12, 7, 11, 14, 19, 14, 11, 7, 11, 14, 12, 16, 19, 24, 19, 16, 12, 16, 12, 9, 12, 17, 12, 9, 5, 9 };
        int n = (int)(beat * 32 * Rate);
        var data = new float[n];
        var rng = new System.Random(2);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int bar = (int)(t / (beat * 4)) % 8;
            float root = roots[bar];
            var chord = chords[bar % 4];

            float eighth = beat / 2f;
            float te = t % eighth;
            int e = (int)(t / eighth);
            float bassF = root * 0.5f * (e % 2 == 0 ? 1f : 2f);
            float bass = Saw(bassF, t) * Mathf.Exp(-te * 5f) * 0.28f;

            float sixteenth = beat / 4f;
            float ts = t % sixteenth;
            int s16 = (int)(t / sixteenth);
            float arpF = root * 2f * Mathf.Pow(2f, chord[s16 % 3] / 12f);
            float arp = Sq(arpF, t) * Mathf.Exp(-ts * 18f) * 0.05f;

            int m = (int)(t / eighth) % 32;
            float mel = 0;
            if ((bar % 2 == 1) || m % 4 != 3)
            {
                float mf = 261.63f * Mathf.Pow(2f, melody[(e) % melody.Length] / 12f);
                mel = (Sq(mf, t) * 0.5f + Saw(mf * 1.005f, t) * 0.5f) * Mathf.Min(1f, te * 60f) * Mathf.Exp(-te * 3f) * 0.07f;
            }

            float tb = t % beat;
            float kick = Mathf.Sin(2 * Mathf.PI * 50f * tb * (1f + 3f * Mathf.Exp(-tb * 35f))) * Mathf.Exp(-tb * 8f) * 0.45f;
            int b = (int)(t / beat);
            float snare = (b % 2 == 1) ? ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-tb * 18f) * 0.2f : 0f;
            float hat = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-te * 70f) * 0.07f;

            data[i] = Mathf.Clamp(bass + arp + mel + kick + snare + hat, -1f, 1f) * 0.85f;
        }
        var clip = AudioClip.Create("music", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Arp(float t, float step, params float[] notes) => notes[Mathf.Min(notes.Length - 1, (int)(t / step))];
    static float Sq(float f, float t) => Mathf.Repeat(f * t, 1f) < 0.5f ? 1f : -1f;
    static float Saw(float f, float t) => Mathf.Repeat(f * t, 1f) * 2f - 1f;
    static float Noise() => Random.Range(-1f, 1f);
    static float Env(float t, float d) => Mathf.Clamp01(t / 0.005f) * (1f - t / d);
}
