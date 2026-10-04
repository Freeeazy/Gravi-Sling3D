using System;

public struct QuestFlavor
{
    public string questTitle;
    public string shortDescription;
    public string fullDescription;
    public string deliveryItemName;

    public CargoEffectType cargoEffectType;
}

public static class QuestFlavorUtil
{
    public static QuestFlavor Generate(NPCData npc, NPCQuestManager.QuestOffer offer, string destinationName, int seed)
    {
        var rng = new System.Random(seed ^ npc.npcId);

        NPCProfile profile = npc.profile.npcId == npc.npcId ? npc.profile : NPCProfileUtil.GenerateProfile(npc.npcId);

        string npcName = string.IsNullOrWhiteSpace(npc.displayName) ? "This client" : npc.displayName;

        if (Has(profile, NPCPersonalityTrait.Family) &&
            (profile.ageBand == NPCAgeBand.Older ||
             profile.ageBand == NPCAgeBand.Elder ||
             profile.primaryFaction == NPCFaction.Friendly))
        {
            return Make(
                "Grandkid Supply Run",
                "New Mag-Boots",
                $"{npcName} says their grandkid wore through another pair of station shoes.",
                $"Deliver new mag-boots to {destinationName}. {npcName} insists the kid is \"one bad puddle away from walking in taped-up scrap.\""
            );
        }

        if (Has(profile, NPCPersonalityTrait.Petty) &&
            Has(profile, NPCPersonalityTrait.Romantic))
        {
            return Make(
                "Absolutely Normal Chocolates",
                "Suspicious Chocolates",
                $"{npcName} has apology chocolates for an ex. The grin is worrying.",
                $"Deliver suspicious chocolates to {destinationName}. {npcName} says they are for an ex and refuses to explain why the box has medical warnings."
            );
        }

        // -------------------------------------------------
        // WEIRD CARGO
        // -------------------------------------------------

        if (Has(profile, NPCPersonalityTrait.Weird))
        {
            bool ultraLight = rng.Next(0, 2) == 0;

            if (ultraLight)
            {
                return Make(
                    "Lighter Than Nothing",
                    "Null-Gravity Core ",
                    $"{npcName} hands you a Null-Gravity Core that feels suspiciously close to weighing nothing at all.",
                    $"Deliver the Null-Gravity Core to {destinationName}. {npcName} warns that its mass-canceling effect may interfere with your ship's handling, though the extra responsiveness might be worth enjoying while it lasts.",
                    CargoEffectType.UltraLight
                );
            }

            return Make(
                "Deadweight Delivery",
                "Artifical Gravity Core",
                $"{npcName} hands you an Artificial Gravity Core dense enough to make your ship noticeably regret the decision.",
                $"Deliver the Artificial Gravity Core to {destinationName}. Its localized gravity field adds considerable effective mass to your ship, so {npcName} recommends giving yourself a little extra room before attempting anything heroic.",
                CargoEffectType.SuperHeavy
            );
        }

        if (profile.primaryFaction == NPCFaction.Mechanic)
        {
            return Make(
                "Emergency Coupler Run",
                "Engine Couplers",
                $"{npcName} needs repair parts moved before anyone notices the smoke.",
                $"Deliver engine couplers to {destinationName}. {npcName} claims everything is under control, which is usually what people say right before it is not."
            );
        }

        if (profile.primaryFaction == NPCFaction.Shady)
        {
            return Make(
                "No Questions Crate",
                "Sealed Contraband",
                $"{npcName} wants a crate moved quietly. Very quietly.",
                $"Deliver sealed contraband to {destinationName}. {npcName} says the crate is legal in at least one jurisdiction and would prefer you stop asking which one."
            );
        }

        if (profile.primaryFaction == NPCFaction.Scientist)
        {
            return Make(
                "Prototype Handling",
                "Volatile Prototype",
                $"{npcName} needs a prototype delivered with most of reality intact.",
                $"Deliver a volatile prototype to {destinationName}. {npcName} says mild humming is normal. Screaming is apparently also normal, but less ideal."
            );
        }

        if (profile.primaryFaction == NPCFaction.Friendly)
        {
            return Make(
                "Community Favor",
                "Care Package",
                $"{npcName} packed a care bundle for someone who needs it.",
                $"Deliver a care package to {destinationName}. {npcName} already paid extra because they \"believe in tipping before the disaster.\""
            );
        }

        return Make(
            Pick(rng, "Station Errand", "Courier Contract", "Package Run"),
            Pick(rng, "Sealed Crate", "Priority Package", "Cargo Bundle"),
            $"{npcName} needs this delivered to {destinationName}.",
            $"Deliver the package to {destinationName}. {npcName} says it matters, which is more information than most clients give."
        );
    }

    private static QuestFlavor Make(
        string title,
        string item,
        string shortDesc,
        string fullDesc,
        CargoEffectType cargoEffectType = CargoEffectType.None)
    {
        return new QuestFlavor
        {
            questTitle = "- " + title,
            deliveryItemName = item,
            shortDescription = shortDesc,
            fullDescription = fullDesc,
            cargoEffectType = cargoEffectType
        };
    }

    private static bool Has(NPCProfile profile, NPCPersonalityTrait trait)
    {
        if (profile.personalityTraits == null)
            return false;

        for (int i = 0; i < profile.personalityTraits.Length; i++)
        {
            if (profile.personalityTraits[i] == trait)
                return true;
        }

        return false;
    }

    private static string Pick(System.Random rng, params string[] values)
    {
        return values == null || values.Length == 0 ? "" : values[rng.Next(values.Length)];
    }
}