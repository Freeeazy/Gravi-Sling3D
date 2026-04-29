using UnityEngine;

public class LetterCollisionRelay : MonoBehaviour
{
    public MouseSpinGroupCollisionExploder group;
    public string triggeringTag = "Player";

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag(triggeringTag))
            return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.transform.position;

        group.ExplodeFromPoint(hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(triggeringTag))
            return;

        Vector3 hitPoint = GetComponent<Collider>().ClosestPoint(other.transform.position);

        group.ExplodeFromPoint(hitPoint);
    }
}