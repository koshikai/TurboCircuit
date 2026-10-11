using UnityEngine;

// カートマシン（車体）定義の ScriptableObject
[CreateAssetMenu(fileName = "NewKartMachineData", menuName = "Turbo Circuit/Kart Machine Data")]
public class KartMachineData : ScriptableObject
{
    [Header("Machine Profile")]
    public string machineName = "STANDARD";
    public string codeName = "STD-01";
    public string trait = "Balanced All-Rounder";
    public Color bodyColor = new Color(0.95f, 0.25f, 0.25f);

    [Header("Performance (1-8, 5 is standard)")]
    [Range(1, 8)] public int speed = 5;
    [Range(1, 8)] public int accel = 5;
    [Range(1, 8)] public int handling = 5;
    [Range(1, 8)] public int weight = 5;

    public KartMachineDef ToDef()
    {
        return new KartMachineDef
        {
            name = machineName,
            code = codeName,
            trait = trait,
            color = bodyColor,
            speed = speed,
            accel = accel,
            handling = handling,
            weight = weight
        };
    }
}
