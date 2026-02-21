using UnityEngine;

public class StationProxy : MonoBehaviour
{
    [Header("Orbit (root)")]
    public SphereCollider orbitTrigger;          // same GO as SlingshotPlanet3D
    public SlingshotPlanet3D slingshot;

    [Header("Pre-Gravity (child)")]
    public SphereCollider preGravityTrigger;     // child GO
    public PreGravityPullZone preGravity;

    [Header("Quest Highlight (optional)")]
    [Tooltip("White sphere overlay (or any highlight GO). Will be toggled for the active quest target.")]
    public GameObject questHighlight;

    public Vector3Int Coord { get; private set; }

    private void Reset()
    {
        // Try auto-wire for convenience
        orbitTrigger = GetComponent<SphereCollider>();
        slingshot = GetComponent<SlingshotPlanet3D>();

        preGravity = GetComponentInChildren<PreGravityPullZone>(true);
        if (preGravity) preGravityTrigger = preGravity.GetComponent<SphereCollider>();
    }

    public void Assign(Vector3Int coord, Vector3 worldPos, Quaternion worldRot, StationFieldData data)
    {
        Coord = coord;

        transform.SetPositionAndRotation(worldPos, worldRot);

        // --- radii ---
        float orbitR = Mathf.Max(0.1f, data.orbitRadius); // or data.orbitRadius if you add it
        float preR = orbitR * 1.8f;                       // or data.preGravityRadius

        if (orbitTrigger)
        {
            orbitTrigger.isTrigger = true;
            orbitTrigger.radius = orbitR;
        }

        if (preGravityTrigger)
        {
            preGravityTrigger.isTrigger = true;
            preGravityTrigger.radius = preR;
        }

        // --- wire references between scripts ---
        if (slingshot)
        {
            slingshot.orbitRadius = orbitR; // IMPORTANT: your script uses this internally
            slingshot.preGravityZone = preGravity;
            // slingshot.Bubble can be optional / or assigned in prefab
        }

        if (preGravity)
        {
            preGravity.planet = slingshot; // your PreGravityPullZone expects this
        }
    }
    public void SetQuestHighlight(bool on)
    {
        if (questHighlight) questHighlight.SetActive(on);
    }
}
