using UnityEngine;

// カート・キャラクター定義の ScriptableObject
// ※ ScriptableObject はクラス名と同名のファイルに置く（でないと .asset のスクリプト参照が切れる）
[CreateAssetMenu(fileName = "NewKartData", menuName = "Turbo Circuit/Kart Data")]
public class KartData : ScriptableObject
{
    [Header("Profile")]
    public string characterName = "TURBO";
    public string driverName = "Player";
    public string trait = "Balanced All-Rounder";
    public Color signatureColor = new Color(1f, 0.22f, 0.22f);

    [Header("Performance (1-8, 5 is standard)")]
    [Range(1, 8)] public int speed = 5;
    [Range(1, 8)] public int accel = 5;
    [Range(1, 8)] public int handling = 5;
    [Range(1, 8)] public int weight = 5;

    public KartCharacterDef ToDef()
    {
        return new KartCharacterDef
        {
            name = characterName,
            driver = driverName,
            trait = trait,
            color = signatureColor,
            speed = speed,
            accel = accel,
            handling = handling,
            weight = weight
        };
    }
}
