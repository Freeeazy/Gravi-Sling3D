using System;

public struct QuestFlavor
{
    public string questTitle;
    public string shortDescription;
    public string fullDescription;
    public string deliveryItemName;
}

public static class QuestFlavorUtil
{
    public static QuestFlavor Generate(NPCData npc, NPCQuestManager.QuestOffer offer, string destinationName, int seed)
    {
        var rng = new System.Random(seed ^ npc.npcId);
        NPCProfile profile = npc.profile.npcId == npc.npcId ? npc.profile : NPCProfileUtil.GenerateProfile(npc.npcId);
        string npcName = string.IsNullOrWhiteSpace(npc.displayName) ? "This client" : npc.displayName;

        if (Has(profile, NPCPersonalityTrait.Family) && (profile.ageBand == NPCAgeBand.Older || profile.ageBand == NPCAgeBand.Elder || profile.primaryFaction == NPCFaction.Friendly))
            return Make("Grandkid Supply Run", "new mag-boots", $"{npcName} says their grandkid wore through another pair of station shoes.", $"Deliver new mag-boots to {destinationName}. {npcName} insists the kid is \"one bad puddle away from walking in taped-up scrap.\"");

        if (Has(profile, NPCPersonalityTrait.Petty) && Has(profile, NPCPersonalityTrait.Romantic))
            return Make("Absolutely Normal Chocolates", "suspicious chocolates", $"{npcName} has apology chocolates for an ex. The grin is worrying.", $"Deliver suspicious chocolates to {destinationName}. {npcName} says they are for an ex and refuses to explain why the box has medical warnings.");

        if (profile.primaryFaction == NPCFaction.Mechanic)
            return Make("Emergency Coupler Run", "engine couplers", $"{npcName} needs repair parts moved before anyone notices the smoke.", $"Deliver engine couplers to {destinationName}. {npcName} claims everything is under control, which is usually what people say right before it is not.");

        if (profile.primaryFaction == NPCFaction.Shady)
            return Make("No Questions Crate", "sealed contraband", $"{npcName} wants a crate moved quietly. Very quietly.", $"Deliver sealed contraband to {destinationName}. {npcName} says the crate is legal in at least one jurisdiction and would prefer you stop asking which one.");

        if (profile.primaryFaction == NPCFaction.Scientist)
            return Make("Prototype Handling", "volatile prototype", $"{npcName} needs a prototype delivered with most of reality intact.", $"Deliver a volatile prototype to {destinationName}. {npcName} says mild humming is normal. Screaming is apparently also normal, but less ideal.");

        if (profile.primaryFaction == NPCFaction.Friendly)
            return Make("Community Favor", "care package", $"{npcName} packed a care bundle for someone who needs it.", $"Deliver a care package to {destinationName}. {npcName} already paid extra because they \"believe in tipping before the disaster.\"");

        return Make(
            Pick(rng, "Station Errand", "Courier Contract", "Package Run"),
            Pick(rng, "sealed crate", "priority package", "cargo bundle"),
            $"{npcName} needs this delivered to {destinationName}.",
            $"Deliver the package to {destinationName}. {npcName} says it matters, which is more information than most clients give."
        );
    }

    private static QuestFlavor Make(string title, string item, string shortDesc, string fullDesc)
    {
        return new QuestFlavor { questTitle = title, deliveryItemName = item, shortDescription = shortDesc, fullDescription = fullDesc };
    }

    private static bool Has(NPCProfile profile, NPCPersonalityTrait trait)
    {
        if (profile.personalityTraits == null) return false;
        for (int i = 0; i < profile.personalityTraits.Length; i++)
            if (profile.personalityTraits[i] == trait) return true;
        return false;
    }

    private static string Pick(System.Random rng, params string[] values)
    {
        return values == null || values.Length == 0 ? "" : values[rng.Next(values.Length)];
    }
}