using UnityEngine;

[CreateAssetMenu(menuName = "GraviSling/Module Data")]
public class ModuleData : ScriptableObject
{
    public string moduleName;
    public Sprite icon;

    public float chargeRateBonus;
    public float maxSpeedBonus;
    public float accelerationBonus;
}