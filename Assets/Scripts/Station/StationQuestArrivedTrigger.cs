using UnityEngine;

public class StationQuestArrivedTrigger : MonoBehaviour
{
    private StationQuestNavigator _nav;

    private NPCQuestManager _questManager;

    private Vector3Int _targetCoord;
    private string _requiredTag;

    public void Init(StationQuestNavigator nav, Vector3Int targetCoord, string requiredTag = "")
    {
        _nav = nav;
        _questManager = null;

        _targetCoord = targetCoord;
        _requiredTag = requiredTag;

        enabled = true;
    }

    // New behavior for multi quests
    public void InitMulti(NPCQuestManager questManager, Vector3Int targetCoord, string requiredTag = "")
    {
        _questManager = questManager;
        _nav = null;

        _targetCoord = targetCoord;
        _requiredTag = requiredTag;

        enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(_requiredTag) && !other.CompareTag(_requiredTag))
            return;

        // Single-target
        if (_nav)
        {
            _nav.NotifyArrived(_targetCoord);
            return;
        }

        // Multi-target
        if (_questManager)
        {
            _questManager.NotifyArrivedAt(_targetCoord);
            return;
        }
    }
}
