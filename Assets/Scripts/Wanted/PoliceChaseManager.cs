using UnityEngine;

/// <summary>
/// One cop for any positive wanted level. Owns spawning and the escape timer.
/// Requires the existing WantedLevelManager; no changes to SimpleMove are needed.
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
    [SerializeField] private PoliceShipAI activePolice;
    [SerializeField] private bool escapeTimerRunning;
    [SerializeField] private float escapeTimeRemaining;

    public PoliceShipAI ActivePolice => activePolice;
    public bool IsEscapeTimerRunning => escapeTimerRunning;
    public float EscapeTimeRemaining => escapeTimeRemaining;

    private bool started;
    private bool subscribed;
    private float nextSpawnAttempt;
    private bool reportedSpawnError;

    private void Start()
    {
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

        // Raising/lowering stars while still wanted never duplicates the cop or
        // resets an existing search countdown in this prototype.
        if (activePolice == null)
            TrySpawnPolice();
    }

    private void TrySpawnPolice()
    {
        nextSpawnAttempt = Time.time + 1f;
        if (activePolice != null || wantedManager == null || !wantedManager.IsWanted)
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
        Vector3 position = playerBody.position + Random.onUnitSphere * spawnRadius;
        activePolice = Instantiate(policePrefab, position, Quaternion.identity);
        // Supports a disabled prefab root/component as well as the recommended active prefab.
        activePolice.gameObject.SetActive(true);
        activePolice.enabled = true;
        activePolice.StateChanged += OnPoliceStateChanged;
        activePolice.Initialize(playerBody);

        if (spawnRadius > activePolice.LoseContactDistance)
            Debug.LogWarning("Spawn Radius exceeds Lose Contact Distance; the cop may lose you immediately.", this);
        if (logChaseEvents)
            Debug.Log("Police spawned: pursuit started.", this);
    }

    private void OnPoliceStateChanged(PoliceShipAI police, PoliceShipAI.ChaseState state)
    {
        if (police != activePolice || wantedManager == null || !wantedManager.IsWanted)
            return;

        if (logChaseEvents)
            Debug.Log($"Police state: {state}", this);

        if (state == PoliceShipAI.ChaseState.Searching)
        {
            escapeTimerRunning = true;
            escapeTimeRemaining = escapeDuration;
            wantedManager.SetEscapeTimer(escapeTimeRemaining);
        }
        else
        {
            ResetEscapeTimer();
        }
    }

    private void FixedUpdate()
    {
        var movement = SimpleMove.Instance;
        if (movement == null)
            return;

        // One cop for now. Later, count all connected cops here.
        int beamCount =
            wantedManager != null &&
            wantedManager.IsWanted &&
            playerBody != null &&
            playerBody.gameObject.activeInHierarchy &&
            movement.rb == playerBody &&
            activePolice != null &&
            activePolice.isActiveAndEnabled &&
            activePolice.IsBeamActive
                ? 1
                : 0;

        movement.SetTractorBeamCount(beamCount);
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

        if (activePolice == null || !activePolice.isActiveAndEnabled ||
            playerBody == null || !playerBody.gameObject.activeInHierarchy)
        {
            // Missing/disabled actors are not treated as successful escapes.
            EndChase();
            if (Time.time >= nextSpawnAttempt)
                TrySpawnPolice();
            return;
        }

        if (!escapeTimerRunning)
            return;
        // Defensive check: reacquisition wins before consuming the countdown.
        if (!activePolice.IsSearching)
        {
            ResetEscapeTimer();
            return;
        }

        escapeTimeRemaining = Mathf.Max(0f, escapeTimeRemaining - Time.deltaTime);
        wantedManager.SetEscapeTimer(escapeTimeRemaining);
        if (escapeTimeRemaining <= 0f)
        {
            if (logChaseEvents)
                Debug.Log("Pursuit lost: wanted level cleared.", this);
            wantedManager.ClearWantedLevel(); // The change event also despawns the cop.
        }
    }

    private void ResetEscapeTimer()
    {
        escapeTimerRunning = false;
        escapeTimeRemaining = 0f;
        if (wantedManager != null)
            wantedManager.ClearEscapeTimer();
    }

    private void EndChase()
    {
        if (SimpleMove.Instance != null)
            SimpleMove.Instance.SetTractorBeamCount(0);

        ResetEscapeTimer();
        if (activePolice == null)
            return;
        PoliceShipAI police = activePolice;
        activePolice = null;
        police.StateChanged -= OnPoliceStateChanged;
        police.gameObject.SetActive(false);
        Destroy(police.gameObject);
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
