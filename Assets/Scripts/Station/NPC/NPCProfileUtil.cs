using System;
using System.Collections.Generic;
using UnityEngine;

public enum NPCFaction
{
    None,
    Merchant,
    Elite,
    Mechanic,
    Scrapper,
    Archivist,
    Scientist,
    Veteran,
    Mercenary,
    Friendly,
    Shady
}

public enum NPCAgeBand
{
    Young,
    Adult,
    Older,
    Elder
}

public enum NPCPersonalityTrait
{
    Family,
    Petty,
    Romantic,
    Nervous,
    Greedy,
    Proud,
    Paranoid,
    Sentimental,
    Bitter,
    Generous,
    Desperate,
    Weird,
    Reckless,
    Lonely
}

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
        NPCFaction.Merchant,
        NPCFaction.Elite,
        NPCFaction.Mechanic,
        NPCFaction.Scrapper,
        NPCFaction.Archivist,
        NPCFaction.Scientist,
        NPCFaction.Veteran,
        NPCFaction.Mercenary,
        NPCFaction.Friendly,
        NPCFaction.Shady
    };

    private static readonly NPCPersonalityTrait[] traits =
    {
        NPCPersonalityTrait.Family,
        NPCPersonalityTrait.Petty,
        NPCPersonalityTrait.Romantic,
        NPCPersonalityTrait.Nervous,
        NPCPersonalityTrait.Greedy,
        NPCPersonalityTrait.Proud,
        NPCPersonalityTrait.Paranoid,
        NPCPersonalityTrait.Sentimental,
        NPCPersonalityTrait.Bitter,
        NPCPersonalityTrait.Generous,
        NPCPersonalityTrait.Desperate,
        NPCPersonalityTrait.Weird,
        NPCPersonalityTrait.Reckless,
        NPCPersonalityTrait.Lonely
    };

    // -------------------------------------------------
    // SINGLE NPC GENERATION
    // -------------------------------------------------

    public static NPCProfile GenerateProfile(int npcId)
    {
        var rng = new System.Random(
            npcId ^ unchecked((int)0xA53C91F1)
        );

        NPCFaction primary =
            factions[rng.Next(factions.Length)];

        NPCFaction secondary = NPCFaction.None;

        // Roughly 45% of NPCs have a second faction.
        if (rng.NextDouble() < 0.45)
        {
            secondary = PickDifferentFaction(rng, primary);
        }

        NPCAgeBand age = RollAge(rng);

        // Most people have 2-3 traits.
        // A smaller number get 4 for extra character.
        int traitRoll = rng.Next(100);

        int traitCount =
            traitRoll < 15 ? 4 :
            traitRoll < 65 ? 3 :
            2;

        var pickedTraits =
            new HashSet<NPCPersonalityTrait>();

        while (pickedTraits.Count < traitCount)
        {
            pickedTraits.Add(
                traits[rng.Next(traits.Length)]
            );
        }

        return new NPCProfile
        {
            npcId = npcId,
            primaryFaction = primary,
            secondaryFaction = secondary,
            ageBand = age,
            personalityTraits =
                new List<NPCPersonalityTrait>(pickedTraits).ToArray()
        };
    }

    // -------------------------------------------------
    // STATION POPULATION GENERATION
    // -------------------------------------------------

    /// <summary>
    /// Generates NPCs for a station while deliberately spreading
    /// factions, ages and traits before allowing heavy repetition.
    ///
    /// stationSeed should be stable for the station/world.
    /// </summary>
    public static NPCProfile[] GenerateStationPopulation(
        int stationSeed,
        int startingNpcId,
        int npcCount)
    {
        var rng = new System.Random(
            stationSeed ^ unchecked((int)0x72D91B43)
        );

        NPCProfile[] population =
            new NPCProfile[npcCount];

        // Shuffled faction bag.
        // This means a station tries to see every faction once
        // before heavily repeating them.
        List<NPCFaction> factionBag =
            new List<NPCFaction>(factions);

        Shuffle(factionBag, rng);

        // Age bag intentionally includes Adult more often,
        // while still guaranteeing age variation.
        List<NPCAgeBand> ageBag = new List<NPCAgeBand>
        {
            NPCAgeBand.Young,
            NPCAgeBand.Adult,
            NPCAgeBand.Adult,
            NPCAgeBand.Adult,
            NPCAgeBand.Older,
            NPCAgeBand.Older,
            NPCAgeBand.Elder
        };

        Shuffle(ageBag, rng);

        // Trait bag helps prevent every station from rolling
        // the same handful of traits repeatedly.
        List<NPCPersonalityTrait> traitBag =
            new List<NPCPersonalityTrait>(traits);

        Shuffle(traitBag, rng);

        int factionIndex = 0;
        int ageIndex = 0;
        int traitIndex = 0;

        for (int i = 0; i < npcCount; i++)
        {
            int npcId = startingNpcId + i;

            // -------------------------------------------------
            // PRIMARY FACTION
            // -------------------------------------------------

            if (factionIndex >= factionBag.Count)
            {
                Shuffle(factionBag, rng);
                factionIndex = 0;
            }

            NPCFaction primary =
                factionBag[factionIndex++];

            // -------------------------------------------------
            // SECONDARY FACTION
            // -------------------------------------------------

            NPCFaction secondary = NPCFaction.None;

            if (rng.NextDouble() < 0.45)
            {
                secondary =
                    PickDifferentFaction(rng, primary);
            }

            // -------------------------------------------------
            // AGE
            // -------------------------------------------------

            if (ageIndex >= ageBag.Count)
            {
                Shuffle(ageBag, rng);
                ageIndex = 0;
            }

            NPCAgeBand age =
                ageBag[ageIndex++];

            // -------------------------------------------------
            // PERSONALITY TRAITS
            // -------------------------------------------------

            int traitRoll = rng.Next(100);

            int traitCount =
                traitRoll < 15 ? 4 :
                traitRoll < 65 ? 3 :
                2;

            HashSet<NPCPersonalityTrait> picked =
                new HashSet<NPCPersonalityTrait>();

            while (picked.Count < traitCount)
            {
                if (traitIndex >= traitBag.Count)
                {
                    Shuffle(traitBag, rng);
                    traitIndex = 0;
                }

                picked.Add(
                    traitBag[traitIndex++]
                );
            }

            population[i] = new NPCProfile
            {
                npcId = npcId,
                primaryFaction = primary,
                secondaryFaction = secondary,
                ageBand = age,
                personalityTraits =
                    new List<NPCPersonalityTrait>(picked).ToArray()
            };
        }

        return population;
    }

    // -------------------------------------------------
    // AGE
    // -------------------------------------------------

    private static NPCAgeBand RollAge(System.Random rng)
    {
        int ageRoll = rng.Next(100);

        if (ageRoll < 15)
            return NPCAgeBand.Young;

        if (ageRoll < 68)
            return NPCAgeBand.Adult;

        if (ageRoll < 91)
            return NPCAgeBand.Older;

        return NPCAgeBand.Elder;
    }

    // -------------------------------------------------
    // FACTIONS
    // -------------------------------------------------

    private static NPCFaction PickDifferentFaction(
        System.Random rng,
        NPCFaction excludedFaction)
    {
        NPCFaction result;

        do
        {
            result =
                factions[rng.Next(factions.Length)];
        }
        while (result == excludedFaction);

        return result;
    }

    // -------------------------------------------------
    // SHUFFLE
    // -------------------------------------------------

    private static void Shuffle<T>(
        List<T> list,
        System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);

            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // -------------------------------------------------
    // UI TAGS
    // -------------------------------------------------

    public static NPCUtil.NPCTag[] GetVisibleTags(
        NPCProfile profile)
    {
        return profile.secondaryFaction == NPCFaction.None
            ? new[]
            {
                ToTag(profile.primaryFaction)
            }
            : new[]
            {
                ToTag(profile.primaryFaction),
                ToTag(profile.secondaryFaction)
            };
    }

    private static NPCUtil.NPCTag ToTag(
        NPCFaction faction)
    {
        return new NPCUtil.NPCTag(
            GetFactionLabel(faction),
            GetFactionColor(faction)
        );
    }

    public static string GetFactionLabel(
        NPCFaction faction)
    {
        return faction.ToString();
    }

    public static Color GetFactionColor(
        NPCFaction faction)
    {
        switch (faction)
        {
            case NPCFaction.Merchant:
                return new Color32(80, 180, 255, 255);

            case NPCFaction.Elite:
                return new Color32(255, 220, 140, 255);

            case NPCFaction.Mechanic:
                return new Color32(170, 170, 170, 255);

            case NPCFaction.Scrapper:
                return new Color32(180, 130, 80, 255);

            case NPCFaction.Archivist:
                return new Color32(255, 230, 100, 255);

            case NPCFaction.Scientist:
                return new Color32(100, 220, 255, 255);

            case NPCFaction.Veteran:
                return new Color32(255, 175, 80, 255);

            case NPCFaction.Mercenary:
                return new Color32(255, 90, 90, 255);

            case NPCFaction.Friendly:
                return new Color32(90, 220, 140, 255);

            case NPCFaction.Shady:
                return new Color32(190, 80, 255, 255);

            default:
                return Color.white;
        }
    }
}