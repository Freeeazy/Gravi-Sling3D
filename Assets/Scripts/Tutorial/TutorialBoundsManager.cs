using TMPro;
using UnityEngine;

public class TutorialBoundsManager : MonoBehaviour
{
    [Header("Player Rigidbody")]
    public Rigidbody playerRb;

    [Header("Outside Safe Zone Drag")]
    public float minOutsideDamping = 0.01f;
    public float maxOutsideDamping = 0.5f;

    [Header("Safe Zones")]
    public Collider[] safeZones;

    [Header("Outside Bounds Object")]
    public GameObject outsideBoundsObject;

    [Header("Countdown UI")]
    public GameObject countdownParentObject;
    public TextMeshProUGUI countdownText;
    public float countdownDuration = 99f;

    [Header("Player Reference")]
    public Transform player;

    [Header("Leviathan Prototype")]
    public GameObject leviathanHead;

    public GameObject[] leviathanBodySegments;

    public float leviathanSpawnDistanceFromPlayer = 250f;
    public float leviathanSideOffset = 40f;
    public float leviathanUpOffset = 25f;
    public bool facePlayerOnSpawn = true;

    private float countdownTimer;
    private bool leviathanStarted;

    private void Awake()
    {
        ResetCountdown();

        if (leviathanHead)
            leviathanHead.SetActive(false);

        if (leviathanBodySegments != null)
        {
            foreach (GameObject segment in leviathanBodySegments)
            {
                if (segment)
                    segment.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!player)
            return;

        bool insideAnySafeZone = IsInsideAnySafeZone();

        LeviathanStalker stalker = GetLeviathanStalker();

        if (insideAnySafeZone)
        {
            if (outsideBoundsObject)
                outsideBoundsObject.SetActive(false);

            ResetCountdown();

            if (leviathanHead && leviathanHead.activeInHierarchy && stalker)
            {
                stalker.BeginFlee();
            }

            if (playerRb)
                playerRb.linearDamping = 0f;

            return;
        }

        if (outsideBoundsObject)
            outsideBoundsObject.SetActive(true);


        if (leviathanHead && leviathanHead.activeInHierarchy && stalker && stalker.IsFleeing)
        {
            Collider activeZone = GetCurrentActiveSafeZone();

            if (activeZone)
                stalker.ResumeStalking(activeZone.transform);

            leviathanStarted = true;

            if (countdownParentObject)
                countdownParentObject.SetActive(false);

            return;
        }

        RunCountdown();
    }

    private bool IsInsideAnySafeZone()
    {
        foreach (Collider zone in safeZones)
        {
            if (zone == null || !zone.gameObject.activeInHierarchy || !zone.enabled)
                continue;

            if (zone.bounds.Contains(player.position))
                return true;
        }

        return false;
    }

    private Collider GetCurrentActiveSafeZone()
    {
        foreach (Collider zone in safeZones)
        {
            if (zone == null || !zone.gameObject.activeInHierarchy || !zone.enabled)
                continue;

            return zone;
        }

        return null;
    }

    private void RunCountdown()
    {
        if (leviathanStarted)
            return;

        if (countdownParentObject)
            countdownParentObject.SetActive(true);

        countdownTimer -= Time.deltaTime;

        if (playerRb)
        {
            float normalizedTimeSpentOutside = 1f - (countdownTimer / countdownDuration);

            playerRb.linearDamping = Mathf.Lerp(
                minOutsideDamping,
                maxOutsideDamping,
                normalizedTimeSpentOutside
            );
        }

        int secondsLeft = Mathf.CeilToInt(Mathf.Max(0f, countdownTimer));

        if (countdownText)
            countdownText.text = $"{secondsLeft}s";

        if (countdownTimer <= 0f)
        {
            leviathanStarted = true;

            if (countdownParentObject)
                countdownParentObject.SetActive(false);

            StartLeviathanSearch();
        }
    }

    private void ResetCountdown()
    {
        countdownTimer = countdownDuration;
        leviathanStarted = false;

        if (countdownParentObject)
            countdownParentObject.SetActive(false);

        if (countdownText)
            countdownText.text = $"{Mathf.CeilToInt(countdownDuration)}s";
    }

    private void StartLeviathanSearch()
    {
        SpawnLeviathanHead();

        Debug.Log("Leviathan begins searching for the player...");
    }

    private void SpawnLeviathanHead()
    {
        if (!leviathanHead || !player)
            return;

        Collider activeZone = GetCurrentActiveSafeZone();

        Vector3 zoneCenter;

        if (activeZone)
            zoneCenter = activeZone.bounds.center;
        else
            zoneCenter = transform.position;

        // Direction from tutorial/safe area through the player and outward
        Vector3 outwardDir = player.position - zoneCenter;

        if (outwardDir.sqrMagnitude < 0.001f)
            outwardDir = player.forward;

        outwardDir.Normalize();

        // Side offset so it is not perfectly centered/math-looking
        Vector3 sideDir = Vector3.Cross(Vector3.up, outwardDir).normalized;

        if (sideDir.sqrMagnitude < 0.001f)
            sideDir = player.right;

        Vector3 spawnPos =
            player.position +
            outwardDir * leviathanSpawnDistanceFromPlayer +
            sideDir * leviathanSideOffset +
            Vector3.up * leviathanUpOffset;

        leviathanHead.transform.position = spawnPos;

        if (facePlayerOnSpawn)
        {
            Vector3 lookDir = player.position - leviathanHead.transform.position;

            if (lookDir.sqrMagnitude > 0.001f)
                leviathanHead.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();

        leviathanHead.SetActive(true);

        if (leviathanBodySegments != null)
        {
            foreach (GameObject segment in leviathanBodySegments)
            {
                if (segment)
                    segment.SetActive(true);
            }
        }

        LeviathanStalker stalker = leviathanHead.GetComponent<LeviathanStalker>();

        if (stalker)
        {
            stalker.player = player;

            if (activeZone)
                stalker.safeZoneCenter = activeZone.transform;
        }
    }

    private LeviathanStalker GetLeviathanStalker()
    {
        if (!leviathanHead)
            return null;

        return leviathanHead.GetComponent<LeviathanStalker>();
    }
}