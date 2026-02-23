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
}