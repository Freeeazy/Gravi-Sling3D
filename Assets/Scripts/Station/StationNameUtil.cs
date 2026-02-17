using UnityEngine;

public static class StationNameUtil
{
    // Deterministic hash (fast, stable)
    public static int StationId(Vector3Int coord, int globalSeed)
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

    public static string StationName(Vector3Int coord, int globalSeed)
    {
        int id = StationId(coord, globalSeed);

        System.Random rng = new System.Random(id);

        string[] prefixes = { "Nova", "Helios", "Astra", "Vanguard", "Orion", "Titan", "Echo", "Zenith" };
        string[] suffixes = { "Station", "Outpost", "Platform", "Hub", "Array", "Relay", "Bastion" };

        string prefix = prefixes[rng.Next(prefixes.Length)];
        string suffix = suffixes[rng.Next(suffixes.Length)];

        int number = rng.Next(100, 999);

        return $"{prefix} {suffix} {number}";
    }
}
