using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class QuestBeaconScreenPointer : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public StationQuestNavigator navigator;

    [Tooltip("If null, uses GetComponent<Image>().")]
    public Image arrowImage;

    [Header("Ellipse Dead Zone")]
    public Vector2 ellipseFrac = new Vector2(0.42f, 0.30f);
    public Vector2 ellipsePaddingPx = new Vector2(40f, 40f);

    [Header("Arrow Placement")]
    public float edgeOffsetPx = 18f;
    public bool rotateArrow = true;
    public float rotationOffsetDegrees = 0f;

    [Header("Behavior")]
    public bool hideWhenInsideEllipse = true;
    public bool hideWhenBehindCamera = false;

    private RectTransform _rt;
    private RectTransform _canvasRt;
    private Canvas _canvas;

    private void Awake()
    {
        _rt = (RectTransform)transform;

        if (!arrowImage) arrowImage = GetComponent<Image>();
        if (!cam) cam = Camera.main;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas) _canvasRt = _canvas.GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!cam) { cam = Camera.main; if (!cam) return; }
        if (!_canvasRt)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas) _canvasRt = _canvas.GetComponent<RectTransform>();
            if (!_canvasRt) return;
        }

        if (!navigator || !navigator.HasTarget)
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
            if (hideWhenBehindCamera)
            {
                SetVisible(false);
                return;
            }

            // Flip so arrow still points “generally” toward it
            vp.x = 1f - vp.x;
            vp.y = 1f - vp.y;
        }

        // Viewport -> canvas local (centered at 0,0)
        Vector2 canvasSize = _canvasRt.rect.size;
        Vector2 p = new Vector2(
            (vp.x - 0.5f) * canvasSize.x,
            (vp.y - 0.5f) * canvasSize.y
        );

        // Ellipse radii
        Vector2 r = new Vector2(
            canvasSize.x * ellipseFrac.x + ellipsePaddingPx.x,
            canvasSize.y * ellipseFrac.y + ellipsePaddingPx.y
        );

        // inside ellipse test
        float nx = (r.x <= 0.0001f) ? 0f : (p.x / r.x);
        float ny = (r.y <= 0.0001f) ? 0f : (p.y / r.y);
        bool insideEllipse = (nx * nx + ny * ny) <= 1f;

        if (hideWhenInsideEllipse && inFront && insideEllipse)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // clamp to ellipse edge
        Vector2 dir = p;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        float dx = dir.x, dy = dir.y;
        float denom = (dx * dx) / (r.x * r.x + 0.0001f) + (dy * dy) / (r.y * r.y + 0.0001f);
        float t = 1f / Mathf.Sqrt(Mathf.Max(denom, 0.000001f));

        Vector2 edgePoint = dir * t;
        Vector2 arrowPos = edgePoint + dir * edgeOffsetPx;

        _rt.anchoredPosition = arrowPos;

        if (rotateArrow)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rt.localRotation = Quaternion.Euler(0f, 0f, ang + rotationOffsetDegrees);
        }
    }

    private void SetVisible(bool on)
    {
        if (arrowImage) arrowImage.enabled = on;
        else gameObject.SetActive(on);
    }
}
