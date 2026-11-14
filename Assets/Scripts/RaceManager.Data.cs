using System.Linq;
using UnityEngine;

[System.Serializable]
public struct KartCharacterDef
{
    public string name;
    public string driver;
    public string trait;
    public Color color;
    public int speed;     // 1〜8
    public int accel;     // 1〜8
    public int handling;  // 1〜8
    public int weight;    // 1〜8
}

[System.Serializable]
public struct KartMachineDef
{
    public string name;
    public string code;
    public string trait;
    public Color color;
    public int speed;     // 1〜8
    public int accel;     // 1〜8
    public int handling;  // 1〜8
    public int weight;    // 1〜8
}

[System.Serializable]
public struct CupDef
{
    public string id;
    public string nameEn;
    public string nameJa;
    public string descEn;
    public string descJa;
    public Color color;
    public int[] courses;
}

public enum SpeedClass { Cc50 = 0, Cc100 = 1, Cc150 = 2 }

public partial class RaceManager
{
    public static readonly int[] CupPointsTable = { 10, 8, 6, 5, 4, 3, 2, 1 };
    public static int GetCupPointsForPlace(int place) => (place >= 1 && place <= CupPointsTable.Length) ? CupPointsTable[place - 1] : 0;

    public static readonly CupDef[] DefaultCups =
    {
        new CupDef
        {
            id = "starter",
            nameEn = "STARTER CUP",
            nameJa = "スターターカップ",
            descEn = "1. Turbo Circuit -> 2. Sunset Dunes -> 3. Hokkaido Campus -> 4. Neon Metropolis",
            descJa = "1. ターボ・サーキット → 2. サンセット・デューンズ → 3. 北大札幌キャンパス → 4. ネオン・メトロポリス",
            color = new Color(0.18f, 0.72f, 0.98f),
            courses = new[] { 0, 1, 4, 3 }
        },
        new CupDef
        {
            id = "nitro",
            nameEn = "NITRO CUP",
            nameJa = "ニトロカップ",
            descEn = "1. Canyon Split -> 2. Frost Peak -> 3. Skyline Overpass -> 4. Turbo Circuit",
            descJa = "1. キャニオン・スプリット → 2. フロスト・ピーク → 3. スカイライン・オーバーパス → 4. ターボ・サーキット",
            color = new Color(0.98f, 0.58f, 0.12f),
            courses = new[] { 6, 2, 5, 0 }
        },
        new CupDef
        {
            id = "allstar",
            nameEn = "ALL-STAR TOUR",
            nameJa = "オールスターツアー",
            descEn = "All 7 Courses Grand Tour: Circuit -> Dunes -> Frost -> Neon -> Campus -> Skyline -> Canyon",
            descJa = "全7コース制覇ツアー：サーキット → 砂漠 → 雪山 → 都市 → 北大 → 高架橋 → 渓谷",
            color = new Color(0.68f, 0.32f, 0.96f),
            courses = new[] { 0, 1, 2, 3, 4, 5, 6 }
        }
    };

    public static float GetSpeedMultiplier(SpeedClass cc) => cc switch
    {
        SpeedClass.Cc50 => 0.80f,
        SpeedClass.Cc100 => 1.00f,
        SpeedClass.Cc150 => 1.15f,
        _ => 1.00f
    };
    // ───────────────────────── 全10種類のドライバー（キャラクター）定義 ─────────────────────────
    public static readonly KartCharacterDef[] DefaultKartCharacters =
    {
        new KartCharacterDef {
            name = "OOBI", driver = "Alien Ace", trait = "Balanced / All-Rounder", color = new Color(0.95f, 0.25f, 0.25f),
            speed = 5, accel = 5, handling = 5, weight = 5
        },
        new KartCharacterDef {
            name = "OODI", driver = "Pink Dash", trait = "High Drift & Turbo Boost", color = new Color(0.98f, 0.38f, 0.78f),
            speed = 4, accel = 7, handling = 6, weight = 3
        },
        new KartCharacterDef {
            name = "OOLI", driver = "Aero Sonic", trait = "Maximum Top Speed", color = new Color(0.18f, 0.65f, 1.0f),
            speed = 7, accel = 4, handling = 4, weight = 6
        },
        new KartCharacterDef {
            name = "OOPI", driver = "Racer Swift", trait = "Sharp Handling & Turn", color = new Color(1.0f, 0.62f, 0.12f),
            speed = 4, accel = 6, handling = 7, weight = 3
        },
        new KartCharacterDef {
            name = "OOZI", driver = "Cool Titan", trait = "Heavy Weight & High Grip", color = new Color(0.22f, 0.88f, 0.38f),
            speed = 6, accel = 3, handling = 4, weight = 7
        },
        new KartCharacterDef {
            name = "PANDA", driver = "Bamboo Boss", trait = "Heavyweight Brawler", color = new Color(0.18f, 0.80f, 0.62f),
            speed = 6, accel = 4, handling = 4, weight = 7
        },
        new KartCharacterDef {
            name = "PENGUIN", driver = "Ice Slider", trait = "Smooth Ice Drifter", color = new Color(0.22f, 0.78f, 0.98f),
            speed = 4, accel = 6, handling = 7, weight = 4
        },
        new KartCharacterDef {
            name = "FOX", driver = "Nitro Kitsune", trait = "Agile Speedster", color = new Color(1.0f, 0.48f, 0.14f),
            speed = 6, accel = 6, handling = 6, weight = 3
        },
        new KartCharacterDef {
            name = "BUNNY", driver = "Hopper Star", trait = "Featherweight Dash", color = new Color(0.74f, 0.42f, 0.98f),
            speed = 3, accel = 7, handling = 7, weight = 3
        },
        new KartCharacterDef {
            name = "LION", driver = "Golden King", trait = "Royal Top Speed", color = new Color(1.0f, 0.76f, 0.12f),
            speed = 7, accel = 4, handling = 4, weight = 7
        },
    };

    // ───────────────────────── 全8種類のカートマシン（車体）定義 ─────────────────────────
    public static readonly KartMachineDef[] DefaultKartMachines =
    {
        new KartMachineDef {
            name = "STANDARD", code = "STD-01", trait = "Balanced All-Rounder", color = new Color(0.95f, 0.25f, 0.25f),
            speed = 5, accel = 5, handling = 5, weight = 5
        },
        new KartMachineDef {
            name = "PIPE BUGGY", code = "BUG-02", trait = "Light & Quick Accel", color = new Color(1.0f, 0.55f, 0.12f),
            speed = 4, accel = 7, handling = 6, weight = 3
        },
        new KartMachineDef {
            name = "MACH FORMULA", code = "GP-03", trait = "High-Speed Aero Wing", color = new Color(0.15f, 0.55f, 1.0f),
            speed = 7, accel = 4, handling = 5, weight = 5
        },
        new KartMachineDef {
            name = "MONSTER", code = "MON-04", trait = "Heavy Off-Road Tire", color = new Color(0.20f, 0.82f, 0.35f),
            speed = 6, accel = 3, handling = 3, weight = 7
        },
        new KartMachineDef {
            name = "CYBER GT", code = "CYB-05", trait = "Neon Drift Machine", color = new Color(0.88f, 0.22f, 0.95f),
            speed = 6, accel = 6, handling = 6, weight = 4
        },
        new KartMachineDef {
            name = "ROADSTER", code = "RET-06", trait = "Classic Smooth Turn", color = new Color(0.96f, 0.76f, 0.18f),
            speed = 5, accel = 6, handling = 7, weight = 4
        },
        new KartMachineDef {
            name = "MUSCLE V8", code = "MUS-07", trait = "Supercharged Power", color = new Color(0.48f, 0.25f, 0.95f),
            speed = 7, accel = 4, handling = 3, weight = 7
        },
        new KartMachineDef {
            name = "JET POD", code = "JET-08", trait = "Twin-Turbine Rocket", color = new Color(0.12f, 0.85f, 0.95f),
            speed = 7, accel = 6, handling = 4, weight = 4
        },
    };

    static KartCharacterDef[] runtimeKartCharacters;
    public static KartCharacterDef[] KartCharacters
    {
        get
        {
            if (runtimeKartCharacters == null)
            {
                var loaded = Resources.LoadAll<KartData>("Data/Karts");
                if (loaded != null && loaded.Length >= DefaultKartCharacters.Length)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runtimeKartCharacters = loaded.Select(k => k.ToDef()).ToArray();
                }
                else
                {
                    runtimeKartCharacters = DefaultKartCharacters;
                }
            }
            return runtimeKartCharacters;
        }
    }

    static KartMachineDef[] runtimeKartMachines;
    public static KartMachineDef[] KartMachines
    {
        get
        {
            if (runtimeKartMachines == null)
            {
                var loaded = Resources.LoadAll<KartMachineData>("Data/Machines");
                if (loaded != null && loaded.Length >= DefaultKartMachines.Length)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    runtimeKartMachines = loaded.Select(m => m.ToDef()).ToArray();
                }
                else
                {
                    runtimeKartMachines = DefaultKartMachines;
                }
            }
            return runtimeKartMachines;
        }
    }

    public static void GetCombinedStats(int driverIdx, int kartIdx, out int speed, out int accel, out int handling, out int weight)
    {
        var d = KartCharacters[Mathf.Clamp(driverIdx, 0, KartCharacters.Length - 1)];
        var m = KartMachines[Mathf.Clamp(kartIdx, 0, KartMachines.Length - 1)];
        speed    = Mathf.Clamp(d.speed + m.speed - 5, 1, 8);
        accel    = Mathf.Clamp(d.accel + m.accel - 5, 1, 8);
        handling = Mathf.Clamp(d.handling + m.handling - 5, 1, 8);
        weight   = Mathf.Clamp(d.weight + m.weight - 5, 1, 8);
    }

    public static int PackDriverKart(int driverIdx, int kartIdx)
    {
        int d = Mathf.Clamp(driverIdx, 0, 15) & 0x0F;
        int k = Mathf.Clamp(kartIdx, 0, 15) & 0x0F;
        return d | (k << 4);
    }

    public static void UnpackDriverKart(int packed, out int driverIdx, out int kartIdx)
    {
        driverIdx = Mathf.Clamp(packed & 0x0F, 0, KartCharacters.Length - 1);
        kartIdx = Mathf.Clamp((packed >> 4) & 0x0F, 0, KartMachines.Length - 1);
    }

    static readonly string[] AiNames = { "Blaze", "Nova", "Rex", "Kiki", "Bolt", "Mochi", "Taro" };
    static readonly Color[] AiColors =
    {
        new Color(0.15f, 0.4f, 1f), new Color(0.15f, 0.8f, 0.25f), new Color(1f, 0.85f, 0.1f), new Color(0.6f, 0.25f, 0.95f),
        new Color(1f, 0.5f, 0.05f), new Color(1f, 0.45f, 0.75f), new Color(0.1f, 0.85f, 0.9f),
    };
}
