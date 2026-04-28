using UnityEngine;

public class LeviathanStalker : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Transform safeZoneCenter;

    [Header("Positioning")]
    public float outwardOffset = 180f;
    public float orbitRadius = 90f;
    public float verticalOffset = 35f;

    [Header("Movement")]
    public float orbitSpeed = 12f;
    public float followSpeed = 2.5f;
    public float minDistanceFromPlayer = 100f;

    [Header("Rotation")]
    public bool facePlayer = true;
    public float turnSpeed = 5f;

    private float orbitAngle;

    private void OnEnable()
    {
        orbitAngle = Random.Range(0f, 360f);
    }

    private void Update()
    {
        if (!player || !safeZoneCenter)
            return;

        Vector3 outwardDir = player.position - safeZoneCenter.position;

        if (outwardDir.sqrMagnitude < 0.001f)
            outwardDir = player.forward;

        outwardDir.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, outwardDir).normalized;

        if (right.sqrMagnitude < 0.001f)
            right = player.right;

        Vector3 up = Vector3.Cross(outwardDir, right).normalized;

        Vector3 dangerCenter =
            player.position +
            outwardDir * outwardOffset +
            Vector3.up * verticalOffset;

        orbitAngle += orbitSpeed * Time.deltaTime;

        float rad = orbitAngle * Mathf.Deg2Rad;

        Vector3 orbitOffset =
            right * Mathf.Cos(rad) * orbitRadius +
            up * Mathf.Sin(rad) * orbitRadius;

        Vector3 targetPos = dangerCenter + orbitOffset;

        float distToPlayer = Vector3.Distance(targetPos, player.position);

        if (distToPlayer < minDistanceFromPlayer)
        {
            Vector3 awayFromPlayer = (targetPos - player.position).normalized;
            targetPos = player.position + awayFromPlayer * minDistanceFromPlayer;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            followSpeed * Time.deltaTime
        );

        if (facePlayer)
        {
            Vector3 lookDir = player.position - transform.position;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    turnSpeed * Time.deltaTime
                );
            }
        }
    }
}