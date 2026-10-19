using UnityEngine;

// コース定義の ScriptableObject
[CreateAssetMenu(fileName = "NewCourseData", menuName = "Turbo Circuit/Course Data")]
public class CourseData : ScriptableObject
{
    [Header("Information")]
    public string courseName = "TURBO CIRCUIT";
    [TextArea(2, 4)]
    public string description = "Grand Prix circuit with sweeping hills and flyover bridge.";

    [Header("Geometry")]
    public Vector3[] controlPoints;
    public JumpRampDef[] ramps;
    public TunnelDef[] tunnels;
    public BranchDef[] branches;

    [Header("Environment & Lighting")]
    public Color skyTint = new Color(0.45f, 0.6f, 1f);
    public Color groundColor = new Color(0.45f, 0.55f, 0.4f);
    public Color sunColor = new Color(1f, 0.96f, 0.88f);
    public float sunIntensity = 1.25f;
    public Vector3 sunRotation = new Vector3(48f, -35f, 0f);
    public Color fogColor = new Color(0.72f, 0.82f, 0.95f);
    public float fogDistance = 900f;

    [Header("Materials & Colors")]
    public Color roadColor = new Color(0.28f, 0.28f, 0.29f);
    public Color curbColorA = new Color(0.9f, 0.1f, 0.1f);
    public Color curbColorB = Color.white;
    public Color wallColorA = new Color(0.15f, 0.35f, 0.95f);
    public Color wallColorB = Color.white;
    public Color shoulderColor = new Color(0.85f, 0.75f, 0.55f);

    [Header("Theme (0=Circuit, 1=Desert, 2=Snow, 3=City, 4=Campus, 5=Overpass)")]
    [Range(0, 5)]
    public int sceneryTheme = 0;

    public TrackDef ToDef()
    {
        return new TrackDef
        {
            Name = courseName,
            Description = description,
            Control = controlPoints,
            Ramps = ramps,
            Tunnels = tunnels,
            Branches = branches,
            SkyTint = skyTint,
            GroundColor = groundColor,
            SunColor = sunColor,
            SunIntensity = sunIntensity,
            SunRotation = sunRotation,
            FogColor = fogColor,
            FogDistance = fogDistance,
            RoadColor = roadColor,
            CurbColorA = curbColorA,
            CurbColorB = curbColorB,
            WallColorA = wallColorA,
            WallColorB = wallColorB,
            ShoulderColor = shoulderColor,
            SceneryTheme = sceneryTheme
        };
    }
}
