using UnityEngine;

public enum POIType
{
    None,
    Station,
    Anomaly,
    AbandonedOutpost,
    Wreckage
}

[CreateAssetMenu(menuName = "POI/POI Field Data")]
public class POIFieldData : ScriptableObject
{
    [Header("Chunk Metadata")]
    public Vector3 fieldCenter = Vector3.zero;
    public Vector3 fieldSize = new Vector3(1000f, 1000f, 1000f);
    public bool useFixedSeed = false;
    public int seed = 12345;

    [Header("POI Instance")]
    public POIType poiType = POIType.None;

    public bool HasPOI => poiType != POIType.None;
    public bool IsStation => poiType == POIType.Station;

    [Header("Transform")]
    public Vector3 localPosition = Vector3.zero;
    public Quaternion localRotation = Quaternion.identity;
    public float uniformScale = 1f;

    [Header("Station / Slingshot Settings")]
    public float preGravityRadius = 400f;
    public float orbitRadius = 280f;

    public void Clear()
    {
        fieldCenter = Vector3.zero;
        fieldSize = new Vector3(1000f, 1000f, 1000f);
        useFixedSeed = false;
        seed = 12345;

        poiType = POIType.None;
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        uniformScale = 1f;

        preGravityRadius = 400f;
        orbitRadius = 280f;
    }

    public Vector3 WorldPosition(Vector3 chunkWorldOrigin)
    {
        return chunkWorldOrigin + localPosition;
    }

    public Quaternion WorldRotation()
    {
        return localRotation;
    }
}