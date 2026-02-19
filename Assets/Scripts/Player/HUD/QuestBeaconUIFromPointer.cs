using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class QuestBeaconUIFromPointer : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public StationQuestNavigator navigator;

    [Tooltip("Beacon circle image")]
    public Image beaconImage;

    [Tooltip("Distance text")]
    public TextMeshProUGUI distanceText;

    [Header("Ellipse Dead Zone (match pointer values!)")]
    public Vector2 ellipseFrac = new Vector2(0.42f, 0.30f);
    public Vector2 ellipsePaddingPx = new Vector2(40f, 40f);

    private RectTransform _rt;
    private RectTransform _canvasRt;

    private void Awake()
    {
        _rt = (RectTransform)transform;

        if (!cam) cam = Camera.main;
        if (!beaconImage) beaconImage = GetComponent<Image>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas) _canvasRt = canvas.GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!cam || !navigator || !navigator.HasTarget || !_canvasRt)
        {
            SetVisible(false);
            return;
        }

        Vector3 targetPos = navigator.TargetWorldPos;

        // World -> viewport
        Vector3 vp = cam.WorldToViewportPoint(targetPos);
        bool inFront = vp.z > 0f;

        if (!inFront)
        {
            SetVisible(false);
            return;
        }

        // Viewport -> canvas local
        Vector2 canvasSize = _canvasRt.rect.size;
        Vector2 p = new Vector2(
            (vp.x - 0.5f) * canvasSize.x,
            (vp.y - 0.5f) * canvasSize.y
        );

        // Same ellipse math as pointer
        Vector2 r = new Vector2(
            canvasSize.x * ellipseFrac.x + ellipsePaddingPx.x,
            canvasSize.y * ellipseFrac.y + ellipsePaddingPx.y
        );

        float nx = (r.x <= 0.0001f) ? 0f : (p.x / r.x);
        float ny = (r.y <= 0.0001f) ? 0f : (p.y / r.y);
        bool insideEllipse = (nx * nx + ny * ny) <= 1f;

        // INVERSE OF POINTER
        if (insideEllipse)
        {
            SetVisible(true);

            // Place beacon at true on-screen position
            _rt.anchoredPosition = p;

            // Update distance
            if (distanceText)
            {
                float dist = Vector3.Distance(cam.transform.position, targetPos);
                distanceText.text = dist >= 1000f ? $"{dist * 0.001f:0.0} Units" : $"{dist:0} m";
            }
        }
        else
        {
            SetVisible(false);
        }
    }

    private void SetVisible(bool on)
    {
        if (beaconImage) beaconImage.enabled = on;
        if (distanceText) distanceText.enabled = on;
    }
}
