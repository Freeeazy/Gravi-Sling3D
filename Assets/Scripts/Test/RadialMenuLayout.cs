using UnityEngine;

public class RadialMenuLayout : MonoBehaviour
{
    [SerializeField]
    private RadialWedgeGraphic[] wedges;

    [SerializeField]
    private float gapDegrees = 3f;

    private void Start()
    {
        RefreshLayout();
    }

    [ContextMenu("Refresh Layout")]
    public void RefreshLayout()
    {
        if (wedges == null || wedges.Length == 0)
            return;

        int count = wedges.Length;

        float anglePerWedge = 360f / count;
        float wedgeAngle = anglePerWedge - gapDegrees;

        for (int i = 0; i < count; i++)
        {
            RadialWedgeGraphic wedge = wedges[i];

            if (wedge == null)
                continue;

            wedge.Angle = wedgeAngle;

            wedge.rectTransform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    i * anglePerWedge
                );
        }
    }
}