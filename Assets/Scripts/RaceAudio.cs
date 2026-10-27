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
    AudioClip beep, go, pickup, tick, gotItem, boost, hit, bump, missile, drop, lap, finalLap, finish, pop, star, shield, select, explosion;
    AudioClip[] bumps;
    readonly AudioClip[] bgm = new AudioClip[7];

    float lastBumpTime = -10f;
    float lastHitTime = -10f;
    float lastExplodeTime = -10f;
    float lastTickTime = -10f;

    static AudioClip Load(string name, System.Func<AudioClip> fallback)
    {
        var clip = Resources.Load<AudioClip>("Audio/" + name);
        return clip != null ? clip : fallback();
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

        // Resources/Audio の実素材（リマスター版アーケードSE・物理フォーリー効果音）を読み込み、欠けている場合のみ合成音を生成
        beep = Load("sfx_beep", () => Make(0.26f, (t, d) => (WarmTone(1046.5f, t) * 0.40f + WarmTone(2093.0f, t) * 0.15f) * Env(t, d)));
        go = Load("sfx_go", () => Make(0.70f, (t, d) => (WarmTone(1046.5f, t) * 0.25f + WarmTone(1318.5f, t) * 0.22f + WarmTone(1568.0f, t) * 0.22f + WarmTone(2093.0f, t) * 0.20f) * Env(t, d)));
        pickup = Load("sfx_pickup", () => Make(0.30f, (t, d) => WarmTone(Arp(t, 0.06f, 523, 659, 784, 1047), t) * 0.3f * Env(t, d)));
        tick = Load("sfx_tick", () => Make(0.045f, (t, d) => (WarmTone(2800.0f, t) * 0.25f + Noise() * 0.10f) * Env(t, d)));
        gotItem = Load("sfx_gotitem", () => Make(0.40f, (t, d) => WarmTone(Arp(t, 0.07f, 784, 1047, 1319, 1568), t) * 0.32f * Env(t, d)));
        boost = Load("sfx_boost", () => Make(0.8f, (t, d) => (Noise() * 0.25f + WarmTone(Mathf.Lerp(140, 320, t / d), t) * 0.35f) * Mathf.Sin(Mathf.PI * t / d) * 0.5f));
        hit = Load("sfx_hit", () => Make(0.45f, (t, d) => (WarmTone(Mathf.Lerp(260, 95, t / d), t) * 0.35f + Noise() * 0.15f) * Env(t, d)));
        missile = Load("sfx_missile", () => Make(0.5f, (t, d) => (Noise() * 0.25f + WarmTone(Mathf.Lerp(220, 520, t / d), t) * 0.25f) * Env(t, d)));
        drop = Load("sfx_drop", () => Make(0.2f, (t, d) => WarmTone(Mathf.Lerp(420, 190, t / d), t) * 0.4f * Env(t, d)));
        lap = Load("sfx_lap", () => Make(0.50f, (t, d) => WarmTone(Arp(t, 0.07f, 784, 1047, 1319), t) * 0.32f * Env(t, d)));
        finalLap = Load("sfx_finallap", () => Make(1.10f, (t, d) => WarmTone(Arp(t, 0.09f, 523, 659, 784, 1047, 1568), t) * 0.35f * Env(t, d)));
        finish = Load("sfx_finish", () => Make(2.0f, (t, d) => WarmTone(Arp(t, 0.11f, 523, 659, 784, 880, 784, 1047, 1319, 1568), t) * 0.36f * Env(t, d)));
        pop = Load("sfx_pop", () => Make(0.18f, (t, d) => (Noise() * 0.20f + WarmTone(1318.5f, t) * 0.35f + WarmTone(2637.0f, t) * 0.15f) * Env(t, d)));
        star = Load("sfx_star", () => Load("sfx_shield", () => Make(1.1f, (t, d) => WarmTone(Arp(t, 0.10f, 523, 659, 784, 1047, 1319), t) * 0.32f * Env(t, d))));
        shield = star;
        select = Load("sfx_select", () => beep);
        explosion = Load("sfx_explosion", () => hit);
        System.Func<AudioClip> bumpFallback = () => bump != null ? bump : (bump = Make(0.18f, (t, d) => (WarmTone(145, t) * 0.45f + Noise() * 0.12f) * Env(t, d)));
        bumps = new[] { Load("sfx_bump_0", bumpFallback), Load("sfx_bump_1", bumpFallback), Load("sfx_bump_2", bumpFallback) };

        var engineClip = Resources.Load<AudioClip>("Audio/sfx_engine");
        useRealEngine = engineClip != null;
        engine = Loop(engineClip != null ? engineClip : MakeEngine(), 0f);

        var driftClip = Resources.Load<AudioClip>("Audio/sfx_drift");
        driftLoop = Loop(driftClip != null ? driftClip : Make(1f, (t, d) => Noise() * 0.25f), 0f);

        // 全7コースの BGM を読み込む（万一欠けていた場合のみフォールバック）
        bool missing = false;
        for (int i = 0; i < bgm.Length; i++)
        {
            bgm[i] = Resources.Load<AudioClip>("Audio/bgm_" + i);
            if (bgm[i] == null) missing = true;
        }
        if (missing)
        {
            var fallbackBgm = bgm[0] != null ? bgm[0] : MakeMusic();
            for (int i = 0; i < bgm.Length; i++) if (bgm[i] == null) bgm[i] = fallbackBgm;
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
        music.pitch = 1f;
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

    float currentEngineRpm = 0f;
    float currentEngineVol = 0f;

    public void SetEngine(float speed01, float throttle, bool active, bool drifting, int driftLevel = 0)
    {
        if (engine == null) return;
        if (!active)
        {
            currentEngineVol = Mathf.MoveTowards(currentEngineVol, 0f, Time.unscaledDeltaTime * 4f);
            engine.volume = currentEngineVol * MasterSfxVolume;
            if (driftLoop != null) driftLoop.volume = Mathf.MoveTowards(driftLoop.volume, 0f, Time.unscaledDeltaTime * 3f);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        float absSpeed = Mathf.Clamp01(Mathf.Abs(speed01));
        float fwdThrottle = Mathf.Clamp01(throttle);

        // RPMモデル:
        // - アクセルON時: エンジン回転数が即座に先行して吹け上がる（レスポンス・加速感）
        // - アクセルOFF時: 負荷が抜け、回転数が素早く落ち着く（耳障りな唸りを防止）
        // - ドリフトチャージ時は少し高回転に吹け上がる
        float boostBonus = (driftLevel > 0) ? (0.05f * driftLevel) : 0f;
        float targetRpm = Mathf.Clamp01(absSpeed * 0.72f + fwdThrottle * 0.28f + boostBonus);

        // 上昇時はクイック(4.0f)、下降時は滑らかに低下(3.0f)
        float rpmSpeed = (targetRpm > currentEngineRpm) ? 4.0f : 3.0f;
        currentEngineRpm = Mathf.MoveTowards(currentEngineRpm, targetRpm, dt * rpmSpeed);

        // ピッチレンジ: 0.78f (アイドリング) 〜 1.32f (全開) の自然で快いレーシング音域
        float basePitch = useRealEngine ? 0.78f : 0.65f;
        float pitchRange = useRealEngine ? 0.54f : 0.60f;
        engine.pitch = basePitch + currentEngineRpm * pitchRange;

        // 音量:
        // - アイドリング時 (RPM低): 0.055f (静かで落ち着いた低音)
        // - 巡航・全開時: 最大 0.135f (BGMやSEと調和する適正音量)
        // - スロットル負荷時 (fwdThrottle > 0): ほんの少し音圧を加える (+0.015f)
        float targetVol = Mathf.Lerp(0.055f, 0.135f, currentEngineRpm) + (fwdThrottle * 0.015f);
        currentEngineVol = Mathf.MoveTowards(currentEngineVol, targetVol, dt * 3.0f);
        engine.volume = currentEngineVol * MasterSfxVolume;

        // ドリフトタイヤスキッド音（速度とドリフト段階に応じてピッチと音量を自然に変化させる）
        if (driftLoop != null)
        {
            float targetDriftVol = drifting ? Mathf.Lerp(0.045f, 0.080f, absSpeed) * MasterSfxVolume : 0f;
            driftLoop.volume = Mathf.MoveTowards(driftLoop.volume, targetDriftVol, dt * 3.0f);
            float targetDriftPitch = 0.92f + absSpeed * 0.14f + driftLevel * 0.03f;
            driftLoop.pitch = Mathf.MoveTowards(driftLoop.pitch, targetDriftPitch, dt * 2.5f);
        }
    }

    public void SetEngine(float speed01, bool active, bool drifting)
    {
        SetEngine(speed01, speed01 > 0.05f ? 1f : 0f, active, drifting, 0);
    }

    public void SetMusicTempo(float pitch) => music.pitch = pitch;
    public void SetMusicVolume(float v)
    {
        currentMusicLevel = v;
        RefreshMusicVolume();
    }

    public void Select() => Play(select, 0.28f, Random.Range(0.97f, 1.03f));

    public void Explode(float volumeScale = 1f)
    {
        float now = Time.unscaledTime;
        if (now - lastExplodeTime < 0.14f) return;
        lastExplodeTime = now;
        float v = 0.35f * Mathf.Clamp01(volumeScale);
        if (v > 0.01f) Play(explosion, v, Random.Range(0.95f, 1.05f));
    }

    public void Beep() => Play(beep, 0.38f);
    public void Go() => Play(go, 0.44f);
    public void Pickup() => Play(pickup, 0.35f, Random.Range(0.97f, 1.04f));

    public void Tick()
    {
        float now = Time.unscaledTime;
        if (now - lastTickTime < 0.045f) return;
        lastTickTime = now;
        Play(tick, 0.22f, Random.Range(0.96f, 1.05f));
    }

    public void GotItem() => Play(gotItem, 0.36f);
    public void Boost() => Play(boost, 0.36f, Random.Range(0.96f, 1.05f));

    public void Hit()
    {
        float now = Time.unscaledTime;
        if (now - lastHitTime < 0.20f) return;
        lastHitTime = now;
        Play(hit, 0.34f, Random.Range(0.95f, 1.05f));
    }

    public void Bump(float intensity = 1f)
    {
        float now = Time.unscaledTime;
        if (now - lastBumpTime < 0.16f) return;
        lastBumpTime = now;
        float v = 0.26f * Mathf.Clamp(intensity, 0.40f, 1.0f);
        Play(bumps[Random.Range(0, bumps.Length)], v, Random.Range(0.92f, 1.08f));
    }

    public void Missile() => Play(missile, 0.34f, Random.Range(0.96f, 1.05f));
    public void Drop() => Play(drop, 0.32f, Random.Range(0.95f, 1.05f));
    public void Lap() => Play(lap, 0.40f);
    public void FinalLap() => Play(finalLap, 0.44f);
    public void Finish() => Play(finish, 0.46f);
    public void Pop(int level) => Play(pop, 0.32f, 0.88f + level * 0.12f);
    public void DriftSpark(int level) => Play(pop, 0.20f, 0.95f + level * 0.14f);
    public void Star() => Play(star, 0.38f);
    public void Shield() => Star();

    void Play(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null) return;
        AudioSource s = null;
        for (int i = 0; i < pool.Length; i++)
        {
            int idx = (next + i) % pool.Length;
            if (!pool[idx].isPlaying)
            {
                s = pool[idx];
                next = (idx + 1) % pool.Length;
                break;
            }
        }
        if (s == null)
        {
            s = pool[next];
            next = (next + 1) % pool.Length;
        }
        s.pitch = pitch;
        s.PlayOneShot(clip, vol * MasterSfxVolume);
    }

    static float WarmTone(float f, float t) =>
        Mathf.Sin(2 * Mathf.PI * f * t) * 0.7f + Mathf.Sin(2 * Mathf.PI * f * 2f * t) * 0.2f + Mathf.Sin(2 * Mathf.PI * f * 3f * t) * 0.1f;

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
        const float f = 55f;
        int n = Rate; // 1秒 = 55周期
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // 柔らかいエンジン燃焼パルス（低周波主体の倍音構造）
            float p = Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f 
                    + Mathf.Sin(2 * Mathf.PI * f * 2f * t) * 0.3f 
                    + Mathf.Sin(2 * Mathf.PI * f * 3f * t) * 0.15f;
            p = Mathf.Clamp(p * 1.5f, -0.8f, 0.8f);
            data[i] = p * 0.45f;
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
