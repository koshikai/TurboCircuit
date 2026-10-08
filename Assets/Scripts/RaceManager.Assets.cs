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
    }

    void LoadUIAssets()
    {
        // フォントの読み込み：Lilita One (メインポップ) & Russo One (計器・数字)
        fontMain = Resources.Load<Font>("Fonts/LilitaOne-Regular");
        if (fontMain == null)
        {
            fontMain = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf")
                     ?? Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault();
        }

        fontNum = Resources.Load<Font>("Fonts/RussoOne-Regular") ?? fontMain;
        fontMono = Resources.Load<Font>("Fonts/ShareTechMono-Regular") ?? fontNum ?? fontMain;
        font = fontMain;

        string iconDir = Application.dataPath + "/Resources/Icons/";
        iconTurbo = LoadTexture("Icons/item_turbo", iconDir + "item_turbo.jpg");
        iconBanana = LoadTexture("Icons/item_banana", iconDir + "item_banana.jpg");
        iconMissile = LoadTexture("Icons/item_missile", iconDir + "item_missile.jpg");
        iconShield = LoadTexture("Icons/item_shield", iconDir + "item_shield.jpg");

        string uiDir = Application.dataPath + "/Resources/UI/";
        titleLogoTex = LoadTexture("UI/title_logo_pop", uiDir + "title_logo_pop.png") ?? LoadTexture("UI/title_logo", uiDir + "title_logo.png");
        for (int i = 0; i < trackBadgeTex.Length; i++)
            trackBadgeTex[i] = LoadTexture($"UI/badge_track_{i}_pop", uiDir + $"badge_track_{i}_pop.png") ?? LoadTexture($"UI/badge_track_{i}", uiDir + $"badge_track_{i}.png");
        if (trackBadgeTex[3] == null) trackBadgeTex[3] = TextureGen.CityBadge();

        cardPopTex = LoadTexture("UI/ui_card_pop", uiDir + "ui_card_pop.png");
        btnRaceTex = LoadTexture("UI/ui_btn_race", uiDir + "ui_btn_race.png");
        ribbonTrackTex = LoadTexture("UI/ui_ribbon_track", uiDir + "ui_ribbon_track.png");
        ribbonDriverTex = LoadTexture("UI/ui_ribbon_driver", uiDir + "ui_ribbon_driver.png");
        cardPanelTex = TextureGen.CardPanel(340, 400, new Color(0.04f, 0.08f, 0.18f, 0.88f), new Color(0.02f, 0.04f, 0.10f, 0.94f), new Color(0.2f, 0.7f, 1f, 0.85f), 2);

        // 現代的な角丸ガラスモーダル・フレーム・バッジ
        modalBgTex = TextureGen.ModernModalPanel(620, 520, new Color(0.08f, 0.12f, 0.24f, 0.93f), new Color(0.04f, 0.06f, 0.14f, 0.97f), new Color(0.0f, 0.85f, 1f, 0.9f), 3, 16f);
        itemSlotTex = TextureGen.ItemSlotFrame(114);
        minimapGlassTex = TextureGen.MinimapGlass(180);

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
        }
        return null;
    }
}
