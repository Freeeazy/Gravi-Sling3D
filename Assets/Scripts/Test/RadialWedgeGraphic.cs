using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadialWedgeGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [Header("Shape")]
    [Range(0f, 360f)]
    [SerializeField] private float angle = 60f;

    [Tooltip("Angular width of the wedge along the INNER radius.")]
    [Range(0f, 360f)]
    [SerializeField] private float innerAngle = 55f;

    [Range(0f, 1f)]
    [SerializeField] private float innerRadius = 0.45f;

    [Header("Quality")]
    [Tooltip("Approximately how many degrees each curved segment covers.")]
    [Range(1f, 20f)]
    [SerializeField] private float degreesPerSegment = 5f;


    public float Angle
    {
        get => angle;
        set
        {
            angle = Mathf.Clamp(value, 0f, 360f);
            SetVerticesDirty();
        }
    }

    public float InnerAngle
    {
        get => innerAngle;
        set
        {
            innerAngle = Mathf.Clamp(value, 0f, 360f);
            SetVerticesDirty();
        }
    }

    public float InnerRadius
    {
        get => innerRadius;
        set
        {
            innerRadius = Mathf.Clamp01(value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        float outerRadius =
            Mathf.Min(rect.width, rect.height) * 0.5f;

        float inner =
            outerRadius * innerRadius;

        int segments = Mathf.Max(
            2,
            Mathf.CeilToInt(
                Mathf.Max(angle, innerAngle) /
                degreesPerSegment
            )
        );

        float outerStartAngle = -angle * 0.5f;
        float innerStartAngle = -innerAngle * 0.5f;

        float outerStep = angle / segments;
        float innerStep = innerAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float outerCurrentAngle =
                outerStartAngle +
                outerStep * i;

            float innerCurrentAngle =
                innerStartAngle +
                innerStep * i;

            float outerRadians =
                outerCurrentAngle *
                Mathf.Deg2Rad;

            float innerRadians =
                innerCurrentAngle *
                Mathf.Deg2Rad;

            Vector2 outerDirection =
                new Vector2(
                    Mathf.Cos(outerRadians),
                    Mathf.Sin(outerRadians)
                );

            Vector2 innerDirection =
                new Vector2(
                    Mathf.Cos(innerRadians),
                    Mathf.Sin(innerRadians)
                );

            Vector2 outerPosition =
                outerDirection * outerRadius;

            Vector2 innerPosition =
                innerDirection * inner;

            vh.AddVert(
                outerPosition,
                color,
                Vector2.zero
            );

            vh.AddVert(
                innerPosition,
                color,
                Vector2.zero
            );
        }

        for (int i = 0; i < segments; i++)
        {
            int outerA = i * 2;
            int innerA = outerA + 1;

            int outerB = outerA + 2;
            int innerB = outerA + 3;

            vh.AddTriangle(
                outerA,
                outerB,
                innerB
            );

            vh.AddTriangle(
                outerA,
                innerB,
                innerA
            );
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                eventCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rectTransform.rect;

        float outerRadius =
            Mathf.Min(rect.width, rect.height) * 0.5f;

        float inner =
            outerRadius * innerRadius;

        float distance =
            localPoint.magnitude;

        // Reject anything outside the donut-shaped radial region.
        if (distance < inner || distance > outerRadius)
            return false;

        // Angle of cursor around THIS wedge's local center.
        float pointAngle =
            Mathf.Atan2(
                localPoint.y,
                localPoint.x
            ) * Mathf.Rad2Deg;

        // Atan2 already produces -180 -> +180,
        // so absolute value gives us distance from wedge center.
        float absoluteAngle =
            Mathf.Abs(pointAngle);

        // Because the inner and outer angular widths can differ,
        // interpolate the allowed width based on radial position.
        float radialT =
            Mathf.InverseLerp(
                inner,
                outerRadius,
                distance
            );

        float allowedAngle =
            Mathf.Lerp(
                innerAngle,
                angle,
                radialT
            );

        float halfAllowedAngle =
            allowedAngle * 0.5f;

        return absoluteAngle <= halfAllowedAngle;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        innerRadius =
            Mathf.Clamp01(innerRadius);

        angle =
            Mathf.Clamp(angle, 0f, 360f);

        innerAngle =
            Mathf.Clamp(innerAngle, 0f, 360f);

        SetVerticesDirty();
    }
#endif
}