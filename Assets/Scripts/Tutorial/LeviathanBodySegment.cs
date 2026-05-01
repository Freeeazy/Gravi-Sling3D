using UnityEngine;

public class LeviathanBodySegment : MonoBehaviour
{
    public Transform targetToFollow;
    public float followDistance = 12f;
    public float followSpeed = 8f;
    public float turnSpeed = 6f;

    private void Update()
    {
        if (!targetToFollow)
            return;

        Vector3 dirFromTarget = transform.position - targetToFollow.position;

        if (dirFromTarget.sqrMagnitude < 0.001f)
            dirFromTarget = -targetToFollow.forward;

        Vector3 desiredPos = targetToFollow.position + dirFromTarget.normalized * followDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            followSpeed * Time.deltaTime
        );

        Vector3 lookDir = targetToFollow.position - transform.position;

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