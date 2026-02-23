using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance;

    [Header("References")]
    public StationPosManager posManager;     // provides WorldToChunkCoord + globalSeed

    [Header("UI")]
    public TMP_Text populationCountText;

    [Header("UI Pool (size 10)")]
    [Tooltip("Assign 10 NPCUILink references (rows). We'll enable/disable them.")]
    public NPCUILink[] npcRows = new NPCUILink[10];

    [Header("Population Settings")]
    public int minPopulation = 2;
    public int maxPopulation = 8;

    // Cache per station coord so we don't regen every time UI opens/refreshes
    private readonly Dictionary<Vector3Int, List<NPCData>> cache = new Dictionary<Vector3Int, List<NPCData>>();

    private void Awake()
    {
        Instance = this;
    }

    public void SetCurrentStationByWorldPos(Vector3 stationWorldPos)
    {
        if (!posManager || npcRows == null || npcRows.Length == 0) return;

        Vector3Int coord = posManager.WorldToChunkCoord(stationWorldPos);
        List<NPCData> npcs = GetOrCreatePopulation(coord);

        if (populationCountText)
            populationCountText.text = $"{npcs.Count}";

        int poolCount = npcRows.Length;
        int showCount = Mathf.Min(poolCount, npcs.Count);

        for (int i = 0; i < poolCount; i++)
        {
            var row = npcRows[i];
            if (!row) continue;

            bool on = i < showCount;

            row.SetRowActive(on);

            if (on)
                row.Bind(npcs[i]);
        }
    }

    public void ClearStation()
    {
        if (npcRows == null) return;

        if (populationCountText)
            populationCountText.text = "0";

        for (int i = 0; i < npcRows.Length; i++)
        {
            if (!npcRows[i]) continue;
            npcRows[i].SetRowActive(false);
        }
    }

    private List<NPCData> GetOrCreatePopulation(Vector3Int coord)
    {
        if (cache.TryGetValue(coord, out var list) && list != null)
            return list;

        int seed = posManager ? posManager.globalSeed : 0;
        list = NPCUtil.GeneratePopulation(coord, seed, minPopulation, maxPopulation);

        cache[coord] = list;
        return list;
    }
}