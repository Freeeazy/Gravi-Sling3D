using UnityEngine;

[RequireComponent(typeof(MouseObjectSpin))]
public class MouseSpinCollisionExploder : MonoBehaviour
{
    [Header("Trigger Rules")]
    public string triggeringTag = "Player";

    [Header("Reset Timing")]
    public float autoResetDelay = 1.5f;
    public float returnDuration = 1f;

    [Header("Spam Guard")]
    public float collisionCooldown = 0.25f;

    private MouseObjectSpin spin;
    private float lastTriggerTime = -999f;

    private void Awake()
    {
        spin = GetComponent<MouseObjectSpin>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag(triggeringTag))
            return;

        if (Time.unscaledTime - lastTriggerTime < collisionCooldown)
            return;

        lastTriggerTime = Time.unscaledTime;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.transform.position;

        spin.ExplodeFromPoint(hitPoint, autoResetDelay, returnDuration);
    }
}