using UnityEngine;

public class ExclusionSphereSource : MonoBehaviour
{
    [Min(0f)] public float radius = 60f;
    public bool active = true;

    public Vector3 Position => transform.position;
    public float Radius => radius;
}
