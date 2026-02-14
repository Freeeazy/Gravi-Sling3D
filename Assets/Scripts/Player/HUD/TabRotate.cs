using UnityEngine;

public class TabRotate : MonoBehaviour
{
    [Header("Rotation States (Y Axis Only)")]
    [Tooltip("Y rotation when OFF (degrees).")]
    public float offY = 0f;

    [Tooltip("Y rotation when ON (degrees).")]
    public float onY = 90f;

    [Header("Animation")]
    [Tooltip("How long the rotation takes (seconds).")]
    public float duration = 0.35f;

    [Tooltip("Curve for easing (0->1 time).")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Tab;

    private bool isOn = false;
    private bool isAnimating = false;
    private float animTimer = 0f;

    private float startY;
    private float targetY;

    private void Start()
    {
        // Initialize to OFF state
        SetRotationImmediate(offY);
    }

    private void Update()
    {
        if (!UIBlock.IsUIOpen && Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }

        if (isAnimating)
        {
            AnimateRotation();
        }
    }

    private void Toggle()
    {
        isOn = !isOn;

        startY = transform.localEulerAngles.y;
        targetY = isOn ? onY : offY;

        animTimer = 0f;
        isAnimating = true;
    }

    private void AnimateRotation()
    {
        animTimer += Time.deltaTime;
        float t = Mathf.Clamp01(animTimer / Mathf.Max(duration, 0.0001f));

        float curvedT = easeCurve.Evaluate(t);
        float newY = Mathf.LerpAngle(startY, targetY, curvedT);

        Vector3 euler = transform.localEulerAngles;
        euler.y = newY;
        transform.localEulerAngles = euler;

        if (t >= 1f)
        {
            isAnimating = false;
        }
    }

    private void SetRotationImmediate(float y)
    {
        Vector3 euler = transform.localEulerAngles;
        euler.y = y;
        transform.localEulerAngles = euler;
    }
}
