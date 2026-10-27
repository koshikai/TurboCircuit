using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 実行時のマテリアル生成と UI テクスチャの読み込み
public partial class RaceManager
{
    void EnsureMaterials()
    {
        // シリアライズ済みの Skidmark.mat は加算合成(白く光る)なので、黒いタイヤ痕は常に実行時に作り直す。
        // Sprites/Default はアルファ透過で URP でも描画される(URP/Unlit は既定が不透明)
        var skidShader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                         ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (skidShader != null)
            skidmarkMaterial = new Material(skidShader) { mainTexture = TextureGen.TireMark() };
        var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        bool isUrp = litShader != null && litShader.name.Contains("Universal");

        if (headlightMaterial == null)
        {
            headlightMaterial = new Material(litShader);
            if (isUrp)
            {
                headlightMaterial.SetColor("_BaseColor", new Color(1f, 1f, 0.95f));
            }
            else
            {
                headlightMaterial.SetColor("_Color", new Color(1f, 1f, 0.95f));
            }
            headlightMaterial.EnableKeyword("_EMISSION");
            headlightMaterial.SetColor("_EmissionColor", new Color(1.4f, 1.4f, 1.1f));
        }
        if (taillightMaterial == null)
        {
            taillightMaterial = new Material(litShader);
            if (isUrp)
            {
                taillightMaterial.SetColor("_BaseColor", new Color(0.9f, 0.1f, 0.1f));
            }
            else
            {
                taillightMaterial.SetColor("_Color", new Color(0.9f, 0.1f, 0.1f));
            }
            taillightMaterial.EnableKeyword("_EMISSION");
            taillightMaterial.SetColor("_EmissionColor", new Color(0.8f, 0.05f, 0.05f));
        }
        if (bannerMaterial == null)
        {
            bannerMaterial = new Material(litShader);
            if (isUrp)
            {
                bannerMaterial.SetColor("_BaseColor", Color.white);
                bannerMaterial.SetFloat("_Smoothness", 0.4f);
            }
            else
            {
                bannerMaterial.SetColor("_Color", Color.white);
                bannerMaterial.SetFloat("_Glossiness", 0.4f);
            }
        }
        if (leavesMaterial != null)
        {
            leavesMaterial.SetFloat("_AlphaClip", 1f);
            leavesMaterial.SetFloat("_Cutoff", 0.3f);
            leavesMaterial.SetFloat("_Cull", 0f);
            leavesMaterial.SetFloat("_Mode", 1f);
            leavesMaterial.EnableKeyword("_ALPHATEST_ON");
            leavesMaterial.renderQueue = (int)RenderQueue.AlphaTest;
        }
    }

    void LoadUIAssets()
    {
        // フォントの読み込み：英語 (Lilita One) / 日本語ポップ (Mochiy Pop One & M PLUS Rounded 1c) / 計器・数字 (Russo One & Share Tech Mono)
        fontEnMain = Resources.Load<Font>("Fonts/LilitaOne-Regular");
        if (fontEnMain == null)
        {
            fontEnMain = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                       ?? Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();
        }

        fontJpPop = Resources.Load<Font>("Fonts/MochiyPopOne-Regular") ?? fontEnMain;
        fontJpRound = Resources.Load<Font>("Fonts/MPLUSRounded1c-ExtraBold") ?? fontJpPop ?? fontEnMain;
        fontNum = Resources.Load<Font>("Fonts/RussoOne-Regular") ?? fontEnMain;
        fontMono = Resources.Load<Font>("Fonts/ShareTechMono-Regular") ?? fontNum ?? fontEnMain;
        font = fontEnMain;
        ApplyLanguageFonts();

        string iconDir = Application.dataPath + "/Resources/Icons/";
        iconTurbo = LoadTexture("Icons/item_turbo_pop", iconDir + "item_turbo_pop.png") ?? LoadTexture("Icons/item_turbo", iconDir + "item_turbo.jpg");
        iconBanana = LoadTexture("Icons/item_banana_pop", iconDir + "item_banana_pop.png") ?? LoadTexture("Icons/item_banana", iconDir + "item_banana.jpg");
        iconMissile = LoadTexture("Icons/item_missile_pop", iconDir + "item_missile_pop.png") ?? LoadTexture("Icons/item_missile", iconDir + "item_missile.jpg");
        iconShield = LoadTexture("Icons/item_shield_pop", iconDir + "item_shield_pop.png") ?? LoadTexture("Icons/item_shield", iconDir + "item_shield.jpg");
        iconStar = LoadTexture("Icons/item_star_pop", iconDir + "item_star_pop.png") ?? iconShield;
        iconBomb = LoadTexture("Icons/item_bomb_pop", iconDir + "item_bomb_pop.png") ?? LoadTexture("Icons/item_bomb", iconDir + "item_bomb.jpg");
        iconThunder = LoadTexture("Icons/item_thunder_pop", iconDir + "item_thunder_pop.png");
        InitItemDefs();

        string uiDir = Application.dataPath + "/Resources/UI/";
        titleLogoTex = LoadTexture("UI/title_logo_pop", uiDir + "title_logo_pop.png") ?? LoadTexture("UI/title_logo", uiDir + "title_logo.png");
        int numCourses = Mathf.Max(Track.Courses.Length, 7);
        if (trackBadgeTex == null || trackBadgeTex.Length < numCourses)
            trackBadgeTex = new Texture2D[numCourses];
        for (int i = 0; i < trackBadgeTex.Length; i++)
            trackBadgeTex[i] = LoadTexture($"UI/badge_track_{i}_pop", uiDir + $"badge_track_{i}_pop.png") ?? LoadTexture($"UI/badge_track_{i}", uiDir + $"badge_track_{i}.png");
        if (trackBadgeTex.Length > 3 && trackBadgeTex[3] == null) trackBadgeTex[3] = TextureGen.CityBadge();
        if (trackBadgeTex.Length > 4 && trackBadgeTex[4] == null) trackBadgeTex[4] = TextureGen.HokkaidoBadge();
        if (trackBadgeTex.Length > 5 && trackBadgeTex[5] == null) trackBadgeTex[5] = TextureGen.OverpassBadge();
        if (trackBadgeTex.Length > 6 && trackBadgeTex[6] == null) trackBadgeTex[6] = TextureGen.CanyonBadge();

        cardPopTex = LoadTexture("UI/ui_card_pop", uiDir + "ui_card_pop.png");
        btnRaceTex = LoadTexture("UI/ui_btn_race", uiDir + "ui_btn_race.png");
        ribbonTrackTex = LoadTexture("UI/ui_ribbon_track", uiDir + "ui_ribbon_track.png");
        ribbonDriverTex = LoadTexture("UI/ui_ribbon_driver", uiDir + "ui_ribbon_driver.png");
        cardPanelTex = TextureGen.CardPanel(340, 400, new Color(0.04f, 0.08f, 0.18f, 0.88f), new Color(0.02f, 0.04f, 0.10f, 0.94f), new Color(0.2f, 0.7f, 1f, 0.85f), 2);

        // 現代的な角丸ガラスモーダル・フレーム・バッジ
        modalBgTex = TextureGen.ModernModalPanel(620, 520, new Color(0.08f, 0.12f, 0.24f, 0.93f), new Color(0.04f, 0.06f, 0.14f, 0.97f), new Color(0.0f, 0.85f, 1f, 0.9f), 3, 16f);
        itemSlotTex = TextureGen.ItemSlotFrame(114);
        minimapGlassTex = TextureGen.MinimapGlass(180);
        circleDotTex = TextureGen.CircleDot(32);
        speedoPlateTex = TextureGen.SlantedPlate(280, 74, new Color(0.05f, 0.08f, 0.16f, 0.88f), new Color(0.25f, 0.75f, 1f, 0.85f), 3, 0.18f);

        // 順位バッジ用スラントプレート
        rankPlateGold = TextureGen.SlantedPlate(150, 68, new Color(1f, 0.82f, 0.1f, 0.92f), new Color(1f, 1f, 0.7f, 1f), 3, 0.2f);
        rankPlateSilver = TextureGen.SlantedPlate(150, 68, new Color(0.75f, 0.80f, 0.88f, 0.92f), Color.white, 3, 0.2f);
        rankPlateBronze = TextureGen.SlantedPlate(150, 68, new Color(0.85f, 0.52f, 0.25f, 0.92f), new Color(1f, 0.85f, 0.6f, 1f), 3, 0.2f);
        rankPlateTex = TextureGen.SlantedPlate(150, 68, new Color(0.08f, 0.12f, 0.22f, 0.88f), new Color(0.3f, 0.75f, 1f, 0.8f), 3, 0.2f);
    }

    public static Texture2D LoadTexture(string resPath, string diskPath)
    {
        var tex = Resources.Load<Texture2D>(resPath);
        if (tex != null) return tex;
        if (System.IO.File.Exists(diskPath))
        {
            var bytes = System.IO.File.ReadAllBytes(diskPath);
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (t.LoadImage(bytes)) return t;
            if (Application.isPlaying) Destroy(t);
            else DestroyImmediate(t);
        }
        return null;
    }

    public void ApplyLanguageFonts()
    {
        if (Loc.IsJa)
        {
            fontMain = fontJpPop ?? fontJpRound ?? fontEnMain;
            fontSys = fontJpRound ?? fontJpPop ?? fontEnMain;
        }
        else
        {
            fontMain = fontEnMain ?? fontJpPop;
            fontSys = fontJpRound ?? fontMain;
        }
        sBig = null;
        styleCache.Clear();
    }

    void InitItemDefs()
    {
        ItemDefs.Clear();
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Turbo,
            NameEn = "TURBO", NameJp = "ターボ",
            AccentColor = new Color(1f, 0.55f, 0.1f),
            Icon = iconTurbo,
            WeightFunc = p => Mathf.Lerp(0.25f, 0.30f, p)
        });
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Banana,
            NameEn = "BANANA", NameJp = "バナナ",
            AccentColor = new Color(1f, 0.9f, 0.2f),
            Icon = iconBanana,
            WeightFunc = p => Mathf.Lerp(0.50f, 0.05f, p)
        });
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Missile,
            NameEn = "MISSILE", NameJp = "ミサイル",
            AccentColor = new Color(1f, 0.2f, 0.2f),
            Icon = iconMissile,
            WeightFunc = p => Mathf.Lerp(0.15f, 0.25f, p)
        });
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Star,
            NameEn = "STAR", NameJp = "スター",
            AccentColor = new Color(1.0f, 0.82f, 0.15f),
            Icon = iconStar,
            WeightFunc = p => Mathf.Lerp(0.00f, 0.18f, p)
        });
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Bomb,
            NameEn = "BOMB", NameJp = "ボム",
            AccentColor = new Color(1f, 0.35f, 0.15f),
            Icon = iconBomb,
            WeightFunc = p => Mathf.Lerp(0.10f, 0.22f, p)
        });
        ItemDefs.Add(new ItemDef
        {
            Type = ItemType.Thunder,
            NameEn = "THUNDER", NameJp = "サンダー",
            AccentColor = new Color(1f, 0.95f, 0.2f),
            Icon = iconThunder,
            // 1〜4位 (p < 0.65) では出ない。6〜8位 (p >= 0.65) で出現！
            WeightFunc = p => (thunderCooldown > 0 || p < 0.65f) ? 0f : Mathf.Lerp(0f, 0.20f, (p - 0.65f) / 0.35f)
        });
    }
}
