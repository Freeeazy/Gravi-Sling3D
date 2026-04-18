using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RingTrackBuilder : MonoBehaviour
{
    [Header("References")]
    public GameObject ringPrefab;
    public Transform ringParent;

    [Header("Path")]
    public List<Transform> controlPoints = new List<Transform>();

    [Header("Ring Placement")]
    [Min(2)] public int ringCount = 10;
    public bool lookAlongPath = true;
    public Vector3 ringRotationOffset = Vector3.zero;

    [Header("Debug")]
    public bool autoRebuild = false;
    public bool drawCurve = true;
    public int curveResolution = 30;
    public Color curveColor = Color.cyan;

    private void OnValidate()
    {
        if (autoRebuild && !Application.isPlaying)
        {
            RebuildRings();
        }
    }

    [ContextMenu("Collect Child Points")]
    public void CollectChildPoints()
    {
        controlPoints.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (ringParent != null && child == ringParent)
                continue;

            controlPoints.Add(child);
        }
    }

    [ContextMenu("Rebuild Rings")]
    public void RebuildRings()
    {
        if (ringPrefab == null || controlPoints.Count < 2)
            return;

        if (ringParent == null)
        {
            Transform existing = transform.Find("GeneratedRings");
            if (existing != null)
            {
                ringParent = existing;
            }
            else
            {
                GameObject parentObj = new GameObject("GeneratedRings");
                parentObj.transform.SetParent(transform);
                parentObj.transform.localPosition = Vector3.zero;
                parentObj.transform.localRotation = Quaternion.identity;
                ringParent = parentObj.transform;
            }
        }

        ClearRings();

        for (int i = 0; i < ringCount; i++)
        {
            float t = (ringCount == 1) ? 0f : i / (float)(ringCount - 1);

            Vector3 position = GetPointOnSpline(t);
            Quaternion rotation = Quaternion.identity;

            if (lookAlongPath)
            {
                Vector3 forward = GetTangentOnSpline(t);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                }
            }

            rotation *= Quaternion.Euler(ringRotationOffset);

            GameObject ring;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                ring = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(ringPrefab, ringParent);
            else
                ring = Instantiate(ringPrefab, ringParent);
#else
ring = Instantiate(ringPrefab, ringParent);
#endif

            ring.transform.position = position;
            ring.transform.rotation = rotation;
        }
    }

    [ContextMenu("Clear Rings")]
    public void ClearRings()
    {
        if (ringParent == null)
            return;

        List<GameObject> toDelete = new List<GameObject>();

        for (int i = 0; i < ringParent.childCount; i++)
        {
            toDelete.Add(ringParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < toDelete.Count; i++)
        {
            if (!Application.isPlaying)
                DestroyImmediate(toDelete[i]);
            else
                Destroy(toDelete[i]);
        }
    }

    public Vector3 GetPointOnSpline(float t)
    {
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

    private void OnDrawGizmos()
    {
        if (!drawCurve || controlPoints.Count < 2)
            return;

        Gizmos.color = curveColor;

        Vector3 prevPoint = GetPointOnSpline(0f);

        for (int i = 1; i <= curveResolution; i++)
        {
            float t = i / (float)curveResolution;
            Vector3 nextPoint = GetPointOnSpline(t);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        Gizmos.color = Color.yellow;
        foreach (Transform point in controlPoints)
        {
            if (point != null)
                Gizmos.DrawSphere(point.position, 0.4f);
        }
    }
}