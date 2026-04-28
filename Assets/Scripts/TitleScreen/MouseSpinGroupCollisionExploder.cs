using UnityEngine;

public class MouseSpinGroupCollisionExploder : MonoBehaviour
{
    [Header("Trigger Rules")]
    public string triggeringTag = "Player";

    [Header("Letters To Explode")]
    public MouseObjectSpin[] spinObjects;

    [Header("Explosion Origin")]
    public Transform sharedExplosionOrigin;

    [Header("Reset Timing")]
    public float autoResetDelay = 1.5f;
    public float returnDuration = 1f;

    [Header("Spam Guard")]
    public float collisionCooldown = 0.25f;

    private float lastTriggerTime = -999f;

    private void Reset()
    {
        spinObjects = FindObjectsByType<MouseObjectSpin>(FindObjectsSortMode.None);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(triggeringTag))
            return;

        if (Time.unscaledTime - lastTriggerTime < collisionCooldown)
            return;

        lastTriggerTime = Time.unscaledTime;

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        if (sharedExplosionOrigin != null)
            sharedExplosionOrigin.position = hitPoint;

        foreach (MouseObjectSpin spin in spinObjects)
        {
            if (spin == null) continue;

            if (sharedExplosionOrigin != null)
                spin.explosionOrigin = sharedExplosionOrigin;

            spin.ExplodeFromOrigin();
        }

        StartCoroutine(ResetAllAfterDelay());
    }

    private System.Collections.IEnumerator ResetAllAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoResetDelay);

        foreach (MouseObjectSpin spin in spinObjects)
        {
            if (spin == null) continue;
            spin.ReturnToRest(returnDuration);
        }
    }
}