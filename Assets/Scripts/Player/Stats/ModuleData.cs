using UnityEngine;

[CreateAssetMenu(menuName = "GraviSling/Module Data")]
public class ModuleData : ScriptableObject
{
    public string moduleName;
    public string moduleType;
    public int moduleTier;
    public Sprite icon;

    //  Orbit
    public float chargeRateBonus;
    public float chargeRateBonus_Percent;
    public float baseLaunchSpeedBonus;
    public float baseLaunchSpeedBonus_Percent;

    // Player Movement
    public float maxSpeedBonus;
    public float maxSpeedBonus_Percent;
    public float accelerationBonus;
    public float accelerationBonus_Percent;
    public float boostAccelAddBonus;
    public float boostAccelAddBonus_Percent;

    // Boosting
    public float boostMaxBonus;
    public float boostMaxBonus_Percent;
    public float capacityBonus;
    public float capacityBonus_Percent;
    public float drainPerSecondBonus;
    public float drainPerSecondBonus_Percent;
    public float regenPerSecondBonus;
    public float regenPerSecondBonus_Percent;

}