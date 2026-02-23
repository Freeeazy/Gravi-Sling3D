using System.Collections.Generic;
using UnityEngine;

public static class NPCUIAssets
{
    private static bool _loaded;
    private static Dictionary<string, Sprite> _portraitsByName;

    // If you want deterministic selection across a set of portraits:
    private static List<Sprite> _portraitList;

    private const string RES_FOLDER = "NPC-Icons";

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        _portraitsByName = new Dictionary<string, Sprite>(64);
        _portraitList = new List<Sprite>(64);

        var sprites = Resources.LoadAll<Sprite>(RES_FOLDER);
        if (sprites == null) return;

        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            if (!s) continue;

            // Key by exact sprite name
            if (!_portraitsByName.ContainsKey(s.name))
                _portraitsByName.Add(s.name, s);

            _portraitList.Add(s);
        }
    }

    /// <summary>
    /// Deterministically pick a portrait based on npcId (stable per NPC).
    /// </summary>
    public static Sprite GetPortraitForNpc(int npcId)
    {
        EnsureLoaded();
        if (_portraitList == null || _portraitList.Count == 0) return null;

        // stable index
        int idx = npcId;
        if (idx < 0) idx = -idx;
        idx %= _portraitList.Count;

        return _portraitList[idx];
    }

    /// <summary>
    /// If later you want explicit portrait keys, you can call this:
    /// </summary>
    public static Sprite GetPortraitByName(string portraitKey)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(portraitKey)) return null;

        if (_portraitsByName != null && _portraitsByName.TryGetValue(portraitKey, out var s))
            return s;

        return null;
    }
}