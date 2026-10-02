using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class CircleOutlineGraphic : MaskableGraphic
{
    [Header("Outline")]
    [Min(0f)]
    [SerializeField] private float thickness = 8f;

    [Header("Quality")]
    [Range(8, 256)]
    [SerializeField] private int segments = 64;

    public float Thickness
    {
        get => thickness;
        set
        {
            thickness = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;

        float outerRadius =
            Mathf.Min(rect.width, rect.height) * 0.5f;

        float innerRadius =
            Mathf.Max(0f, outerRadius - thickness);

        int segmentCount =
            Mathf.Max(8, segments);

        float step =
            Mathf.PI * 2f / segmentCount;

        for (int i = 0; i <= segmentCount; i++)
        {
            float angle =
                i * step;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                );

            Vector2 outerPosition =
                direction * outerRadius;

            Vector2 innerPosition =
                direction * innerRadius;

            Vector2 outerUV =
                new Vector2(
                    outerPosition.x / rect.width + 0.5f,
                    outerPosition.y / rect.height + 0.5f
                );

            Vector2 innerUV =
                new Vector2(
                    innerPosition.x / rect.width + 0.5f,
                    innerPosition.y / rect.height + 0.5f
                );

            vh.AddVert(
                outerPosition,
                color,
                outerUV
            );

            vh.AddVert(
                innerPosition,
                color,
                innerUV
            );
        }

        for (int i = 0; i < segmentCount; i++)
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

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        thickness =
            Mathf.Max(0f, thickness);

        segments =
            Mathf.Max(8, segments);

        SetVerticesDirty();
    }
#endif
}