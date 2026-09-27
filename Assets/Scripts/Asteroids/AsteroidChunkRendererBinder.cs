using UnityEngine;

// Compatibility bridge for existing scenes. Rendering now reads posManager.Chunks
// directly; the Inspector list is refreshed only when the grid changes.
[RequireComponent(typeof(AsteroidFieldInstancedRenderer))]
public class AsteroidChunkRendererBinder : MonoBehaviour
{
    public AsteroidPosManager posManager;
    private AsteroidFieldInstancedRenderer asteroidRenderer;
    private AsteroidPosManager _subscribedManager;

    private void Awake()
    {
        asteroidRenderer = GetComponent<AsteroidFieldInstancedRenderer>();
        if (!posManager) posManager = FindFirstObjectByType<AsteroidPosManager>();
    }

    private void OnEnable() => SyncManager();
    private void Start() => RefreshList();

    private void Update()
    {
        // Allow a runtime manager-reference change without rebuilding the list each frame.
        if (_subscribedManager != posManager) SyncManager();
    }

    private void OnDisable()
    {
        if (_subscribedManager) _subscribedManager.OnGridChanged -= RefreshList;
        _subscribedManager = null;
    }

    private void SyncManager()
    {
        if (!asteroidRenderer) asteroidRenderer = GetComponent<AsteroidFieldInstancedRenderer>();
        AsteroidPosManager previousManager = _subscribedManager;
        if (_subscribedManager) _subscribedManager.OnGridChanged -= RefreshList;
        _subscribedManager = posManager;
        if (_subscribedManager) _subscribedManager.OnGridChanged += RefreshList;
        if (asteroidRenderer && (!asteroidRenderer.posManager || asteroidRenderer.posManager == previousManager))
            asteroidRenderer.posManager = posManager;
        RefreshList();
    }

    private void RefreshList()
    {
        if (!asteroidRenderer) return;
        if (asteroidRenderer.fieldDatas == null)
            asteroidRenderer.fieldDatas = new System.Collections.Generic.List<AsteroidFieldData>();
        asteroidRenderer.fieldDatas.Clear();
        if (!posManager) return;
        foreach (var chunk in posManager.Chunks)
            asteroidRenderer.fieldDatas.Add(chunk.Value);
    }
}
