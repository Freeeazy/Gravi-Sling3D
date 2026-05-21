using UnityEngine;

public class RandomizeChildRotations : MonoBehaviour
{
    [Header("Rotation Settings")]
    public bool rotateX = false;
    public bool rotateY = true;
    public bool rotateZ = false;

    [Tooltip("Smallest random rotation in degrees.")]
    public int minDegrees = 0;

    [Tooltip("Largest random rotation in degrees.")]
    public int maxDegrees = 360;

    [Tooltip("Rotation will snap to this increment. Example: 5 = 0, 5, 10, 15...")]
    public int degreeStep = 5;

    [Tooltip("Use local rotation instead of world rotation.")]
    public bool useLocalRotation = true;

    [ContextMenu("Randomize Direct Child Rotations")]
    private void RandomizeDirectChildRotations()
    {
        if (degreeStep <= 0)
        {
            Debug.LogWarning("Degree step must be greater than 0.");
            return;
        }

        int childCount = transform.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning($"{name} has no direct children to rotate.");
            return;
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);

            Vector3 currentEuler = useLocalRotation
                ? child.localEulerAngles
                : child.eulerAngles;

            float x = rotateX ? GetRandomSteppedAngle() : currentEuler.x;
            float y = rotateY ? GetRandomSteppedAngle() : currentEuler.y;
            float z = rotateZ ? GetRandomSteppedAngle() : currentEuler.z;

            Vector3 newRotation = new Vector3(x, y, z);

            if (useLocalRotation)
                child.localEulerAngles = newRotation;
            else
                child.eulerAngles = newRotation;
        }

        Debug.Log($"Randomized rotations for {childCount} direct children of {name}.");
    }

    private float GetRandomSteppedAngle()
    {
        int minStep = Mathf.CeilToInt(minDegrees / (float)degreeStep);
        int maxStep = Mathf.FloorToInt(maxDegrees / (float)degreeStep);

        int randomStep = Random.Range(minStep, maxStep + 1);

        return randomStep * degreeStep;
    }
}