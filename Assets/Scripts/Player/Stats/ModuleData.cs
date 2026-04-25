using UnityEngine;

[CreateAssetMenu(menuName = "GraviSling/Module Data")]
public class ModuleData : ScriptableObject
{
    public string moduleName;
    public Sprite icon;

    //  Orbit
    public float chargeRateBonus;

    // Player Movement
    public float maxSpeedBonus;
    public float accelerationBonus;
    public float boostAccelAddBonus;

    // Boosting
    public float boostMaxBonus;
    public float capacityBonus;
    public float drainPerSecondBonus;
    public float regenPerSecondBonus;

}