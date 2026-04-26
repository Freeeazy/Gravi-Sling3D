using UnityEngine;
using System.Collections.Generic;

public class SpinManager : MonoBehaviour
{
    [Header("Targets")]
    public List<Transform> targets = new List<Transform>();

    [Header("Speed Settings")]
    public float minSpeed = 5f;
    public float maxSpeed = 25f;

    [Header("Axis Influence")]
    [Range(0f, 1f)] public float verticalBias = 0.4f; // how much Y-axis contributes
    [Range(0f, 1f)] public float horizontalBias = 0.6f; // X/Z contribution

    [Header("Random Offset")]
    public float directionJitter = 0.2f; // slight randomness so it's not perfectly uniform

    private class SpinData
    {
        public Transform target;
        public Vector3 axis;
        public float speed;
    }

    private List<SpinData> spins = new List<SpinData>();

    void Start()
    {
        spins.Clear();

        for (int i = 0; i < targets.Count; i++)
        {
            Transform t = targets[i];
            if (t == null) continue;

            SpinData data = new SpinData();
            data.target = t;

            // Alternate directions
            float dir = (i % 2 == 0) ? 1f : -1f;

            // Alternate vertical influence
            float verticalDir = (i % 3 == 0) ? 1f : -1f;

            // Base axis
            Vector3 axis = new Vector3(
                horizontalBias * dir,
                verticalBias * verticalDir,
                horizontalBias * -dir
            );

            // Add slight randomness
            axis += new Vector3(
                Random.Range(-directionJitter, directionJitter),
                Random.Range(-directionJitter, directionJitter),
                Random.Range(-directionJitter, directionJitter)
            );

            data.axis = axis.normalized;

            // Random speed in range
            data.speed = Random.Range(minSpeed, maxSpeed);

            spins.Add(data);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        foreach (var s in spins)
        {
            if (s.target == null) continue;

            s.target.Rotate(s.axis, s.speed * dt, Space.Self);
        }
    }
}