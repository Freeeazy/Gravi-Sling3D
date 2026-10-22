
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class QuestPointerMarker : MonoBehaviour
{
    [Header("Refs")]
    public Image arrowImage;

    [Header("Optional Distance Label")]
    [Tooltip("Optional child TMP text. Leave empty for the original arrow-only behavior.")]
    public TextMeshProUGUI distanceText;
    [Tooltip("Canvas units inward from the arrow. Keeps the label away from the screen edge.")]
    public float distanceTextInset = 40f;
    public bool keepDistanceTextUpright = true;

    [Header("Highlight Materials")]
    public Material normalMaterial;
    public Material highlightMaterial;

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

    private Camera _cam;
    private Vector3 _targetPos;
    private bool _hasTarget;
    private bool _visible;
    private bool _distanceLabelVisible = true;

    public bool IsPointerVisible => isActiveAndEnabled && _visible;
    public Vector2 CanvasPosition => _rt ? _rt.anchoredPosition : Vector2.zero;

    public void SetDistanceLabelVisible(bool visible)
    {
        _distanceLabelVisible = visible;
        if (distanceText) distanceText.enabled = IsPointerVisible && visible;
    }

    private void OnDisable()
    {
        _visible = false;
        _distanceLabelVisible = true;
        if (distanceText) distanceText.enabled = false;
    }

    private bool _highlighted;

    public void SetHighlighted(bool highlighted)
    {
        _highlighted = highlighted;
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        if (arrowImage)
            arrowImage.material = _highlighted
                ? highlightMaterial
                : normalMaterial;
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;

        if (!arrowImage)
            arrowImage = GetComponent<Image>();

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas)
            _canvasRt = _canvas.GetComponent<RectTransform>();

        ApplyMaterial();
    }

    public void SetTarget(Camera cam, Vector3 targetPos, bool hasTarget)
    {
        _cam = cam;
        _targetPos = targetPos;
        _hasTarget = hasTarget;
    }

    public void SetSlotActive(bool on)
    {
        if (!on)
            SetHighlighted(false);

        gameObject.SetActive(on);
    }

    private void Update()
    {
        if (!_hasTarget || !_cam)
        {
            SetVisible(false);
            return;
        }

        if (!_canvasRt)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas)
                _canvasRt = _canvas.GetComponent<RectTransform>();

            if (!_canvasRt) return;
        }

        // World -> viewport
        Vector3 vp = _cam.WorldToViewportPoint(_targetPos);
        bool inFront = vp.z > 0f;

        if (!inFront)
        {
            if (hideWhenBehindCamera)
            {
                SetVisible(false);
                return;
            }

            // Flip so arrow still points generally toward target
            vp.x = 1f - vp.x;
            vp.y = 1f - vp.y;
        }

        // Viewport -> canvas local
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

        float nx = (r.x <= 0.0001f) ? 0f : (p.x / r.x);
        float ny = (r.y <= 0.0001f) ? 0f : (p.y / r.y);

        bool insideEllipse = (nx * nx + ny * ny) <= 1f;

        if (hideWhenInsideEllipse && inFront && insideEllipse)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // Clamp to ellipse edge
        Vector2 dir = p;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.right;

        dir.Normalize();

        float dx = dir.x;
        float dy = dir.y;

        float denom =
            (dx * dx) / (r.x * r.x + 0.0001f) +
            (dy * dy) / (r.y * r.y + 0.0001f);

        float t = 1f / Mathf.Sqrt(
            Mathf.Max(denom, 0.000001f)
        );

        Vector2 edgePoint = dir * t;
        Vector2 arrowPos = edgePoint + dir * edgeOffsetPx;

        _rt.anchoredPosition = arrowPos;

        if (rotateArrow)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            _rt.localRotation = Quaternion.Euler(
                0f,
                0f,
                ang + rotationOffsetDegrees
            );
        }

        if (distanceText)
        {
            float dist = Vector3.Distance(_cam.transform.position, _targetPos);
            distanceText.text = $"{dist:0} Units";
            // Position in canvas space so rotating the arrow does not swing its text.
            RectTransform textRt = distanceText.rectTransform;
            if (textRt != _rt)
            {
                textRt.position = _canvasRt.TransformPoint(arrowPos - dir * distanceTextInset);
                if (keepDistanceTextUpright) textRt.rotation = _canvasRt.rotation;
            }
        }
    }

    private void SetVisible(bool on)
    {
        _visible = on;
        if (distanceText) distanceText.enabled = on && _distanceLabelVisible;
        if (arrowImage)
            arrowImage.enabled = on;
        else
            gameObject.SetActive(on);
    }
}
