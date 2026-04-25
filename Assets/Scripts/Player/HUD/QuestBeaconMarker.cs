using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class QuestBeaconMarker : MonoBehaviour
{
    [Header("Refs")]
    public Image beaconImage;
    public TextMeshProUGUI distanceText;

    [Header("Highlight")]
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    private bool _highlighted;

    [Header("Ellipse Dead Zone (match pointer values!)")]
    public Vector2 ellipseFrac = new Vector2(0.42f, 0.30f);
    public Vector2 ellipsePaddingPx = new Vector2(40f, 40f);

    [Header("Distance Cutoffs")]
    public float maxDistance = 1000f;

    [Header("Scaling")]
    public float growMult = 2.0f;
    public float growDistance = 2000f;

    private RectTransform _rt;
    private RectTransform _canvasRt;
    private Canvas _canvas;

    private Camera _cam;
    private Vector3 _targetPos;
    private bool _hasTarget;

    public void SetHighlighted(bool highlighted)
    {
        _highlighted = highlighted;
        ApplyColor();
    }

    private void ApplyColor()
    {
        Color c = _highlighted ? highlightColor : normalColor;

        if (beaconImage)
            beaconImage.color = c;

        if (distanceText)
            distanceText.color = c;
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (!beaconImage) beaconImage = GetComponent<Image>();

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas) _canvasRt = _canvas.GetComponent<RectTransform>();

        ApplyColor();
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
            if (_canvas) _canvasRt = _canvas.GetComponent<RectTransform>();
            if (!_canvasRt) return;
        }

        // World -> viewport
        Vector3 vp = _cam.WorldToViewportPoint(_targetPos);
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

        if (!insideEllipse)
        {
            SetVisible(false);
            return;
        }

        // Distance checks
        float dist = Vector3.Distance(_cam.transform.position, _targetPos);
        if (dist < maxDistance)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        // Place beacon at true on-screen position
        _rt.anchoredPosition = p;

        // Update distance text
        if (distanceText)
            distanceText.text = $"{dist:0} Units";

        // Scale logic (same vibe as yours)
        if (dist <= growDistance && dist > maxDistance)
        {
            float normalized = Mathf.InverseLerp(growDistance, maxDistance, dist);
            float t = Mathf.SmoothStep(0f, 1f, normalized);
            float scale = Mathf.Lerp(1f, growMult, t);
            _rt.localScale = Vector3.one * scale;
        }
        else
        {
            _rt.localScale = Vector3.one;
        }
    }

    private void SetVisible(bool on)
    {
        if (beaconImage) beaconImage.enabled = on;
        if (distanceText) distanceText.enabled = on;
    }
}