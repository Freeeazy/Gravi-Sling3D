using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Stations/Station Field Data")]
public class StationFieldData : ScriptableObject
{
    [Header("Chunk Metadata")]
    public Vector3 fieldCenter = Vector3.zero;     // world center of chunk
    public Vector3 fieldSize = new Vector3(1000f, 1000f, 1000f);
    public bool useFixedSeed = false;
    public int seed = 12345;

    [Header("Station Instance (0 or 1 for now)")]
    public bool hasStation = false;

    // Local-to-chunk coordinates (0..chunkSize range-ish)
    public Vector3 localPosition = Vector3.zero;
    public Quaternion localRotation = Quaternion.identity;

    // Optional for later (orbit radius / culling radius / keepout)
    public float preGravityRadius = 400f; // outer trigger
    public float orbitRadius = 280f;      // inner trigger (capture)

    public void Clear()
    {
        fieldCenter = Vector3.zero;
        fieldSize = new Vector3(1000f, 1000f, 1000f);
        useFixedSeed = false;
        seed = 12345;

        hasStation = false;
        localPosition = Vector3.zero;
        localRotation = Quaternion.identity;
        preGravityRadius = 400f;
        orbitRadius = 280f;
    }   

    public Vector3 WorldPosition(Vector3 chunkWorldOrigin) => chunkWorldOrigin + localPosition;
    public Quaternion WorldRotation() => localRotation;
}
