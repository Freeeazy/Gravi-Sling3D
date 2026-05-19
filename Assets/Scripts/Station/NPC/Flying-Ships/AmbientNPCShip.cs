using UnityEngine;

public class AmbientNPCShip : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody rb;

    [Header("Movement")]
    public Vector2 speedRange = new Vector2(60f, 160f);
    public float recycleDistance = 1200f;
    public float spawnDistance = 700f;

    [Header("Flyby Target")]
    public Vector2 flybyRadiusRange = new Vector2(100f, 300f);

    [Header("Scale Randomization")]
    public bool randomizeScale = true;
    public Vector2 scaleRange = new Vector2(0.6f, 1.4f);

    private Vector3 moveDir;
    private float currentSpeed;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    private void Start()
    {
        Recycle();
    }

    private void FixedUpdate()
    {
        if (player == null || rb == null) return;

        rb.linearVelocity = moveDir * currentSpeed;

        float sqrDistance = (transform.position - player.position).sqrMagnitude;
        float sqrRecycleDistance = recycleDistance * recycleDistance;

        if (sqrDistance > sqrRecycleDistance)
        {
            Recycle();
        }
    }

    private void LateUpdate()
    {
        if (moveDir != Vector3.zero)
        {
            transform.forward = moveDir;
        }
    }

    private void Recycle()
    {
        if (player == null) return;

        Vector3 spawnDir = Random.onUnitSphere;
        Vector3 spawnPos = player.position + spawnDir * spawnDistance;

        float flybyRadius = Random.Range(flybyRadiusRange.x, flybyRadiusRange.y);
        Vector3 flybyTarget = player.position + Random.insideUnitSphere * flybyRadius;

        moveDir = (flybyTarget - spawnPos).normalized;
        currentSpeed = Random.Range(speedRange.x, speedRange.y);

        if (rb != null)
        {
            rb.position = spawnPos;
            rb.linearVelocity = moveDir * currentSpeed;
            rb.angularVelocity = Vector3.zero;
            rb.rotation = Quaternion.LookRotation(moveDir);
        }
        else
        {
            transform.position = spawnPos;
            transform.forward = moveDir;
        }

        if (randomizeScale)
        {
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            transform.localScale = Vector3.one * randomScale;
        }
    }
}