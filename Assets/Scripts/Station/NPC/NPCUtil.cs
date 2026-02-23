using System;
using System.Collections.Generic;
using UnityEngine;

public struct NPCData
{
    public int npcId;
    public string displayName;

    public NPCData(int id, string name)
    {
        npcId = id;
        displayName = name;
    }
}

public static class NPCUtil
{
    // Keep these small + readable. Add more later.
    static readonly string[] firstNames =
    {
        "Kai", "Mara", "Juno", "Rin", "Sol", "Vera", "Niko", "Lyra",
        "Tess", "Orin", "Sage", "Mina", "Ezra", "Nova", "Iris", "Dax"
    };

    static readonly string[] lastNames =
    {
        "Kessler", "Vance", "Arden", "Halcyon", "Stroud", "Kade", "Sato", "Vale",
        "Rowan", "Kepler", "Rook", "Warden", "Ash", "Drake", "Quill", "Hawke"
    };

    // Deterministic hash -> stable across runs
    public static int StationSeed(Vector3Int coord, int globalSeed)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + coord.x;
            h = h * 31 + coord.y;
            h = h * 31 + coord.z;
            h = h * 31 + globalSeed;
            return h;
        }
    }

    // Stable NPC id per station + index
    public static int NpcId(Vector3Int coord, int globalSeed, int index)
    {
        unchecked
        {
            int h = StationSeed(coord, globalSeed);
            h = h * 31 + index;
            return h;
        }
    }

    public static List<NPCData> GeneratePopulation(Vector3Int coord, int globalSeed, int minPop = 2, int maxPop = 8)
    {
        int stationSeed = StationSeed(coord, globalSeed);
        System.Random rng = new System.Random(stationSeed);

        int pop = rng.Next(minPop, maxPop + 1);

        var list = new List<NPCData>(pop);
        for (int i = 0; i < pop; i++)
        {
            int id = NpcId(coord, globalSeed, i);

            // Use npc-specific RNG so each NPC is stable even if you change pop rules later.
            System.Random npcRng = new System.Random(id);

            string first = firstNames[npcRng.Next(firstNames.Length)];
            string last = lastNames[npcRng.Next(lastNames.Length)];
            int num = npcRng.Next(10, 99); // small suffix for flavor

            string name = $"{first} {last}-{num}";
            list.Add(new NPCData(id, name));
        }

        return list;
    }

    public struct NPCTag
    {
        public string label;
        public Color color;

        public NPCTag(string label, Color color)
        {
            this.label = label;
            this.color = color;
        }
    }

    // Expand this list whenever.
    static readonly NPCTag[] tagPool =
    {
        new NPCTag("Trader",    new Color32( 80, 180, 255, 255)),
        new NPCTag("Veteran",   new Color32(255, 175,  80, 255)),
        new NPCTag("Shady",     new Color32(190,  80, 255, 255)),
        new NPCTag("Friendly",  new Color32( 90, 220, 140, 255)),
        new NPCTag("Mechanic",  new Color32(170, 170, 170, 255)),
        new NPCTag("Courier",   new Color32(255, 110, 150, 255)),
        new NPCTag("Scholar",   new Color32(255, 230, 100, 255)),
        new NPCTag("Rookie",    new Color32(120, 255, 200, 255)),
        new NPCTag("Bounty",    new Color32(255,  90,  90, 255)),
        new NPCTag("Pilot",     new Color32(120, 160, 255, 255)),
        new NPCTag("Scientist", new Color32(100, 220, 255, 255)),
    };

    /// <summary>
    /// Always returns 2–4 distinct tags, deterministic per npcId.
    /// </summary>
    public static NPCTag[] GenerateTags(int npcId, int min = 2, int max = 4)
    {
        if (min < 0) min = 0;
        if (max < min) max = min;
        max = Mathf.Min(max, 4);

        var rng = new System.Random(npcId ^ 0x5F3759DF);

        int count = rng.Next(min, max + 1);
        count = Mathf.Clamp(count, 0, 4);

        // Pick distinct tags
        var chosen = new NPCTag[count];
        var used = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int tries = 0;
            int idx;
            do
            {
                idx = rng.Next(0, tagPool.Length);
                tries++;
            }
            while (used.Contains(idx) && tries < 20);

            used.Add(idx);
            chosen[i] = tagPool[idx];
        }

        return chosen;
    }
}