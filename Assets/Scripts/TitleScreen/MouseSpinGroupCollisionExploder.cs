using UnityEngine;

public class MouseSpinGroupCollisionExploder : MonoBehaviour
{
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
    private Coroutine resetRoutine;

    private void Reset()
    {
        spinObjects = FindObjectsByType<MouseObjectSpin>(FindObjectsSortMode.None);
    }

    public void ExplodeFromPoint(Vector3 hitPoint)
    {
        if (Time.unscaledTime - lastTriggerTime < collisionCooldown)
            return;

        lastTriggerTime = Time.unscaledTime;

        if (sharedExplosionOrigin != null)
            sharedExplosionOrigin.position = hitPoint;

        foreach (MouseObjectSpin spin in spinObjects)
        {
            if (spin == null) continue;

            spin.explosionOrigin = sharedExplosionOrigin;
            spin.ExplodeFromOrigin();
        }

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(ResetAllAfterDelay());
    }

    private System.Collections.IEnumerator ResetAllAfterDelay()
    {
        yield return new WaitForSecondsRealtime(autoResetDelay);

        foreach (MouseObjectSpin spin in spinObjects)
        {
            if (spin == null) continue;
            spin.ReturnToRest(returnDuration);
        }

        resetRoutine = null;
    }
}