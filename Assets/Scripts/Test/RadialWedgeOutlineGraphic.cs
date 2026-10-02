using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadialWedgeOutlineGraphic : MaskableGraphic
{
    [Header("Source")]
    [SerializeField]
    private RadialWedgeGraphic source;

    [Header("Outline")]
    [Min(0f)]
    [SerializeField]
    private float thickness = 4f;

    [Header("Quality")]
    [Range(1f, 20f)]
    [SerializeField]
    private float degreesPerSegment = 5f;


    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (source == null)
            return;

        Rect rect = rectTransform.rect;

        float outerRadius =
            Mathf.Min(
                rect.width,
                rect.height
            ) * 0.5f;

        float innerRadius =
            outerRadius *
            source.InnerRadius;

        float outerAngle =
            source.Angle;

        float innerAngle =
            source.InnerAngle;


        // ------------------------------------
        // OUTER ARC
        // ------------------------------------

        BuildArcStrip(
            vh,
            outerRadius,
            Mathf.Max(
                innerRadius,
                outerRadius - thickness
            ),
            outerAngle,
            outerAngle,
            color
        );


        // ------------------------------------
        // INNER ARC
        // ------------------------------------

        BuildArcStrip(
            vh,
            Mathf.Min(
                outerRadius,
                innerRadius + thickness
            ),
            innerRadius,
            innerAngle,
            innerAngle,
            color
        );


        // ------------------------------------
        // SIDE EDGES
        // ------------------------------------

        BuildSide(
            vh,
            outerRadius,
            innerRadius,
            -outerAngle * 0.5f,
            -innerAngle * 0.5f,
            thickness,
            true
        );

        BuildSide(
            vh,
            outerRadius,
            innerRadius,
            outerAngle * 0.5f,
            innerAngle * 0.5f,
            thickness,
            false
        );
    }


    private void BuildArcStrip(
        VertexHelper vh,
        float outerRadius,
        float innerRadius,
        float outerAngle,
        float innerAngle,
        Color vertexColor
    )
    {
        int segments =
            Mathf.Max(
                2,
                Mathf.CeilToInt(
                    Mathf.Max(
                        outerAngle,
                        innerAngle
                    ) /
                    degreesPerSegment
                )
            );

        float outerStart =
            -outerAngle * 0.5f;

        float innerStart =
            -innerAngle * 0.5f;

        float outerStep =
            outerAngle / segments;

        float innerStep =
            innerAngle / segments;

        int startVertex =
            vh.currentVertCount;

        for (int i = 0; i <= segments; i++)
        {
            float outerDegrees =
                outerStart +
                outerStep * i;

            float innerDegrees =
                innerStart +
                innerStep * i;

            float outerRadians =
                outerDegrees *
                Mathf.Deg2Rad;

            float innerRadians =
                innerDegrees *
                Mathf.Deg2Rad;

            Vector2 outerPosition =
                new Vector2(
                    Mathf.Cos(outerRadians),
                    Mathf.Sin(outerRadians)
                ) * outerRadius;

            Vector2 innerPosition =
                new Vector2(
                    Mathf.Cos(innerRadians),
                    Mathf.Sin(innerRadians)
                ) * innerRadius;

            vh.AddVert(
                outerPosition,
                vertexColor,
                Vector2.zero
            );

            vh.AddVert(
                innerPosition,
                vertexColor,
                Vector2.zero
            );
        }

        for (int i = 0; i < segments; i++)
        {
            int a =
                startVertex +
                i * 2;

            int b = a + 1;
            int c = a + 2;
            int d = a + 3;

            vh.AddTriangle(
                a,
                c,
                d
            );

            vh.AddTriangle(
                a,
                d,
                b
            );
        }
    }


    private void BuildSide(
        VertexHelper vh,
        float outerRadius,
        float innerRadius,
        float outerDegrees,
        float innerDegrees,
        float width,
        bool leftSide
    )
    {
        float outerRadians =
            outerDegrees *
            Mathf.Deg2Rad;

        float innerRadians =
            innerDegrees *
            Mathf.Deg2Rad;

        Vector2 outerPoint =
            new Vector2(
                Mathf.Cos(outerRadians),
                Mathf.Sin(outerRadians)
            ) * outerRadius;

        Vector2 innerPoint =
            new Vector2(
                Mathf.Cos(innerRadians),
                Mathf.Sin(innerRadians)
            ) * innerRadius;


        Vector2 edgeDirection =
            (innerPoint - outerPoint)
            .normalized;

        Vector2 normal =
            new Vector2(
                -edgeDirection.y,
                edgeDirection.x
            );

        Vector2 centerDirection =
            (
                Vector2.zero -
                (
                    outerPoint +
                    innerPoint
                ) * 0.5f
            ).normalized;

        if (
            Vector2.Dot(
                normal,
                centerDirection
            ) < 0f
        )
        {
            normal = -normal;
        }

        Vector2 outerInset =
            outerPoint +
            normal * width;

        Vector2 innerInset =
            innerPoint +
            normal * width;

        int start =
            vh.currentVertCount;

        vh.AddVert(
            outerPoint,
            color,
            Vector2.zero
        );

        vh.AddVert(
            innerPoint,
            color,
            Vector2.zero
        );

        vh.AddVert(
            outerInset,
            color,
            Vector2.zero
        );

        vh.AddVert(
            innerInset,
            color,
            Vector2.zero
        );

        vh.AddTriangle(
            start,
            start + 1,
            start + 3
        );

        vh.AddTriangle(
            start,
            start + 3,
            start + 2
        );
    }


#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        thickness =
            Mathf.Max(
                0f,
                thickness
            );

        SetVerticesDirty();
    }
#endif
}