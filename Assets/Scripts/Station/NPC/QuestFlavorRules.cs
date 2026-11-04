public static class QuestFlavorRules
{
    public static int GetMaxHeat(int rankIndex)
    {
        if (rankIndex <= 0) return 0;
        if (rankIndex == 1) return 2;

        return 5;
    }

    public static int GetCargoUnlockRank(CargoEffectType effect)
    {
        switch (effect)
        {
            case CargoEffectType.None:
                return 0;

            case CargoEffectType.UltraLight:
            case CargoEffectType.SuperHeavy:
            case CargoEffectType.OverchargedBoost:
                return 1;

            // Future effects stay locked until explicitly configured.
            default:
                return int.MaxValue;
        }
    }

    public static bool IsAllowed(QuestFlavor flavor, int rankIndex)
    {
        rankIndex = System.Math.Max(0, rankIndex);

        return rankIndex >= flavor.minimumRank
            && flavor.heatLevel >= 0
            && flavor.heatLevel <= GetMaxHeat(rankIndex)
            && rankIndex >= GetCargoUnlockRank(flavor.cargoEffectType);
    }
}