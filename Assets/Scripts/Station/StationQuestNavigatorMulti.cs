using System.Collections.Generic;
using UnityEngine;

public class StationQuestNavigatorMulti : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public StationPosManager posManager;
    public StationProxyManager proxyManager;
    public NPCQuestManager questManager;

    [Header("Arrival / Trigger")]
    public float armTriggerDistance = 900f;
    public string requiredPlayerTag = "";

    private void Awake()
    {
        if (!player) player = Camera.main ? Camera.main.transform : transform;
    }

    private void Update()
    {
        if (!player || !posManager || !questManager) return;

        var quests = questManager.ActiveQuests;
        if (quests == null || quests.Count == 0)
        {
            ClearAllProxyHighlights();
            return;
        }

        // highlight all active quest targets
        ApplyHighlights(quests);

        // arm triggers when close to any target
        for (int i = 0; i < quests.Count; i++)
        {
            float dist = Vector3.Distance(player.position, quests[i].toWorldPos);
            if (dist <= armTriggerDistance)
            {
                TryArmArrivalTriggerOnProxy(quests[i].toCoord);
            }
        }
    }

    private void ApplyHighlights(IReadOnlyList<NPCQuestManager.ActiveQuest> quests)
    {
        if (!proxyManager) return;

        ClearAllProxyHighlights();

        for (int i = 0; i < quests.Count; i++)
        {
            if (proxyManager.TryGetProxy(quests[i].toCoord, out var proxy) && proxy)
                proxy.SetQuestHighlight(true);
        }
    }

    private void ClearAllProxyHighlights()
    {
        if (!proxyManager) return;
        proxyManager.ForEachActiveProxy(p => p.SetQuestHighlight(false));
    }

    private void TryArmArrivalTriggerOnProxy(Vector3Int coord)
    {
        if (!proxyManager) return;

        if (!proxyManager.TryGetProxy(coord, out var proxy) || !proxy) return;

        proxy.SetQuestHighlight(true);

        if (!proxy.orbitTrigger) return;

        var trigger = proxy.orbitTrigger.GetComponent<StationQuestArrivedTrigger>();
        if (!trigger)
            trigger = proxy.orbitTrigger.gameObject.AddComponent<StationQuestArrivedTrigger>();

        trigger.InitMulti(questManager, coord, requiredPlayerTag);
    }
}