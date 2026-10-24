using System.Collections.Generic;
using UnityEngine;

/// <summary>Attach to a POI trigger sphere. Only carried heat 3-5 can restart an escaped chase.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class PoliceScanner : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0.1f)] private float heat3ScanSeconds = 8f;
    [SerializeField, Min(0.1f)] private float heat4ScanSeconds = 5f;
    [SerializeField, Min(0.1f)] private float heat5ScanSeconds = 3f;

    public float ScanProgress { get; private set; }
    private readonly Dictionary<Collider, float> contacts = new Dictionary<Collider, float>();
    private readonly List<Collider> staleContacts = new List<Collider>();
    private float scanTime;
    private int scanningHeat;

    private void Reset() => GetComponent<SphereCollider>().isTrigger = true;

    private void Awake() => GetComponent<SphereCollider>().isTrigger = true;

    private void OnTriggerStay(Collider other)
    {
        // Support the player's child Rigidbody/colliders and a tag on an ancestor.
        for (Transform part = other.transform; part != null; part = part.parent)
        {
            if (!part.CompareTag(playerTag)) continue;
            contacts[other] = Time.fixedTime;
            break;
        }
    }

    private void OnTriggerExit(Collider other) => contacts.Remove(other);

    private void Update()
    {
        // Disabled, destroyed or teleported colliders must not leave a phantom scan.
        staleContacts.Clear();
        foreach (var contact in contacts)
            if (!contact.Key || !contact.Key.enabled || !contact.Key.gameObject.activeInHierarchy ||
                Time.fixedTime - contact.Value > Time.fixedDeltaTime * 2.1f)
                staleContacts.Add(contact.Key);
        foreach (var contact in staleContacts) contacts.Remove(contact);

        var wanted = WantedLevelManager.Instance;
        int heat = wanted != null ? wanted.CargoHeatLevel : 0;
        if (contacts.Count == 0 || wanted == null || wanted.IsWanted || heat < 3)
        {
            ResetScan();
            return;
        }
        if (scanningHeat != heat) ResetScan();
        scanningHeat = heat;
        float duration = Mathf.Max(0.1f, heat == 3 ? heat3ScanSeconds : heat == 4 ? heat4ScanSeconds : heat5ScanSeconds);
        scanTime += Time.deltaTime;
        ScanProgress = Mathf.Clamp01(scanTime / duration);
        if (scanTime >= duration)
        {
            wanted.TryRescanCargo();
            ResetScan();
        }
    }

    private void ResetScan()
    {
        scanTime = 0f;
        scanningHeat = 0;
        ScanProgress = 0f;
    }

    private void OnDisable()
    {
        contacts.Clear();
        ResetScan();
    }
}
