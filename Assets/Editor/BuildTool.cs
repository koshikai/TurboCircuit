using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// マテリアル・シーンの自動生成とビルド。
// エディタのメニュー「Turbo Circuit」からも、コマンドラインの -executeMethod からも呼べる。
public static class BuildTool
{
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = SceneDir + "/Race.unity";
    const string MatDir = "Assets/Materials";

    [MenuItem("Turbo Circuit/Setup Scene")]
    public static void Setup()
    {
        Directory.CreateDirectory(SceneDir);
        Directory.CreateDirectory(MatDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sky = LoadOrCreate("Sky", Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.05f);
        sky.SetFloat("_AtmosphereThickness", 0.8f);
        sky.SetColor("_SkyTint", new Color(0.45f, 0.6f, 1f));
        sky.SetColor("_GroundColor", new Color(0.45f, 0.55f, 0.4f));
        sky.SetFloat("_Exposure", 1.3f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.9f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.25f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 250f;
        RenderSettings.fogEndDistance = 900f;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.95f);

        var rm = new GameObject("Race").AddComponent<RaceManager>();
        rm.roadMaterial = Lit("Road", Color.white, 0.25f);
        rm.curbMaterial = Lit("Curb", Color.white, 0.3f);
        rm.wallMaterial = Lit("Wall", Color.white, 0.4f);
        rm.grassMaterial = Lit("Grass", Color.white, 0.05f);
        rm.chromeMaterial = Lit("Chrome", new Color(0.8f, 0.82f, 0.85f), 0.85f, 0.9f);
        rm.standMaterial = Lit("Stand", new Color(0.55f, 0.57f, 0.62f), 0.2f);
        rm.kartPaintMaterial = Lit("KartPaint", Color.white, 0.8f, 0.25f);
        rm.tireMaterial = Lit("Tire", new Color(0.08f, 0.08f, 0.09f), 0.3f);
        rm.visorMaterial = Lit("Visor", new Color(0.05f, 0.08f, 0.15f), 0.95f, 0.5f);
        rm.skinMaterial = Lit("Glove", new Color(0.95f, 0.95f, 0.95f), 0.2f);
        rm.trunkMaterial = Lit("Trunk", new Color(0.4f, 0.26f, 0.15f), 0.1f);
        rm.leavesMaterial = Lit("Leaves", new Color(0.2f, 0.5f, 0.18f), 0.15f);
        rm.mountainMaterial = Lit("Mountain", new Color(0.42f, 0.5f, 0.42f), 0.05f);
        rm.snowMaterial = Lit("Snow", new Color(0.95f, 0.97f, 1f), 0.3f);
        rm.boostPadMaterial = Lit("BoostPad", Color.white, 0.6f, 0f, new Color(0.9f, 0.6f, 0.2f));
        rm.itemBoxMaterial = Lit("ItemBox", Color.white, 0.9f, 0.1f, new Color(0.5f, 0.5f, 0.5f));
        rm.bananaMaterial = Lit("Banana", new Color(1f, 0.88f, 0.15f), 0.5f, 0f, new Color(0.25f, 0.2f, 0f));
        rm.missileMaterial = Lit("Missile", new Color(0.9f, 0.1f, 0.1f), 0.8f, 0.3f, new Color(0.4f, 0.02f, 0.02f));
        rm.glowMaterial = Glow("Glow");
        rm.skidmarkMaterial = Glow("Skidmark");
        rm.headlightMaterial = Lit("Headlight", new Color(1f, 1f, 0.95f), 0.9f, 0.1f, new Color(1.4f, 1.4f, 1.1f));
        rm.taillightMaterial = Lit("Taillight", new Color(0.9f, 0.1f, 0.1f), 0.9f, 0.1f, new Color(0.8f, 0.05f, 0.05f));
        rm.bannerMaterial = Lit("Banner", Color.white, 0.4f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Turbo Circuit";
        PlayerSettings.companyName = "kaito";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildTool] Scene setup complete: " + ScenePath);
    }

    [MenuItem("Turbo Circuit/Build Windows")]
    public static void BuildWindows()
    {
        Setup();
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/Windows/TurboCircuit.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[BuildTool] Build result: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static Material Lit(string name, Color albedo, float smoothness, float metallic = 0f, Color? emission = null)
    {
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        var shader = urpShader != null ? urpShader : Shader.Find("Standard");
        var mat = LoadOrCreate(name, shader);

        if (urpShader != null && mat.shader == urpShader)
        {
            mat.SetColor("_BaseColor", albedo);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            else mat.DisableKeyword("_EMISSION");
        }
        else
        {
            mat.SetColor("_Color", albedo);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            else mat.DisableKeyword("_EMISSION");
        }
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material Glow(string name)
    {
        var shader = Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
        var mat = LoadOrCreate(name, shader);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreate(string name, Shader shader)
    {
        string path = MatDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }
}
