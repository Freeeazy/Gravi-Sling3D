using UnityEngine;

public class TutorialStationProxyManager : MonoBehaviour
{
    [Header("Stations")]
    [SerializeField] private StationProxy[] stations;

    [Header("Startup")]
    [SerializeField] private bool initializeOnStart = true;

    [Tooltip("If no stations are assigned, gather StationProxy components below this manager.")]
    [SerializeField] private bool autoFindChildStationsIfEmpty = true;

    [SerializeField] private bool includeInactiveChildStations = true;

    private void Start()
    {
        if (initializeOnStart)
            InitializeStations();
    }

    [ContextMenu("Initialize Stations")]
    public void InitializeStations()
    {
        EnsureStationList();

        if (stations == null)
            return;

        for (int i = 0; i < stations.Length; i++)
        {
            StationProxy station = stations[i];

            if (!station)
                continue;

            station.RandomizeBubbleAndStationLights();
        }
    }

    private void EnsureStationList()
    {
        if (!autoFindChildStationsIfEmpty || stations != null && stations.Length > 0)
            return;

        stations = GetComponentsInChildren<StationProxy>(includeInactiveChildStations);
    }
}