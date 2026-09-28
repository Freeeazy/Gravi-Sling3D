using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns three cops per wanted star, up to fifteen. Positive star decreases leave
/// existing cops in the chase. Escape requires every cop to lose contact.
/// Requires WantedLevelManager and the existing SimpleMove tractor-beam API.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class PoliceChaseManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional: falls back to WantedLevelManager.Instance.")]
    [SerializeField] private WantedLevelManager wantedManager;
    [Tooltip("Assign the actual CHILD Rigidbody used by SimpleMove. If empty, uses SimpleMove.Instance.rb.")]
    [SerializeField] private Rigidbody playerBody;
    [Tooltip("Assign an active prefab with PoliceShipAI on its ROOT.")]
    [SerializeField] private PoliceShipAI policePrefab;

    [Header("Spawn")]
    [Tooltip("Random direction on a sphere this many units from the player. Keep below the cop's Lose Contact Distance.")]
    [SerializeField, Min(1f)] private float spawnRadius = 60f;

    [Header("Escape")]
    [SerializeField, Min(0.1f)] private float escapeDuration = 8f;
    [SerializeField] private bool logChaseEvents = true;

    [Header("Runtime Debug")]
    [SerializeField] private List<PoliceShipAI> activePoliceShips = new List<PoliceShipAI>();
    [SerializeField] private int targetPoliceCount;
    [SerializeField] private bool escapeTimerRunning;
    [SerializeField] private float escapeTimeRemaining;

    // Kept for existing callers that only need one cop.
    public PoliceShipAI ActivePolice => activePoliceShips.Count > 0 ? activePoliceShips[0] : null;
    public IReadOnlyList<PoliceShipAI> ActivePoliceShips => activePoliceShips;
    public int ActivePoliceCount => activePoliceShips.Count;
    public bool IsEscapeTimerRunning => escapeTimerRunning;
    public float EscapeTimeRemaining => escapeTimeRemaining;

    private bool started;
    private bool subscribed;
    private float nextSpawnAttempt;
    private bool reportedSpawnError;

    private void Start()
    {
        activePoliceShips.Clear();
        targetPoliceCount = 0;
        started = true;
        Connect();
    }

    private void OnEnable()
    {
        if (started)
            Connect();
    }

    private void Connect()
    {
        if (wantedManager == null)
            wantedManager = WantedLevelManager.Instance;
        if (wantedManager == null)
        {
            Debug.LogError("PoliceChaseManager needs an active WantedLevelManager in this scene.", this);
            enabled = false;
            return;
        }

        if (!subscribed)
        {
            wantedManager.WantedLevelChanged += OnWantedLevelChanged;
            subscribed = true;
        }
        // Handles starting with stars already assigned and re-enabling the manager.
        OnWantedLevelChanged(wantedManager.CurrentWantedLevel, wantedManager.CurrentWantedLevel);
    }

    private void OnWantedLevelChanged(int previousLevel, int newLevel)
    {
        if (newLevel == 0)
        {
            EndChase();
            return;
        }

        // Remember the peak level until the chase ends. Lowering debug stars
        // neither removes cops nor lowers the replacement count.
        targetPoliceCount = Mathf.Max(targetPoliceCount, Mathf.Clamp(newLevel, 1, WantedLevelManager.SupportedMaxWantedLevel) * 3);
        RemoveUnavailablePolice();
        if (activePoliceShips.Count < targetPoliceCount)
            TrySpawnPolice();
    }

    private void TrySpawnPolice()
    {
        nextSpawnAttempt = Time.time + 1f;
        if (wantedManager == null || !wantedManager.IsWanted || activePoliceShips.Count >= targetPoliceCount)
            return;
        if (playerBody == null && SimpleMove.Instance != null)
            playerBody = SimpleMove.Instance.rb;

        if (policePrefab == null || playerBody == null)
        {
            if (!reportedSpawnError)
                Debug.LogWarning("PoliceChaseManager: assign the Police Prefab and player's child Rigidbody. Will retry.", this);
            reportedSpawnError = true;
            return;
        }
        if (!playerBody.gameObject.activeInHierarchy)
            return;

        reportedSpawnError = false;
        ResetEscapeTimer();
        while (activePoliceShips.Count < targetPoliceCount)
        {
            Vector3 position = playerBody.position + Random.onUnitSphere * spawnRadius;
            PoliceShipAI police = Instantiate(policePrefab, position, Quaternion.identity);
            police.gameObject.SetActive(true);
            police.enabled = true;
            activePoliceShips.Add(police);
            police.StateChanged += OnPoliceStateChanged;
            police.Initialize(playerBody);

            if (spawnRadius > police.LoseContactDistance)
                Debug.LogWarning("Spawn Radius exceeds Lose Contact Distance; the cop may lose you immediately.", this);
        }

        if (logChaseEvents)
            Debug.Log($"Police deployed: {activePoliceShips.Count} cop(s) in the chase.", this);
    }

    private void OnPoliceStateChanged(PoliceShipAI police, PoliceShipAI.ChaseState state)
    {
        if (!activePoliceShips.Contains(police) || wantedManager == null || !wantedManager.IsWanted)
            return;

        if (logChaseEvents)
            Debug.Log($"Police {police.name} state: {state}", police);

        // Any reacquisition cancels escape immediately. Start the countdown in
        // Update, after all cops have completed their physics state changes.
        if (state != PoliceShipAI.ChaseState.Searching)
            ResetEscapeTimer();
    }

    private void FixedUpdate()
    {
        // This manager runs before PoliceShipAI. Sample every cop first, then
        // distribute one real sighting before any cop chooses movement this tick.
        SharePoliceSightings();

        var movement = SimpleMove.Instance;
        if (movement == null)
            return;

        int beamCount = 0;
        if (wantedManager != null && wantedManager.IsWanted &&
            playerBody != null && playerBody.gameObject.activeInHierarchy &&
            movement.rb == playerBody)
        {
            foreach (PoliceShipAI police in activePoliceShips)
            {
                if (police != null && police.isActiveAndEnabled && police.IsBeamActive)
                    beamCount++;
            }
        }
        movement.SetTractorBeamCount(beamCount);
    }

    private void SharePoliceSightings()
    {
        if (wantedManager == null || !wantedManager.IsWanted ||
            playerBody == null || !playerBody.gameObject.activeInHierarchy)
            return;

        PoliceShipAI spotter = null;
        foreach (PoliceShipAI police in activePoliceShips)
        {
            if (police == null || !police.isActiveAndEnabled)
                continue;
            police.RefreshDirectContact();
            if (police.HasDirectContact &&
                (spotter == null || police.DistanceToPlayer < spotter.DistanceToPlayer))
                spotter = police;
        }

        if (spotter == null)
            return;

        // Received reports never qualify as direct sightings on the next pass.
        Vector3 position = spotter.LastObservedPosition;
        Vector3 velocity = spotter.LastObservedVelocity;
        foreach (PoliceShipAI police in activePoliceShips)
        {
            if (police != null && police.isActiveAndEnabled)
                police.ReceiveSharedSighting(position, velocity);
        }
        if (escapeTimerRunning)
            ResetEscapeTimer();
    }

    private void Update()
    {
        if (wantedManager == null)
        {
            EndChase();
            return;
        }
        if (!wantedManager.IsWanted)
            return;

        if (playerBody == null || !playerBody.gameObject.activeInHierarchy)
        {
            // A missing player is not an escape. Retain the target count for retry.
            DespawnAllPolice();
            ResetEscapeTimer();
            if (Time.time >= nextSpawnAttempt)
                TrySpawnPolice();
            return;
        }

        RemoveUnavailablePolice();
        if (activePoliceShips.Count < targetPoliceCount)
        {
            // Replace only missing/disabled cops, preserving the other pursuers.
            ResetEscapeTimer();
            if (Time.time >= nextSpawnAttempt)
                TrySpawnPolice();
            return;
        }

        if (!AllPoliceSearching())
        {
            if (escapeTimerRunning)
                ResetEscapeTimer();
            return;
        }

        if (!escapeTimerRunning)
        {
            escapeTimerRunning = true;
            escapeTimeRemaining = escapeDuration;
            wantedManager.SetEscapeTimer(escapeTimeRemaining);
            return;
        }

        escapeTimeRemaining = Mathf.Max(0f, escapeTimeRemaining - Time.deltaTime);
        wantedManager.SetEscapeTimer(escapeTimeRemaining);
        if (escapeTimeRemaining <= 0f)
        {
            if (logChaseEvents)
                Debug.Log("All police lost: wanted level cleared.", this);
            wantedManager.ClearWantedLevel(); // Change event despawns every cop.
        }
    }

    private bool AllPoliceSearching()
    {
        if (activePoliceShips.Count == 0)
            return false;
        foreach (PoliceShipAI police in activePoliceShips)
        {
            if (police == null || !police.isActiveAndEnabled || !police.IsSearching)
                return false;
        }
        return true;
    }

    private void RemoveUnavailablePolice()
    {
        for (int i = activePoliceShips.Count - 1; i >= 0; i--)
        {
            PoliceShipAI police = activePoliceShips[i];
            if (police != null && police.isActiveAndEnabled)
                continue;
            activePoliceShips.RemoveAt(i);
            DestroyPolice(police);
            ResetEscapeTimer();
        }
    }

    private void DestroyPolice(PoliceShipAI police)
    {
        if (police == null)
            return;
        police.StateChanged -= OnPoliceStateChanged;
        police.gameObject.SetActive(false);
        Destroy(police.gameObject);
    }

    private void ResetEscapeTimer()
    {
        escapeTimerRunning = false;
        escapeTimeRemaining = 0f;
        if (wantedManager != null)
            wantedManager.ClearEscapeTimer();
    }

    private void DespawnAllPolice()
    {
        if (SimpleMove.Instance != null)
            SimpleMove.Instance.SetTractorBeamCount(0);
        foreach (PoliceShipAI police in activePoliceShips)
            DestroyPolice(police);
        activePoliceShips.Clear();
    }

    private void EndChase()
    {
        targetPoliceCount = 0;
        ResetEscapeTimer();
        DespawnAllPolice();
    }

    private void OnDisable()
    {
        if (subscribed && wantedManager != null)
            wantedManager.WantedLevelChanged -= OnWantedLevelChanged;
        subscribed = false;
        EndChase();
    }

    private void OnDrawGizmosSelected()
    {
        Rigidbody targetBody = playerBody;
        if (targetBody == null && Application.isPlaying && SimpleMove.Instance != null)
            targetBody = SimpleMove.Instance.rb;
        if (targetBody == null)
            return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetBody.position, spawnRadius);
    }
}
