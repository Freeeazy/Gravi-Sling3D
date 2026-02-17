using TMPro;
using UnityEngine;

public class StationNameManager : MonoBehaviour
{
    public static StationNameManager Instance;

    [Header("References")]
    public StationPosManager posManager;

    [Header("UI")]
    public TMP_Text stationNameText;

    private void Awake()
    {
        Instance = this;
    }

    public void SetCurrentStationByWorldPos(Vector3 stationWorldPos)
    {
        if (!posManager || !stationNameText) return;

        Vector3Int coord = posManager.WorldToChunkCoord(stationWorldPos);
        stationNameText.text = StationNameUtil.StationName(coord, posManager.globalSeed);
    }

    public void ClearStation()
    {
        if (stationNameText) stationNameText.text = "";
    }
}
