using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class QuestBeaconScreenPointer : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("If null, uses Camera.main.")]
    public Camera cam;

    [Tooltip("The world-space quest beacon transform. If null, we will find by name at runtime.")]
    public Transform worldBeacon;

    [Tooltip("If null, uses GetComponent<Image>().")]
    public Image arrowImage;

    [Header("Find Beacon")]
    [Tooltip("Matches the name you set in StationQuestNavigator.EnsureBeacon().")]
    public string beaconName = "StationQuestBeacon";

    [Tooltip("How often (seconds) we try to find the beacon when missing.")]
    public float findInterval = 0.25f;

    [Header("Ellipse Dead Zone")]
    [Tooltip("Dead-zone ellipse size as a fraction of the canvas size. (0.5,0.5) = half width/height.")]
    public Vector2 ellipseFrac = new Vector2(0.42f, 0.30f);

    [Tooltip("Extra padding (pixels) added to the dead-zone ellipse radii.")]
    public Vector2 ellipsePaddingPx = new Vector2(40f, 40f);

    [Header("Arrow Placement")]
    [Tooltip("How far OUTSIDE the dead-zone edge the arrow sits (pixels).")]
    public float edgeOffsetPx = 18f;

    [Tooltip("Rotate arrow to point toward the beacon.")]
    public bool rotateArrow = true;

    [Tooltip("Additional rotation offset (degrees). Use 0 if your arrow sprite points RIGHT by default, or 90 if it points UP, etc.")]
    public float rotationOffsetDegrees = 0f;

    [Header("Behavior")]
    [Tooltip("Hide arrow if beacon is in front of camera and inside the dead-zone ellipse.")]
    public bool hideWhenInsideEllipse = true;

    [Tooltip("Hide arrow if beacon is behind the camera.")]
    public bool hideWhenBehindCamera = false;

    private RectTransform _rt;
    private RectTransform _canvasRt;
    private float _nextFindTime;

    private void Awake()
    {
        _rt = (RectTransform)transform;

        if (!arrowImage) arrowImage = GetComponent<Image>();
        if (!cam) cam = Camera.main;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas) _canvasRt = canvas.GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (!cam)
        {
            cam = Camera.main;
            if (!cam) return;
        }

        if (!_canvasRt)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas) _canvasRt = canvas.GetComponent<RectTransform>();
            if (!_canvasRt) return;
        }

        // Find/cached world beacon
        if (!worldBeacon)
        {
            TryFindBeacon();
            SetVisible(false);
            return;
        }

        // Convert beacon world -> viewport (0..1)
        Vector3 vp = cam.WorldToViewportPoint(worldBeacon.position);

        bool inFront = vp.z > 0f;

        if (!inFront)
        {
            if (hideWhenBehindCamera)
            {
                SetVisible(false);
                return;
            }

            // Flip direction when behind so arrow still points “generally” toward it
            vp.x = 1f - vp.x;
            vp.y = 1f - vp.y;
        }

        // Convert viewport -> canvas local point (centered at 0,0)
        Vector2 canvasSize = _canvasRt.rect.size;
        Vector2 p = new Vector2(
            (vp.x - 0.5f) * canvasSize.x,
            (vp.y - 0.5f) * canvasSize.y
        );

        // Ellipse dead-zone radii
        Vector2 r = new Vector2(
            canvasSize.x * ellipseFrac.x + ellipsePaddingPx.x,
            canvasSize.y * ellipseFrac.y + ellipsePaddingPx.y
        );

        // Check if inside ellipse: (x/rx)^2 + (y/ry)^2 <= 1
        float nx = (r.x <= 0.0001f) ? 0f : (p.x / r.x);
        float ny = (r.y <= 0.0001f) ? 0f : (p.y / r.y);
        float ellipseVal = nx * nx + ny * ny;

        bool insideEllipse = ellipseVal <= 1f;

        if (hideWhenInsideEllipse && inFront && insideEllipse)
        {
            SetVisible(false);
            return;
        }

        // Show arrow and clamp to ellipse edge
        SetVisible(true);

        // Direction in canvas-space from center toward beacon point
        Vector2 dir = p;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        // Find intersection with ellipse boundary along dir:
        // scale t so that (t*dx/rx)^2 + (t*dy/ry)^2 = 1  -> t = 1 / sqrt((dx/rx)^2 + (dy/ry)^2)
        float dx = dir.x;
        float dy = dir.y;
        float denom = (dx * dx) / (r.x * r.x + 0.0001f) + (dy * dy) / (r.y * r.y + 0.0001f);
        float t = 1f / Mathf.Sqrt(Mathf.Max(denom, 0.000001f));

        Vector2 edgePoint = dir * t;

        // Put arrow slightly outside the ellipse edge
        Vector2 arrowPos = edgePoint + dir * edgeOffsetPx;

        _rt.anchoredPosition = arrowPos;

        if (rotateArrow)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rt.localRotation = Quaternion.Euler(0f, 0f, ang + rotationOffsetDegrees);
        }
    }

    private void TryFindBeacon()
    {
        if (Time.unscaledTime < _nextFindTime) return;
        _nextFindTime = Time.unscaledTime + Mathf.Max(0.05f, findInterval);

        // Cheap global lookup. Fine for now.
        var go = GameObject.Find(beaconName);
        if (go) worldBeacon = go.transform;
    }

    private void SetVisible(bool on)
    {
        if (arrowImage) arrowImage.enabled = on;
        else gameObject.SetActive(on);
    }
}
