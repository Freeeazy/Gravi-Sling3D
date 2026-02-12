using UnityEngine;

public class StationQuestArrivedTrigger : MonoBehaviour
{
    private StationQuestNavigator _nav;
    private Vector3Int _targetCoord;
    private string _requiredTag;

    public void Init(StationQuestNavigator nav, Vector3Int targetCoord, string requiredTag = "")
    {
        _nav = nav;
        _targetCoord = targetCoord;
        _requiredTag = requiredTag;
        enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_nav) return;

        if (!string.IsNullOrEmpty(_requiredTag) && !other.CompareTag(_requiredTag))
            return;

        _nav.NotifyArrived(_targetCoord);
    }
}
