using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmbientNPCShipManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Ship Prefabs")]
    public AmbientShipEntry[] shipPrefabs;

    [Header("Pool")]
    public int startingPoolSize = 20;
    public Transform shipHolder;

    [Header("Movement")]
    public Vector2 speedRange = new Vector2(200f, 800f);
    public float spawnDistance = 900f;
    public float recycleDistance = 1400f;

    [Header("Flyby Target")]
    public Vector2 flybyRadiusRange = new Vector2(100f, 350f);

    [Header("Scale Randomization")]
    public bool randomizeScale = true;
    public Vector2 scaleRange = new Vector2(0.6f, 1.4f);

    [Header("Traffic Rhythm")]
    public Vector2 quietDelayRange = new Vector2(2f, 7f);
    public Vector2 burstDelayRange = new Vector2(0.15f, 0.8f);
    public Vector2Int burstShipCountRange = new Vector2Int(1, 5);

    [Range(0f, 1f)]
    public float burstChance = 0.35f;

    [Header("Player Speed Behavior")]
    public float fastPlayerSpeedThreshold = 120f;

    [Tooltip("When player is moving fast, ships spawn more in front of the player.")]
    [Range(0f, 1f)]
    public float fastForwardSpawnBias = 0.85f;

    [Tooltip("When player is slow, ships spawn more evenly around the player.")]
    [Range(0f, 1f)]
    public float slowForwardSpawnBias = 0.2f;

    [Header("Safety")]
    public float minSpawnAngleFromPlayerVelocity = 15f;

    private readonly List<AmbientNPCShipInstance> ships = new List<AmbientNPCShipInstance>();

    private void Awake()
    {
        if (!player)
            player = Camera.main ? Camera.main.transform : transform;

        if (!playerRb && player)
            playerRb = player.GetComponent<Rigidbody>();

        if (!shipHolder)
            shipHolder = transform;

        BuildPool();
    }

    private void OnEnable()
    {
        StartCoroutine(TrafficRoutine());
    }

    private void FixedUpdate()
    {
        UpdateActiveShips();
    }

    private void BuildPool()
    {
        if (shipPrefabs == null || shipPrefabs.Length == 0)
            return;

        for (int i = 0; i < startingPoolSize; i++)
        {
            GameObject prefab = PickPrefab();
            if (!prefab)
                continue;

            GameObject obj = Instantiate(prefab, shipHolder);
            obj.SetActive(false);

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (!rb)
                rb = obj.AddComponent<Rigidbody>();

            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            AmbientNPCShipInstance instance = new AmbientNPCShipInstance
            {
                obj = obj,
                rb = rb
            };

            ships.Add(instance);
        }
    }

    private IEnumerator TrafficRoutine()
    {
        while (true)
        {
            bool burst = Random.value < burstChance;
            int shipsToSpawn = burst
                ? Random.Range(burstShipCountRange.x, burstShipCountRange.y + 1)
                : 1;

            for (int i = 0; i < shipsToSpawn; i++)
            {
                SpawnOneShip();

                float delay = burst
                    ? Random.Range(burstDelayRange.x, burstDelayRange.y)
                    : Random.Range(quietDelayRange.x, quietDelayRange.y);

                yield return new WaitForSeconds(delay);
            }

            yield return new WaitForSeconds(Random.Range(quietDelayRange.x, quietDelayRange.y));
        }
    }

    private void UpdateActiveShips()
    {
        if (!player)
            return;

        float sqrRecycleDistance = recycleDistance * recycleDistance;

        for (int i = 0; i < ships.Count; i++)
        {
            AmbientNPCShipInstance ship = ships[i];

            if (ship == null || ship.obj == null || !ship.obj.activeSelf)
                continue;

            ship.rb.linearVelocity = ship.moveDir * ship.speed;

            if (ship.moveDir != Vector3.zero)
                ship.rb.rotation = Quaternion.LookRotation(ship.moveDir);

            float sqrDistance = (ship.obj.transform.position - player.position).sqrMagnitude;

            if (sqrDistance > sqrRecycleDistance)
            {
                DisableShip(ship);
            }
        }
    }

    private void SpawnOneShip()
    {
        AmbientNPCShipInstance ship = GetInactiveShip();

        if (ship == null)
            return;

        Vector3 playerVelocity = GetPlayerVelocity();
        float playerSpeed = playerVelocity.magnitude;

        bool playerIsFast = playerSpeed >= fastPlayerSpeedThreshold;

        Vector3 playerMoveDir = playerIsFast
            ? playerVelocity.normalized
            : player.forward;

        float forwardBias = playerIsFast
            ? fastForwardSpawnBias
            : slowForwardSpawnBias;

        Vector3 spawnDir = GetBiasedSpawnDirection(playerMoveDir, forwardBias);
        Vector3 spawnPos = player.position + spawnDir * spawnDistance;

        float flybyRadius = Random.Range(flybyRadiusRange.x, flybyRadiusRange.y);

        Vector3 flybyTarget;

        if (playerIsFast)
        {
            // Player is zooming, so ships mostly exist ahead/around the travel corridor.
            flybyTarget = player.position + playerMoveDir * Random.Range(100f, 400f);
            flybyTarget += Random.insideUnitSphere * flybyRadius;
        }
        else
        {
            // Player is observing, so ships feel like random ambient traffic passing nearby.
            flybyTarget = player.position + Random.insideUnitSphere * flybyRadius;
        }

        Vector3 moveDir = (flybyTarget - spawnPos).normalized;

        ship.moveDir = moveDir;
        ship.speed = Random.Range(speedRange.x, speedRange.y);

        ship.obj.transform.position = spawnPos;
        ship.obj.transform.rotation = Quaternion.LookRotation(moveDir);

        if (randomizeScale)
        {
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            ship.obj.transform.localScale = Vector3.one * randomScale;
        }

        ship.rb.position = spawnPos;
        ship.rb.rotation = Quaternion.LookRotation(moveDir);
        ship.rb.linearVelocity = moveDir * ship.speed;
        ship.rb.angularVelocity = Vector3.zero;

        ship.obj.SetActive(true);
    }

    private Vector3 GetBiasedSpawnDirection(Vector3 forwardDir, float forwardBias)
    {
        forwardDir = forwardDir.normalized;

        Vector3 randomDir = Random.onUnitSphere;

        // Blend between fully random sphere spawning and forward-biased spawning.
        Vector3 biasedDir = Vector3.Slerp(randomDir, forwardDir, forwardBias).normalized;

        // Tiny safety fallback.
        if (biasedDir == Vector3.zero)
            biasedDir = Random.onUnitSphere;

        return biasedDir;
    }

    private Vector3 GetPlayerVelocity()
    {
        if (playerRb)
            return playerRb.linearVelocity;

        return Vector3.zero;
    }

    private AmbientNPCShipInstance GetInactiveShip()
    {
        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null && ships[i].obj != null && !ships[i].obj.activeSelf)
                return ships[i];
        }

        return null;
    }

    private GameObject PickPrefab()
    {
        if (shipPrefabs == null || shipPrefabs.Length == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < shipPrefabs.Length; i++)
        {
            totalWeight += Mathf.Max(0f, shipPrefabs[i].weight);
        }

        if (totalWeight <= 0f)
            return shipPrefabs[Random.Range(0, shipPrefabs.Length)].prefab;

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < shipPrefabs.Length; i++)
        {
            roll -= Mathf.Max(0f, shipPrefabs[i].weight);

            if (roll <= 0f)
                return shipPrefabs[i].prefab;
        }

        return shipPrefabs[0].prefab;
    }

    private void DisableShip(AmbientNPCShipInstance ship)
    {
        ship.rb.linearVelocity = Vector3.zero;
        ship.rb.angularVelocity = Vector3.zero;
        ship.obj.SetActive(false);
    }
}

[System.Serializable]
public class AmbientShipEntry
{
    public GameObject prefab;
    public float weight = 1f;
}

[System.Serializable]
public class AmbientNPCShipInstance
{
    public GameObject obj;
    public Rigidbody rb;
    public Vector3 moveDir;
    public float speed;
}