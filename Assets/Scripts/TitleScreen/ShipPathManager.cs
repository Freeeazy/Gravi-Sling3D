using System.Collections.Generic;
using UnityEngine;

public class ShipPathManager : MonoBehaviour
{
    [Tooltip("Optional first path for debugging/testing. Uses this once, then returns to random logic. (-1 For Immediate Random)")]
    public int startingPathIndex = -1;

    [System.Serializable]
    public class ShipPath
    {
        public string pathName;
        public List<Transform> controlPoints = new List<Transform>();

        [Header("Optional Per-Path Speed Override")]
        public bool overrideSpeed;
        public float minSpeed = 10f;
        public float maxSpeed = 40f;

        public AnimationCurve speedCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.8f, 1f),
            new Keyframe(1f, 0f)
        );
    }

    [Header("Player")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Paths")]
    public List<ShipPath> paths = new List<ShipPath>();
    public bool autoStart = false;
    public bool randomizePath = true;
    public bool avoidPreviousPath = true;
    public bool loopPaths = true;
    public float loopDelay = 1f;

    [Header("Default Speed")]
    public float minSpeed = 10f;
    public float maxSpeed = 40f;

    [Tooltip("Used unless the selected path overrides speed.")]
    public AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(0.8f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Rotation")]
    public bool rotateAlongPath = true;
    public float rotationLerpSpeed = 8f;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Debug")]
    public bool drawCurve = true;
    public int curveResolution = 40;
    public Color curveColor = Color.cyan;
    public bool drawOnlyActivePath = false;

    private bool isMoving;
    private float pathT;
    private int currentPathIndex = -1;
    private int previousPathIndex = -1;
    private Coroutine loopRoutine;
    private bool hasUsedStartingPath = false;

    private ShipPath CurrentPath
    {
        get
        {
            if (currentPathIndex < 0 || currentPathIndex >= paths.Count)
                return null;

            return paths[currentPathIndex];
        }
    }

    private void Start()
    {
        if (player != null && playerRb == null)
            playerRb = player.GetComponent<Rigidbody>();

        if (!autoStart)
            return;

        if (startingPathIndex >= 0 && startingPathIndex < paths.Count)
        {
            hasUsedStartingPath = true;
            BeginPath(startingPathIndex);
        }
        else
        {
            BeginRandomPath();
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving)
            return;

        MoveAlongPath();
    }

    [ContextMenu("Begin Random Path")]
    public void BeginRandomPath()
    {
        // Use debug starting path only once
        if (!hasUsedStartingPath &&
            startingPathIndex >= 0 &&
            startingPathIndex < paths.Count)
        {
            hasUsedStartingPath = true;
            BeginPath(startingPathIndex);
            return;
        }

        int index = GetRandomPathIndex();
        BeginPath(index);
    }

    public void BeginPath(int pathIndex)
    {
        if (player == null)
            return;

        if (pathIndex < 0 || pathIndex >= paths.Count)
            return;

        if (paths[pathIndex].controlPoints.Count < 2)
            return;

        previousPathIndex = currentPathIndex;
        currentPathIndex = pathIndex;

        pathT = 0f;
        isMoving = true;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        if (playerRb != null)
        {
            playerRb.isKinematic = false;
            playerRb.useGravity = false;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        player.position = GetPointOnSpline(0f);
    }

    private int GetRandomPathIndex()
    {
        if (paths.Count == 0)
            return -1;

        if (paths.Count == 1)
            return 0;

        int index = Random.Range(0, paths.Count);

        if (avoidPreviousPath)
        {
            int safety = 0;

            while (index == currentPathIndex && safety < 25)
            {
                index = Random.Range(0, paths.Count);
                safety++;
            }
        }

        return index;
    }

    private void MoveAlongPath()
    {
        ShipPath path = CurrentPath;

        if (path == null || path.controlPoints.Count < 2)
        {
            EndPath();
            return;
        }

        float activeMinSpeed = path.overrideSpeed ? path.minSpeed : minSpeed;
        float activeMaxSpeed = path.overrideSpeed ? path.maxSpeed : maxSpeed;
        AnimationCurve activeCurve = path.overrideSpeed ? path.speedCurve : speedCurve;

        float curveValue = Mathf.Clamp01(activeCurve.Evaluate(pathT));
        float currentSpeed = Mathf.Lerp(activeMinSpeed, activeMaxSpeed, curveValue);

        float approxLength = GetApproxPathLength();
        float deltaT = (currentSpeed * Time.fixedDeltaTime) / Mathf.Max(approxLength, 0.0001f);

        pathT = Mathf.Clamp01(pathT + deltaT);

        Vector3 targetPos = GetPointOnSpline(pathT);
        Vector3 tangent = GetTangentOnSpline(pathT);

        if (playerRb != null)
        {
            Vector3 velocity = (targetPos - playerRb.position) / Time.fixedDeltaTime;

            playerRb.linearVelocity = velocity;
            playerRb.MovePosition(targetPos);
        }
        else
        {
            player.position = targetPos;
        }

        if (rotateAlongPath && tangent.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
            targetRot *= Quaternion.Euler(rotationOffset);

            if (playerRb != null)
            {
                Quaternion smoothed = Quaternion.Slerp(
                    playerRb.rotation,
                    targetRot,
                    rotationLerpSpeed * Time.fixedDeltaTime
                );

                playerRb.MoveRotation(smoothed);
            }
            else
            {
                player.rotation = Quaternion.Slerp(
                    player.rotation,
                    targetRot,
                    rotationLerpSpeed * Time.fixedDeltaTime
                );
            }
        }

        if (pathT >= 1f)
        {
            EndPath();
        }
    }

    public void EndPath()
    {
        isMoving = false;

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        if (loopPaths)
            loopRoutine = StartCoroutine(LoopNextPathAfterDelay());
    }

    private System.Collections.IEnumerator LoopNextPathAfterDelay()
    {
        yield return new WaitForSeconds(loopDelay);
        BeginRandomPath();
    }

    public Vector3 GetPointOnSpline(float t)
    {
        ShipPath path = CurrentPath;

        if (path == null || path.controlPoints.Count < 2)
            return player != null ? player.position : transform.position;

        List<Transform> controlPoints = path.controlPoints;

        t = Mathf.Clamp01(t);

        if (controlPoints.Count == 2)
        {
            return Vector3.Lerp(controlPoints[0].position, controlPoints[1].position, t);
        }

        int segmentCount = controlPoints.Count - 1;
        float scaledT = t * segmentCount;
        int i = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, segmentCount - 1);
        float localT = scaledT - i;

        Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)].position;
        Vector3 p1 = controlPoints[i].position;
        Vector3 p2 = controlPoints[i + 1].position;
        Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Count - 1)].position;

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    public Vector3 GetTangentOnSpline(float t)
    {
        float delta = 0.01f;
        float t1 = Mathf.Clamp01(t);
        float t2 = Mathf.Clamp01(t + delta);

        Vector3 p1 = GetPointOnSpline(t1);
        Vector3 p2 = GetPointOnSpline(t2);

        return (p2 - p1).normalized;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private float GetApproxPathLength()
    {
        if (CurrentPath == null || CurrentPath.controlPoints.Count < 2)
            return 0f;

        float length = 0f;
        Vector3 prev = GetPointOnSpline(0f);

        for (int i = 1; i <= curveResolution; i++)
        {
            float t = i / (float)curveResolution;
            Vector3 next = GetPointOnSpline(t);

            length += Vector3.Distance(prev, next);
            prev = next;
        }

        return length;
    }

    private void OnDrawGizmos()
    {
        if (!drawCurve || paths == null)
            return;

        for (int p = 0; p < paths.Count; p++)
        {
            if (drawOnlyActivePath && p != currentPathIndex)
                continue;

            DrawPathGizmos(paths[p], p == currentPathIndex);
        }
    }

    private void DrawPathGizmos(ShipPath path, bool active)
    {
        if (path == null || path.controlPoints == null || path.controlPoints.Count < 2)
            return;

        Gizmos.color = active ? Color.green : curveColor;

        Vector3 prevPoint = GetPointOnSplineForPath(path, 0f);

        for (int i = 1; i <= curveResolution; i++)
        {
            float t = i / (float)curveResolution;
            Vector3 nextPoint = GetPointOnSplineForPath(path, t);

            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.color = active ? Color.green : Color.yellow;

        foreach (Transform point in path.controlPoints)
        {
            if (point != null)
                Gizmos.DrawSphere(point.position, active ? 0.55f : 0.35f);
        }
    }

    private Vector3 GetPointOnSplineForPath(ShipPath path, float t)
    {
        List<Transform> controlPoints = path.controlPoints;

        t = Mathf.Clamp01(t);

        if (controlPoints.Count == 2)
        {
            return Vector3.Lerp(controlPoints[0].position, controlPoints[1].position, t);
        }

        int segmentCount = controlPoints.Count - 1;
        float scaledT = t * segmentCount;
        int i = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, segmentCount - 1);
        float localT = scaledT - i;

        Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)].position;
        Vector3 p1 = controlPoints[i].position;
        Vector3 p2 = controlPoints[i + 1].position;
        Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Count - 1)].position;

        return CatmullRom(p0, p1, p2, p3, localT);
    }
}