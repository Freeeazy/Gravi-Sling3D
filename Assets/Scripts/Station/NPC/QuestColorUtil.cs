
using UnityEngine;

public static class QuestColorUtil
{
    public static Color GetCargoColor(
        CargoEffectType cargoEffectType,
        NPCFaction faction)
    {
        switch (cargoEffectType)
        {
            case CargoEffectType.UltraLight:
                return new Color(0.35f, 0.90f, 1.00f, 1f);

            case CargoEffectType.SuperHeavy:
                return new Color(1.00f, 0.55f, 0.25f, 1f);

            case CargoEffectType.OverchargedBoost:
                return new Color(0.33f, 1.00f, 0.52f, 1f);

            default:
                return NPCProfileUtil.GetFactionColor(faction);
        }
    }

    public static string GetHex(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    public static string Colorize(string text, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return $"<color={GetHex(color)}>{text}</color>";
    }

    public static string ColorizeItem(string itemName, NPCFaction faction, CargoEffectType cargoEffectType)
    {
        return Colorize(itemName, GetCargoColor(cargoEffectType, faction));
    }

    public static string ColorizeDescription(string description, string itemName, Color color)
    {
        if (string.IsNullOrEmpty(description) || string.IsNullOrEmpty(itemName))
            return description;

        return description.Replace(itemName, Colorize(itemName, color));
    }
}
