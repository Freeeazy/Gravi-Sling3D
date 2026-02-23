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
    [Tooltip("Assign 10 TMP_Text references (list items). We'll enable/disable or clear them.")]
    public TMP_Text[] npcNameTexts = new TMP_Text[10];

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
        if (!posManager || npcNameTexts == null || npcNameTexts.Length == 0) return;

        Vector3Int coord = posManager.WorldToChunkCoord(stationWorldPos);

        List<NPCData> npcs = GetOrCreatePopulation(coord);

        // Update population count text
        if (populationCountText)
        {
            populationCountText.text = $"{npcs.Count}";
        }

        // Fill pool
        int poolCount = npcNameTexts.Length;
        int showCount = Mathf.Min(poolCount, npcs.Count);

        for (int i = 0; i < poolCount; i++)
        {
            TMP_Text t = npcNameTexts[i];
            if (!t) continue;

            bool on = i < showCount;

            // Either disable the whole item or just blank text — your call.
            // If your list item is just the TMP_Text GO, this is fine.
            t.gameObject.SetActive(on);

            if (on)
                t.text = npcs[i].displayName;
            else
                t.text = "";
        }
    }

    public void ClearStation()
    {
        if (npcNameTexts == null) return;

        for (int i = 0; i < npcNameTexts.Length; i++)
        {
            if (!npcNameTexts[i]) continue;
            npcNameTexts[i].text = "";
            npcNameTexts[i].gameObject.SetActive(false);
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