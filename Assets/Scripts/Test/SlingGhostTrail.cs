using System.Collections;
using UnityEngine;

public class SlingGhostTrail : MonoBehaviour
{
    public static SlingGhostTrail Instance { get; private set; }

    [Header("Source")]
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private MeshRenderer targetMeshRenderer;

    [Header("Ghost")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private float ghostLifetime = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float startAlpha = 0.45f;

    [Header("Trail Burst")]
    [SerializeField] private float ghostSpawnInterval = 0.05f;
    [SerializeField] private float ghostScaleMultiplier = 2f;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void EmitGhost(int ghostCount)
    {
        if (ghostCount <= 0)
            return;

        StartCoroutine(EmitGhostBurst(ghostCount));
    }

    private IEnumerator EmitGhostBurst(int ghostCount)
    {
        for (int i = 0; i < ghostCount; i++)
        {
            SpawnGhost();

            yield return new WaitForSeconds(ghostSpawnInterval);
        }
    }

    private void SpawnGhost()
    {
        if (targetMeshFilter == null ||
            targetMeshRenderer == null ||
            targetMeshFilter.sharedMesh == null ||
            ghostMaterial == null)
        {
            return;
        }

        GameObject ghost = new GameObject("SlingGhost");

        ghost.transform.position = targetMeshRenderer.transform.position;
        ghost.transform.rotation = targetMeshRenderer.transform.rotation;

        ghost.transform.localScale =
            targetMeshRenderer.transform.lossyScale * ghostScaleMultiplier;

        MeshFilter ghostFilter = ghost.AddComponent<MeshFilter>();
        MeshRenderer ghostRenderer = ghost.AddComponent<MeshRenderer>();

        ghostFilter.sharedMesh = targetMeshFilter.sharedMesh;

        Material runtimeMaterial = new Material(ghostMaterial);
        SetAlpha(runtimeMaterial, startAlpha);

        ghostRenderer.material = runtimeMaterial;

        ghostRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        ghostRenderer.receiveShadows = false;

        StartCoroutine(FadeGhost(ghost, runtimeMaterial));
    }

    private IEnumerator FadeGhost(GameObject ghost, Material material)
    {
        float elapsed = 0f;

        while (elapsed < ghostLifetime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / ghostLifetime);
            float alpha = Mathf.Lerp(startAlpha, 0f, t);

            SetAlpha(material, alpha);

            yield return null;
        }

        Destroy(material);
        Destroy(ghost);
    }

    private void SetAlpha(Material material, float alpha)
    {
        if (material.HasProperty(BaseColorID))
        {
            Color color = material.GetColor(BaseColorID);
            color.a = alpha;
            material.SetColor(BaseColorID, color);
        }
        else if (material.HasProperty(ColorID))
        {
            Color color = material.GetColor(ColorID);
            color.a = alpha;
            material.SetColor(ColorID, color);
        }
    }
}