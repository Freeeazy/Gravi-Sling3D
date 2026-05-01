using UnityEngine;

public class LeviathanStalker : MonoBehaviour
{
    private enum LeviathanPhase
    {
        Lurking,
        ClosingSpiral,
        Containment
    }

    [Header("Refs")]
    public Transform player;
    public Transform safeZoneCenter;

    [Header("Body Spawn Placement")]
    public Transform[] bodySegmentsToTeleport;
    public bool teleportBodyWithHead = true;

    [Header("Positioning")]
    public float outwardOffset = 180f;
    public float orbitRadius = 90f;
    public float verticalOffset = 35f;

    [Header("Movement")]
    public float orbitSpeed = 12f;
    public float followSpeed = 2.5f;
    public float minDistanceFromPlayer = 100f;

    [Header("Rotation")]
    public bool facePlayer = true;
    public float turnSpeed = 5f;

    [Header("Spawn Visibility")]
    public bool hideUntilPositioned = true;
    public Renderer[] renderersToToggle;

    [Header("Flee/Despawn")]
    public float fleeSpeed = 180f;
    public float despawnDistanceFromPlayer = 3000f;

    [Header("Phase 2 - Closing Spiral")]
    public bool useClosingSpiral = true;
    public float phase1LurkDurationMin = 8f;
    public float phase1LurkDurationMax = 15f;
    public float phase2CloseDurationMin = 30f;
    public float phase2CloseDurationMax = 60f;
    public float phase2OutwardOffset = 80f;
    public float phase2OrbitRadius = 45f;

    [Header("Grasp Movement Suppression")]
    public SimpleMove simpleMove;
    public float graspRange = 350f;
    public float graspDuration = 3f;

    [Header("Phase 3 - Containment")]
    public float phase3StartDelay = 1.5f;
    public float phase3OrbitRadius = 250f;
    public float phase3StartRotationSpeed = 40f;
    public float phase3EndRotationSpeed = 220f;
    public float phase3Duration = 4f;
    public Transform[] restartAnchors;

    private float orbitAngle;
    private bool hasPositionedOnce;
    private Vector3 previousPosition;

    private bool isFleeing;
    private Vector3 fleeDir;

    public bool IsFleeing => isFleeing;

    private float phaseTimer;
    private float phaseDuration;
    private float phase2StartOutwardOffset;
    private float phase2StartOrbitRadius;

    private LeviathanPhase currentPhase;

    private float originalAcceleration;
    private float graspTimer;
    private bool isGrasping;

    private float phase3Timer;
    private bool phase3DelayActive;

    private void OnEnable()
    {
        orbitAngle = Random.Range(0f, 360f);
        hasPositionedOnce = false;

        if (hideUntilPositioned)
            SetRenderersVisible(false);

        previousPosition = transform.position;

        currentPhase = LeviathanPhase.Lurking;
        phaseTimer = 0f;
        phaseDuration = Random.Range(phase1LurkDurationMin, phase1LurkDurationMax);
        phase2StartOutwardOffset = outwardOffset;
        phase2StartOrbitRadius = orbitRadius;

        if (simpleMove)
            originalAcceleration = simpleMove.acceleration;

        graspTimer = 0f;
        isGrasping = false;
    }

    private void Update()
    {
        if (!player || !safeZoneCenter)
            return;

        if (isFleeing)
        {
            transform.position += fleeDir * fleeSpeed * Time.deltaTime;

            if (facePlayer == false)
            {
                Quaternion targetRot = Quaternion.LookRotation(fleeDir, Vector3.up);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    turnSpeed * Time.deltaTime
                );
            }

            if (Vector3.Distance(transform.position, player.position) >= despawnDistanceFromPlayer)
            {
                gameObject.SetActive(false);
            }

            return;
        }

        UpdatePhase();
        UpdateGrasp();

        if (currentPhase == LeviathanPhase.Containment)
        {
            UpdateContainment();
            return;
        }

        Vector3 outwardDir = player.position - safeZoneCenter.position;

        if (outwardDir.sqrMagnitude < 0.001f)
            outwardDir = player.forward;

        outwardDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, outwardDir).normalized;

        if (right.sqrMagnitude < 0.001f)
            right = player.right;

        Vector3 up = Vector3.Cross(outwardDir, right).normalized;

        float currentOutwardOffset = outwardOffset;
        float currentOrbitRadius = orbitRadius;

        if (currentPhase == LeviathanPhase.ClosingSpiral)
        {
            float t = Mathf.Clamp01(phaseTimer / phaseDuration);

            currentOutwardOffset = Mathf.Lerp(
                phase2StartOutwardOffset,
                phase2OutwardOffset,
                t
            );

            currentOrbitRadius = Mathf.Lerp(
                phase2StartOrbitRadius,
                phase2OrbitRadius,
                t
            );
        }

        Vector3 dangerCenter =
            player.position +
            outwardDir * currentOutwardOffset +
            Vector3.up * verticalOffset;

        float currentOrbitSpeed = orbitSpeed;

        if (currentPhase == LeviathanPhase.ClosingSpiral)
        {
            float t = Mathf.Clamp01(phaseTimer / phaseDuration);

            currentOrbitSpeed = Mathf.Lerp(
                orbitSpeed,
                40f,
                t
            );
        }

        orbitAngle += currentOrbitSpeed * Time.deltaTime;

        float rad = orbitAngle * Mathf.Deg2Rad;

        Vector3 orbitOffset =
            right * Mathf.Cos(rad) * currentOrbitRadius +
            up * Mathf.Sin(rad) * currentOrbitRadius;

        Vector3 targetPos = dangerCenter + orbitOffset;

        float distToPlayer = Vector3.Distance(targetPos, player.position);

        if (distToPlayer < minDistanceFromPlayer)
        {
            Vector3 awayFromPlayer = (targetPos - player.position).normalized;
            targetPos = player.position + awayFromPlayer * minDistanceFromPlayer;
        }

        if (!hasPositionedOnce)
        {
            transform.position = targetPos;

            if (teleportBodyWithHead)
                TeleportBodySegmentsToHead();

            hasPositionedOnce = true;

            if (hideUntilPositioned)
                SetRenderersVisible(true);
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                followSpeed * Time.deltaTime
            );
        }

        Vector3 lookDir;

        if (facePlayer)
        {
            lookDir = player.position - transform.position;
        }
        else
        {
            lookDir = transform.position - previousPosition;
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(
                lookDir.normalized,
                Vector3.up
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );
        }

        previousPosition = transform.position;
    }
    private void SetRenderersVisible(bool visible)
    {
        foreach (Renderer r in renderersToToggle)
        {
            if (r)
                r.enabled = visible;
        }
    }

    private void TeleportBodySegmentsToHead()
    {
        foreach (Transform segment in bodySegmentsToTeleport)
        {
            if (!segment)
                continue;

            segment.position = transform.position;
            segment.rotation = transform.rotation;
        }
    }

    public void BeginFlee()
    {
        if (!player)
            return;

        isFleeing = true;

        fleeDir = transform.position - player.position;

        if (fleeDir.sqrMagnitude < 0.001f)
            fleeDir = -transform.forward;

        fleeDir.Normalize();

        ResetGrasp();
    }

    public void ResumeStalking(Transform newSafeZoneCenter)
    {
        isFleeing = false;

        if (newSafeZoneCenter)
            safeZoneCenter = newSafeZoneCenter;
    }
    private void UpdatePhase()
    {
        if (!useClosingSpiral)
            return;

        phaseTimer += Time.deltaTime;

        if (currentPhase == LeviathanPhase.Lurking)
        {
            if (phaseTimer >= phaseDuration)
            {
                currentPhase = LeviathanPhase.ClosingSpiral;
                phaseTimer = 0f;
                phaseDuration = Random.Range(phase2CloseDurationMin, phase2CloseDurationMax);

                phase2StartOutwardOffset = outwardOffset;
                phase2StartOrbitRadius = orbitRadius;
            }
        }
    }
    private void UpdateGrasp()
    {
        if (!simpleMove || !player)
            return;

        bool canGrasp =
            currentPhase == LeviathanPhase.ClosingSpiral &&
            Vector3.Distance(transform.position, player.position) <= graspRange;

        if (canGrasp)
        {
            isGrasping = true;

            graspTimer += Time.deltaTime;

            float t = Mathf.Clamp01(graspTimer / graspDuration);

            simpleMove.acceleration = Mathf.Lerp(
                originalAcceleration,
                0f,
                t
            );
        }
        else if (currentPhase != LeviathanPhase.Containment)
        {
            if (isGrasping)
            {
                graspTimer = 0f;
                isGrasping = false;

                simpleMove.acceleration = originalAcceleration;
            }
        }

        if (simpleMove.acceleration <= 0.01f)
        {
            BeginContainment();
        }
    }
    private void ResetGrasp()
    {
        graspTimer = 0f;
        isGrasping = false;

        if (simpleMove)
            simpleMove.acceleration = originalAcceleration;
    }
    private void BeginContainment()
    {
        if (currentPhase == LeviathanPhase.Containment)
            return;

        currentPhase = LeviathanPhase.Containment;
        phase3Timer = 0f;
        phase3DelayActive = true;

        if (simpleMove)
            simpleMove.acceleration = 0f;
    }
    private void UpdateContainment()
    {
        if (!player)
            return;

        phase3Timer += Time.deltaTime;

        if (phase3DelayActive)
        {
            if (phase3Timer < phase3StartDelay)
                return;

            phase3DelayActive = false;
            phase3Timer = 0f;
        }

        float t = Mathf.Clamp01(phase3Timer / phase3Duration);
        float spinSpeed = Mathf.Lerp(phase3StartRotationSpeed, phase3EndRotationSpeed, t);

        orbitAngle += spinSpeed * Time.deltaTime;

        Transform[] ringPieces = GetRingPieces();

        int count = ringPieces.Length;

        for (int i = 0; i < count; i++)
        {
            if (!ringPieces[i])
                continue;

            float angle = orbitAngle + (360f / count) * i;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 offset =
                new Vector3(
                    Mathf.Cos(rad),
                    0f,
                    Mathf.Sin(rad)
                ) * phase3OrbitRadius;

            ringPieces[i].position = player.position + offset;
            ringPieces[i].LookAt(player.position);
        }

        if (phase3Timer >= phase3Duration)
        {
            RespawnPlayerAtActiveAnchor();
            gameObject.SetActive(false);
        }
    }
    private Transform[] GetRingPieces()
    {
        Transform[] ringPieces = new Transform[bodySegmentsToTeleport.Length + 1];

        ringPieces[0] = transform;

        for (int i = 0; i < bodySegmentsToTeleport.Length; i++)
        {
            ringPieces[i + 1] = bodySegmentsToTeleport[i];
        }

        return ringPieces;
    }
    private void RespawnPlayerAtActiveAnchor()
    {
        Transform anchor = null;

        foreach (Transform restartAnchor in restartAnchors)
        {
            if (restartAnchor && restartAnchor.gameObject.activeInHierarchy)
            {
                anchor = restartAnchor;
                break;
            }
        }

        if (!anchor)
            return;

        player.position = anchor.position;
        player.rotation = anchor.rotation;

        if (simpleMove && simpleMove.rb)
        {
            simpleMove.rb.linearVelocity = Vector3.zero;
            simpleMove.rb.angularVelocity = Vector3.zero;
            simpleMove.rb.position = anchor.position;
            simpleMove.rb.rotation = anchor.rotation;
        }

        ResetGrasp();
    }
}