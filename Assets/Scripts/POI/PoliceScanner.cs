
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class PoliceScanner : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Scan Durations")]
    [SerializeField, Min(0.1f)] private float heat3ScanSeconds = 8f;
    [SerializeField, Min(0.1f)] private float heat4ScanSeconds = 5f;
    [SerializeField, Min(0.1f)] private float heat5ScanSeconds = 3f;

    [Header("Scanning Laser")]
    [SerializeField] private LineRenderer scanningLaser;
    [SerializeField] private Transform laserOrigin;

    [Tooltip("World-space radius of the laser's scanning motion.")]
    [SerializeField, Min(0f)] private float sweepRadius = 1.5f;

    [Tooltip("How many complete sweeps the laser makes per second.")]
    [SerializeField, Min(0f)] private float sweepFrequency = 0.7f;

    [Tooltip("How much the laser's sweeping pattern changes over time.")]
    [SerializeField, Min(0f)] private float sweepVariation = 0.3f;

    [Header("Laser Appearance")]
    [SerializeField, Min(0f)] private float laserWidth = 0.15f;

    [SerializeField]
    private Color laserColor =
        new Color(0.25f, 0.85f, 1f, 0.85f);

    [SerializeField] private bool pulseLaser = true;
    [SerializeField, Min(0f)] private float laserPulseSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float laserPulseStrength = 0.2f;

    [Header("Scanner Rotation")]
    [SerializeField] private bool rotateTowardPlayer = true;

    [Tooltip("Optional rotating model/pivot. Defaults to this transform.")]
    [SerializeField] private Transform rotatingPart;

    [SerializeField, Min(0f)] private float rotationSpeed = 12f;

    [Tooltip("Model correction relative to local +X. Leave zero for an X-forward disc.")]
    [SerializeField] private Vector3 rotationOffsetEuler;
    [Tooltip("Ignore small aiming errors so the disc follows loosely instead of jittering.")]
    [SerializeField, Range(0f, 15f)] private float rotationAimTolerance = 2f;

    public float ScanProgress => WarningUI.Instance ? WarningUI.Instance.ScanProgress : 0f;
    public bool IsScanning { get; private set; }

    private readonly Dictionary<Collider, float> contacts = new();
    private readonly List<Collider> staleContacts = new();

    private float sweepTime;

    private Transform targetTransform;
    private Collider targetCollider;

    private void Reset()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        scanningLaser = GetComponent<LineRenderer>();
    }

    private void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;

        if (!scanningLaser)
            scanningLaser = GetComponent<LineRenderer>();

        if (!rotatingPart)
            rotatingPart = transform;

        if (scanningLaser)
        {
            scanningLaser.useWorldSpace = true;
            scanningLaser.positionCount = 2;
            scanningLaser.enabled = false;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Support player colliders on child objects.
        for (Transform part = other.transform;
             part != null;
             part = part.parent)
        {
            if (!part.CompareTag(playerTag))
                continue;

            contacts[other] = Time.fixedTime;
            break;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        contacts.Remove(other);
    }


    private void Update()
    {
        CleanupContacts();
        bool hasTarget = FindScanTarget();
        var wanted = WantedLevelManager.Instance;
        var network = WarningUI.Instance;
        int heat = wanted != null ? wanted.CargoHeatLevel : 0;

        if (!hasTarget || wanted == null || wanted.IsWanted || heat < 3 ||
            !network || !network.isActiveAndEnabled)
        {
            StopScanning();
            return;
        }

        float duration = Mathf.Max(0.1f,
            heat == 3 ? heat3ScanSeconds : heat == 4 ? heat4ScanSeconds : heat5ScanSeconds);

        // Report contact only. The singleton advances one shared meter after all
        // scanners update, so overlapping triggers cannot multiply scan speed.
        network.ReportScan(this, duration);
        IsScanning = true;
        sweepTime += Time.deltaTime;
        UpdateRotation();
        UpdateLaser();
    }


    private void CleanupContacts()
    {
        staleContacts.Clear();

        foreach (var contact in contacts)
        {
            if (!contact.Key ||
                !contact.Key.enabled ||
                !contact.Key.gameObject.activeInHierarchy ||
                Time.fixedTime - contact.Value >
                Time.fixedDeltaTime * 2.1f)
            {
                staleContacts.Add(contact.Key);
            }
        }

        foreach (var contact in staleContacts)
            contacts.Remove(contact);
    }

    private bool FindScanTarget()
    {
        // Keep one target while it remains in range; compound colliders must not
        // make the aim hop between different points on the ship every frame.
        if (targetCollider && targetTransform && contacts.ContainsKey(targetCollider))
            return true;

        targetTransform = null;
        targetCollider = null;

        foreach (var contact in contacts)
        {
            Collider col = contact.Key;

            if (!col)
                continue;

            for (Transform part = col.transform;
                 part != null;
                 part = part.parent)
            {
                if (!part.CompareTag(playerTag))
                    continue;

                targetTransform = col.attachedRigidbody ? col.attachedRigidbody.transform : part;
                targetCollider = col;

                return true;
            }
        }

        return false;
    }

    private void UpdateLaser()
    {
        if (!scanningLaser || !targetTransform)
            return;

        if (!scanningLaser.enabled)
            scanningLaser.enabled = true;

        Vector3 origin = laserOrigin
            ? laserOrigin.position
            : transform.position;

        // Aim around the player's collider center.
        Vector3 center = targetCollider
            ? targetCollider.bounds.center
            : targetTransform.position;

        float phase = sweepTime *
                      sweepFrequency *
                      Mathf.PI * 2f;

        // A slightly imperfect circular sweep.
        float x = Mathf.Cos(phase);
        float y = Mathf.Sin(phase);

        x += Mathf.Sin(phase * 0.47f) * sweepVariation;
        y += Mathf.Cos(phase * 0.63f) * sweepVariation;

        Vector3 offset =
            targetTransform.right * x * sweepRadius +
            targetTransform.up * y * sweepRadius;

        Vector3 endpoint = center + offset;

        scanningLaser.SetPosition(0, origin);
        scanningLaser.SetPosition(1, endpoint);

        float width = laserWidth;

        if (pulseLaser)
        {
            float pulse =
                Mathf.Sin(Time.time * laserPulseSpeed) *
                laserPulseStrength;

            width *= 1f + pulse;
        }

        scanningLaser.startWidth = width;
        scanningLaser.endWidth = width * 0.65f;

        scanningLaser.startColor = laserColor;
        scanningLaser.endColor = laserColor;
    }

    private void UpdateRotation()
    {
        if (!rotateTowardPlayer ||
            !rotatingPart ||
            !targetTransform)
            return;

        Vector3 direction =
            targetTransform.position - rotatingPart.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 localFacing = Quaternion.Inverse(Quaternion.Euler(rotationOffsetEuler)) * Vector3.right;
        Vector3 currentFacing = rotatingPart.rotation * localFacing;
        if (Vector3.Angle(currentFacing, direction) <= rotationAimTolerance)
            return;

        // Swing the existing X-facing axis toward the player without prescribing
        // an up vector or roll. No pole switch, upright reset or accumulated twist.
        Quaternion targetRotation = Quaternion.FromToRotation(currentFacing, direction) * rotatingPart.rotation;

        rotatingPart.rotation = Quaternion.RotateTowards(
            rotatingPart.rotation,
            targetRotation,
            Mathf.Max(0f, rotationSpeed) * Time.deltaTime
        );
    }


    private void StopScanning()
    {
        if (WarningUI.Instance) WarningUI.Instance.UnregisterScanner(this);
        sweepTime = 0f;
        IsScanning = false;
        if (scanningLaser) scanningLaser.enabled = false;
    }


    private void OnDisable()
    {
        contacts.Clear();
        targetTransform = null;
        targetCollider = null;
        StopScanning();
    }
}
