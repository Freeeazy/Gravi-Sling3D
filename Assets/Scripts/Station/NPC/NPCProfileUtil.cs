using System;
using System.Collections.Generic;
using UnityEngine;

public enum NPCFaction { None, Merchant, Elite, Mechanic, Scrapper, Archivist, Scientist, Veteran, Mercenary, Friendly, Shady }
public enum NPCAgeBand { Young, Adult, Older, Elder }
public enum NPCPersonalityTrait { Family, Petty, Romantic, Nervous, Greedy, Proud, Paranoid, Sentimental, Bitter, Generous, Desperate, Weird, Reckless, Lonely }

public struct NPCProfile
{
    public int npcId;
    public NPCFaction primaryFaction;
    public NPCFaction secondaryFaction;
    public NPCAgeBand ageBand;
    public NPCPersonalityTrait[] personalityTraits;
}

public static class NPCProfileUtil
{
    private static readonly NPCFaction[] factions =
    {
        NPCFaction.Merchant, NPCFaction.Elite, NPCFaction.Mechanic, NPCFaction.Scrapper, NPCFaction.Archivist,
        NPCFaction.Scientist, NPCFaction.Veteran, NPCFaction.Mercenary, NPCFaction.Friendly, NPCFaction.Shady
    };

    private static readonly NPCPersonalityTrait[] traits =
    {
        NPCPersonalityTrait.Family, NPCPersonalityTrait.Petty, NPCPersonalityTrait.Romantic, NPCPersonalityTrait.Nervous,
        NPCPersonalityTrait.Greedy, NPCPersonalityTrait.Proud, NPCPersonalityTrait.Paranoid, NPCPersonalityTrait.Sentimental,
        NPCPersonalityTrait.Bitter, NPCPersonalityTrait.Generous, NPCPersonalityTrait.Desperate, NPCPersonalityTrait.Weird,
        NPCPersonalityTrait.Reckless, NPCPersonalityTrait.Lonely
    };

    public static NPCProfile GenerateProfile(int npcId)
    {
        var rng = new System.Random(npcId ^ unchecked((int)0xA53C91F1));

        NPCFaction primary = factions[rng.Next(factions.Length)];
        NPCFaction secondary = rng.NextDouble() < 0.45 ? factions[rng.Next(factions.Length)] : NPCFaction.None;
        if (secondary == primary) secondary = NPCFaction.None;

        int ageRoll = rng.Next(100);
        NPCAgeBand age = ageRoll < 15 ? NPCAgeBand.Young : ageRoll < 70 ? NPCAgeBand.Adult : ageRoll < 92 ? NPCAgeBand.Older : NPCAgeBand.Elder;

        int traitCount = rng.Next(2, 4);
        var picked = new HashSet<NPCPersonalityTrait>();
        while (picked.Count < traitCount)
            picked.Add(traits[rng.Next(traits.Length)]);

        return new NPCProfile
        {
            npcId = npcId,
            primaryFaction = primary,
            secondaryFaction = secondary,
            ageBand = age,
            personalityTraits = new List<NPCPersonalityTrait>(picked).ToArray()
        };
    }

    public static NPCUtil.NPCTag[] GetVisibleTags(NPCProfile profile)
    {
        return profile.secondaryFaction == NPCFaction.None
            ? new[] { ToTag(profile.primaryFaction) }
            : new[] { ToTag(profile.primaryFaction), ToTag(profile.secondaryFaction) };
    }

    private static NPCUtil.NPCTag ToTag(NPCFaction faction)
    {
        return new NPCUtil.NPCTag(GetFactionLabel(faction), GetFactionColor(faction));
    }

    public static string GetFactionLabel(NPCFaction faction) => faction.ToString();

    public static Color GetFactionColor(NPCFaction faction)
    {
        switch (faction)
        {
            case NPCFaction.Merchant: return new Color32(80, 180, 255, 255);
            case NPCFaction.Elite: return new Color32(255, 220, 140, 255);
            case NPCFaction.Mechanic: return new Color32(170, 170, 170, 255);
            case NPCFaction.Scrapper: return new Color32(180, 130, 80, 255);
            case NPCFaction.Archivist: return new Color32(255, 230, 100, 255);
            case NPCFaction.Scientist: return new Color32(100, 220, 255, 255);
            case NPCFaction.Veteran: return new Color32(255, 175, 80, 255);
            case NPCFaction.Mercenary: return new Color32(255, 90, 90, 255);
            case NPCFaction.Friendly: return new Color32(90, 220, 140, 255);
            case NPCFaction.Shady: return new Color32(190, 80, 255, 255);
            default: return Color.white;
        }
    }
}