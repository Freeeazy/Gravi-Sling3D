using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
 
public class ModuleInventoryManager : MonoBehaviour
{
    public static ModuleInventoryManager Instance { get; private set; }

    [Header("Inventory Setup")]
    public GameObject blankSlotPrefab;
    public GameObject emptySlotPrefab;
    public int totalSlots = 30;

    [Header("Random Module Debug")]
    public string[] debugModuleTypes =
{
        "Engine",
        "Battery",
        "Orbit"
    };

    public int minDebugTier = 0;
    public int maxDebugTier = 6;
    public bool saveGeneratedDebugAssetsInEditor = true;

    [Header("Currency")]
    public float credits = 0f;
    public TMP_Text creditsText;

    [Header("Currency Animation")]
    public float creditTickDelay = 0.02f;
    public float creditAnimationDuration = 0.75f;

    [Header("Inventory Counter")]
    public TMP_Text inventoryCounterText;

    [Header("UI Parent")]
    public Transform moduleListParent;

    [Header("Runtime Drag References")]
    public RectTransform dragCanvas;
    public ScrollRect parentScrollRect;
    public Camera uiCamera;

    [Header("Module Tier Colors")]
    public Color tier0Color = new Color(0.45f, 0.45f, 0.45f, 1f); // Gray
    public Color tier1Color = Color.white;                        // White
    public Color tier2Color = new Color(0.3f, 1f, 0.3f, 1f);       // Green
    public Color tier3Color = new Color(0.3f, 0.6f, 1f, 1f);       // Blue
    public Color tier4Color = new Color(1f, 0.75f, 0.15f, 1f);     // Orange / Yellow
    public Color tier5Color = new Color(1f, 0.2f, 0.2f, 1f);       // Red
    public Color tier6Color = new Color(0.75f, 0.3f, 1f, 1f);      // Purple

    private readonly Dictionary<ModuleData, int> ownedModules = new Dictionary<ModuleData, int>();

    private float _displayedCredits = 0f;
    private float _pendingCreditGain = 0f;
    private Coroutine _creditsRoutine;

    private void Awake()
    {
        Instance = this;

        if (moduleListParent == null)
            moduleListParent = transform;

        RefreshInventoryUI();

        _displayedCredits = credits;
        UpdateCreditsText();
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Keypad0))
            GenerateRandomDebugModule();
    }
    private void GenerateRandomDebugModule()
    {
        if (ModuleGenerator.Instance == null)
        {
            Debug.LogWarning("[Inventory] Cannot generate module. ModuleGenerator.Instance is null.");
            return;
        }

        if (debugModuleTypes == null || debugModuleTypes.Length == 0)
        {
            Debug.LogWarning("[Inventory] Cannot generate module. No debug module types assigned.");
            return;
        }

        string randomType = debugModuleTypes[Random.Range(0, debugModuleTypes.Length)];
        int randomTier = Random.Range(minDebugTier, maxDebugTier + 1);

        ModuleData generatedModule = ModuleGenerator.Instance.GenerateModule(
            randomType,
            randomTier,
            saveGeneratedDebugAssetsInEditor
        );

        if (generatedModule == null)
        {
            Debug.LogWarning($"[Inventory] Module generation failed. Type: {randomType}, Tier: {randomTier}");
            return;
        }

        AddModule(generatedModule, 1);

        Debug.Log($"[Inventory] Generated random module: {generatedModule.moduleName} | Type: {generatedModule.moduleType} | Tier: {generatedModule.moduleTier}");
    }

    public void AddModule(ModuleData moduleData, int amount = 1)
    {
        if (moduleData == null || amount <= 0)
            return;

        if (ownedModules.ContainsKey(moduleData))
            ownedModules[moduleData] += amount;
        else
            ownedModules.Add(moduleData, amount);

        RefreshInventoryUI();
    }

    public void RemoveModule(ModuleData moduleData, int amount = 1)
    {
        if (moduleData == null || amount <= 0)
            return;

        if (!ownedModules.ContainsKey(moduleData))
            return;

        ownedModules[moduleData] -= amount;

        if (ownedModules[moduleData] <= 0)
            ownedModules.Remove(moduleData);

        RefreshInventoryUI();
    }

    public int GetAmount(ModuleData moduleData)
    {
        if (moduleData == null)
            return 0;

        return ownedModules.TryGetValue(moduleData, out int amount) ? amount : 0;
    }

    public void RefreshInventoryUI()
    {
        ClearCurrentSlots();

        int usedSlots = 0;

        foreach (var pair in ownedModules)
        {
            ModuleData data = pair.Key;
            int amount = pair.Value;

            if (data == null || amount <= 0)
                continue;

            if (blankSlotPrefab == null)
            {
                Debug.LogWarning("[Inventory] blankSlotPrefab is missing.");
                continue;
            }

            GameObject newSlotObject = Instantiate(blankSlotPrefab, moduleListParent);

            ModuleButtonUI newSlot = newSlotObject.GetComponent<ModuleButtonUI>();

            if (newSlot == null)
            {
                Debug.LogWarning($"[Inventory] blankSlotPrefab does not have a ModuleButtonUI component.");
                continue;
            }

            newSlot.moduleData = data;
            newSlot.dragCanvas = dragCanvas;
            newSlot.parentScrollRect = parentScrollRect;
            newSlot.uiCamera = uiCamera;
            newSlot.SetAmount(amount);

            usedSlots++;
        }

        UpdateInventoryCounter();

        int emptySlotsToCreate = Mathf.Max(0, totalSlots - usedSlots);

        for (int i = 0; i < emptySlotsToCreate; i++)
        {
            if (emptySlotPrefab != null)
                Instantiate(emptySlotPrefab, moduleListParent);
        }
    }

    private void ClearCurrentSlots()
    {
        for (int i = moduleListParent.childCount - 1; i >= 0; i--)
        {
            Destroy(moduleListParent.GetChild(i).gameObject);
        }
    }
    private void UpdateInventoryCounter()
    {
        if (inventoryCounterText == null)
            return;

        int uniqueCount = ownedModules.Count;
        inventoryCounterText.text = $"{uniqueCount}/{totalSlots}";
    }

    public void GiveXCredits(float creditsToGive)
    {
        if (creditsToGive <= 0f)
            return;

        credits += creditsToGive;
        _pendingCreditGain += creditsToGive;

        if (_creditsRoutine != null)
            StopCoroutine(_creditsRoutine);

        _creditsRoutine = StartCoroutine(AnimateCreditsText());

        Debug.Log($"[Inventory] Credits added: {creditsToGive}. Total credits: {credits}");
    }
    private void UpdateCreditsText()
    {
        if (creditsText == null)
            return;

        int shownCredits = Mathf.RoundToInt(_displayedCredits);
        int shownPending = Mathf.RoundToInt(_pendingCreditGain);

        if (shownPending > 0)
            creditsText.text = $"<color=#4DFF88>{shownPending}</color> + {shownCredits} credits";
        else
            creditsText.text = $"{shownCredits} credits";
    }
    private IEnumerator AnimateCreditsText()
    {
        float startCredits = _displayedCredits;
        float targetCredits = credits;

        float startPending = _pendingCreditGain;

        float elapsed = 0f;

        while (elapsed < creditAnimationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / creditAnimationDuration);

            _displayedCredits = Mathf.Lerp(startCredits, targetCredits, t);
            _pendingCreditGain = Mathf.Lerp(startPending, 0f, t);

            UpdateCreditsText();

            yield return null;
        }

        _displayedCredits = targetCredits;
        _pendingCreditGain = 0f;

        UpdateCreditsText();

        _creditsRoutine = null;
    }
    public Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 0:
                return tier0Color;
            case 1:
                return tier1Color;
            case 2:
                return tier2Color;
            case 3:
                return tier3Color;
            case 4:
                return tier4Color;
            case 5:
                return tier5Color;
            case 6:
                return tier6Color;
            default:
                return tier0Color;
        }
    }
}