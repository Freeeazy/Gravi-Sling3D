using System.Collections;
using UnityEngine;

public class ModulePickupObject : MonoBehaviour
{
    [Header("Generated Module")]
    public int moduleTier = 0;
    public bool saveGeneratedAssetInEditor = false;
    public bool generatePreviewModuleOnStart = false;
    public bool showRewardPopupOnCollect = true;

    [Header("Player Detection")]
    public string playerTag = "Player";
    public Transform playerTarget;
    public bool scanForPlayerInRange = true;
    public float attractionRadius = 12f;
    public float collectDistance = 1.25f;

    [Header("Loose Follow")]
    public float followSpeed = 18f;
    public float followResponsiveness = 4f;
    public float maxFollowSpeed = 500f;
    public bool useRandomFollowOffset = true;
    public float followOffsetRadius = 1.5f;
    public float followOffsetRefreshInterval = 0.45f;
    public float followOffsetBlendSpeed = 3f;

    [Header("Collect Animation")]
    public float collectShrinkDuration = 0.18f;
    public AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Visuals")]
    public Renderer[] glowRenderers;
    public TrailRenderer trailRenderer;
    public Color fallbackGlowColor = Color.white;
    public string colorPropertyName = "_Color";
    public string emissionPropertyName = "_EmissionColor";
    public float emissionIntensity = 2f;

    [Header("Debug Gizmos")]
    public bool showGizmos = true;
    public bool showGizmosOnlyWhenSelected = true;
    public Color attractionGizmoColor = new Color(0.25f, 0.75f, 1f, 0.25f);
    public Color collectGizmoColor = new Color(0.4f, 1f, 0.35f, 0.35f);

    private MaterialPropertyBlock propertyBlock;

    private ModuleData previewModule;
    private Vector3 followVelocity;
    private Vector3 currentFollowOffset;
    private Vector3 targetFollowOffset;
    private float nextFollowOffsetRefreshTime;
    private Vector3 startingScale;
    private bool isFollowing;
    private bool isCollecting;
    private bool isCollected;

    private void Awake()
    {
        startingScale = transform.localScale;
        propertyBlock = new MaterialPropertyBlock();
        RandomizeFollowOffset(true);

        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        if (generatePreviewModuleOnStart)
            previewModule = GenerateModule();

        RefreshGlow();
    }

    private void Update()
    {
        if (isCollected || isCollecting)
            return;

        if (playerTarget == null && scanForPlayerInRange)
            TryFindPlayerInRange();

        if (playerTarget == null)
            return;

        float distance = Vector3.Distance(transform.position, playerTarget.position);

        if (!isFollowing && distance <= attractionRadius)
            isFollowing = true;

        if (!isFollowing)
            return;

        FollowPlayer();

        if (distance <= collectDistance)
            StartCoroutine(Collect());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || isCollecting)
            return;

        if (!IsPlayerCollider(other))
            return;

        playerTarget = ResolvePlayerTarget(other);
        isFollowing = true;
    }

    private void FollowPlayer()
    {
        UpdateFollowOffset();

        Vector3 followPoint = playerTarget.position + currentFollowOffset;
        Vector3 toPlayer = followPoint - transform.position;

        if (toPlayer.sqrMagnitude <= 0.0001f)
            return;

        Vector3 desiredVelocity = toPlayer.normalized * followSpeed;
        followVelocity = Vector3.Lerp(
            followVelocity,
            desiredVelocity,
            1f - Mathf.Exp(-followResponsiveness * Time.deltaTime)
        );

        followVelocity = Vector3.ClampMagnitude(followVelocity, maxFollowSpeed);
        transform.position += followVelocity * Time.deltaTime;
    }

    private IEnumerator Collect()
    {
        if (isCollecting)
            yield break;

        isCollecting = true;

        float elapsed = 0f;
        Vector3 collectStartScale = transform.localScale;

        while (elapsed < collectShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = collectShrinkDuration > 0f ? Mathf.Clamp01(elapsed / collectShrinkDuration) : 1f;

            if (playerTarget != null)
                transform.position = Vector3.MoveTowards(transform.position, playerTarget.position, maxFollowSpeed * Time.deltaTime);

            float scale = shrinkCurve != null ? shrinkCurve.Evaluate(t) : 1f - t;
            transform.localScale = collectStartScale * Mathf.Max(0f, scale);

            yield return null;
        }

        GiveModuleToInventory();

        isCollected = true;
        Destroy(gameObject);
    }

    private void GiveModuleToInventory()
    {
        if (ModuleInventoryManager.Instance == null)
        {
            Debug.LogWarning("[ModulePickupObject] Cannot collect module. ModuleInventoryManager.Instance is null.", this);
            return;
        }

        ModuleData module = previewModule != null ? previewModule : GenerateModule();

        if (module == null)
            return;

        if (showRewardPopupOnCollect && RewardPopupUI.Instance != null)
            RewardPopupUI.Instance.ShowModuleReward(module);

        ModuleInventoryManager.Instance.AddModule(module, 1);
        Debug.Log($"[ModulePickupObject] Collected module: {module.moduleName} | Tier {module.moduleTier}", this);
    }

    private ModuleData GenerateModule()
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[ModulePickupObject] Cannot generate module. ModuleGenerator.Instance is null.", this);
            return null;
        }

        return ModuleGenerator.Instance.GenerateRandomModuleByTier(moduleTier, saveGeneratedAssetInEditor);
    }

    private void TryFindPlayerInRange()
    {
        Transform target = null;

        if (SimpleMove.Instance != null)
            target = SimpleMove.Instance.transform;
        else if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

            if (playerObject != null)
                target = playerObject.transform;
        }

        if (target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attractionRadius)
            playerTarget = target;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (string.IsNullOrEmpty(playerTag))
            return true;

        if (other.CompareTag(playerTag))
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag))
            return true;

        return false;
    }

    private Transform ResolvePlayerTarget(Collider other)
    {
        if (SimpleMove.Instance != null)
            return SimpleMove.Instance.transform;

        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.transform;

        return other.transform;
    }

    private void RefreshGlow()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Color glowColor = GetTierColor();

        if (glowRenderers != null)
        {
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                Renderer targetRenderer = glowRenderers[i];

                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(colorPropertyName, glowColor);
                propertyBlock.SetColor(emissionPropertyName, glowColor * emissionIntensity);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        if (trailRenderer != null)
        {
            trailRenderer.startColor = glowColor;
            trailRenderer.endColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        }
    }

    private Color GetTierColor()
    {
        if (ModuleInventoryManager.Instance != null)
            return ModuleInventoryManager.Instance.GetTierColor(moduleTier);

        return fallbackGlowColor;
    }

    private void OnValidate()
    {
        attractionRadius = Mathf.Max(0f, attractionRadius);
        collectDistance = Mathf.Max(0.01f, collectDistance);
        followSpeed = Mathf.Max(0f, followSpeed);
        followResponsiveness = Mathf.Max(0f, followResponsiveness);
        maxFollowSpeed = Mathf.Max(followSpeed, maxFollowSpeed);
        followOffsetRadius = Mathf.Max(0f, followOffsetRadius);
        followOffsetRefreshInterval = Mathf.Max(0f, followOffsetRefreshInterval);
        followOffsetBlendSpeed = Mathf.Max(0f, followOffsetBlendSpeed);
        collectShrinkDuration = Mathf.Max(0f, collectShrinkDuration);
    }
    private void UpdateFollowOffset()
    {
        if (!useRandomFollowOffset || followOffsetRadius <= 0f)
        {
            currentFollowOffset = Vector3.zero;
            targetFollowOffset = Vector3.zero;
            return;
        }

        if (Time.time >= nextFollowOffsetRefreshTime)
            RandomizeFollowOffset(false);

        currentFollowOffset = Vector3.Lerp(
            currentFollowOffset,
            targetFollowOffset,
            1f - Mathf.Exp(-followOffsetBlendSpeed * Time.deltaTime)
        );
    }

    private void RandomizeFollowOffset(bool snapToOffset)
    {
        if (!useRandomFollowOffset || followOffsetRadius <= 0f)
        {
            currentFollowOffset = Vector3.zero;
            targetFollowOffset = Vector3.zero;
            nextFollowOffsetRefreshTime = Time.time + followOffsetRefreshInterval;
            return;
        }

        targetFollowOffset = Random.insideUnitSphere * followOffsetRadius;

        if (snapToOffset)
            currentFollowOffset = targetFollowOffset;

        nextFollowOffsetRefreshTime = Time.time + followOffsetRefreshInterval;
    }
    private void OnDrawGizmos()
    {
        if (showGizmosOnlyWhenSelected)
            return;

        DrawPickupGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawPickupGizmos();
    }

    private void DrawPickupGizmos()
    {
        if (!showGizmos)
            return;

        DrawWireSphere(attractionRadius, attractionGizmoColor);
        DrawWireSphere(collectDistance, collectGizmoColor);
    }

    private void DrawWireSphere(float radius, Color color)
    {
        if (radius <= 0f)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
