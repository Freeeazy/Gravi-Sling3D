using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class WormholeConeTether : MonoBehaviour
{
    public static WormholeConeTether Instance { get; private set; }

    [Header("Targets")]
    public Transform startPoint;
    public Transform endPoint;

    [Header("Particle Distance Control")]
    public float particleLifetime = 2f;
    public float minSpeed = 0.5f;
    public float maxSpeed = 200f;

    [Header("Cone Shape")]
    public float coneAngle = 12f;
    public float minRadius = 0.25f;
    public float radiusPerDistance = 0.03f;
    public float maxRadius = 4f;

    [Header("Rotation")]
    public bool faceTarget = true;
    public float rotateLerpSpeed = 12f;

    [Header("Endpoint Offset")]
    public float endBuffer = 1.5f; // distance away from player

    private ParticleSystem ps;
    private ParticleSystem.MainModule main;
    private ParticleSystem.ShapeModule shape;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ps = GetComponent<ParticleSystem>();
        main = ps.main;
        shape = ps.shape;
    }

    private void LateUpdate()
    {
        if (startPoint == null || endPoint == null)
            return;

        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;

        Vector3 dir = (end - start).normalized;

        // apply buffer so particles stop before hitting player
        end -= dir * endBuffer;

        float distance = Vector3.Distance(start, end);

        transform.position = start;

        if (faceTarget && distance > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(end - start, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotateLerpSpeed
            );
        }

        float speed = distance / Mathf.Max(0.01f, particleLifetime);
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        main.startLifetime = particleLifetime;
        main.startSpeed = speed;

        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;

        float radius = Mathf.Clamp(distance * radiusPerDistance, minRadius, maxRadius);
        shape.radius = radius;
    }

    // ---------------------------
    // PUBLIC CONTROL METHODS
    // ---------------------------

    public void SetTargets(Transform start, Transform end)
    {
        startPoint = start;
        endPoint = end;

        if (!ps.isPlaying)
            ps.Play();
    }

    public void SetEndPoint(Transform end)
    {
        endPoint = end;

        if (!ps.isPlaying)
            ps.Play();
    }

    public void SetStartPoint(Transform start)
    {
        startPoint = start;
    }

    public void StopTether()
    {
        ps.Stop();
        startPoint = null;
        endPoint = null;
    }
}