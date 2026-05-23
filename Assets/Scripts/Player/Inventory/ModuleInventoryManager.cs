using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
 
public class ModuleInventoryManager : MonoBehaviour
{
    public static ModuleInventoryManager Instance { get; private set; }

    [System.Serializable]
    public class ModulePrefabEntry
    {
        public string displayName;
        public ModuleData moduleData;
        public GameObject prefab;
    }

    [Header("Inventory Setup")]
    public List<ModulePrefabEntry> modulePrefabs = new List<ModulePrefabEntry>();
    public GameObject emptySlotPrefab;
    public int totalSlots = 30;

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
        if (Input.GetKeyDown(KeyCode.Keypad1)) TrySpawnModule(0);
        if (Input.GetKeyDown(KeyCode.Keypad2)) TrySpawnModule(1);
        if (Input.GetKeyDown(KeyCode.Keypad3)) TrySpawnModule(2);
        if (Input.GetKeyDown(KeyCode.Keypad4)) TrySpawnModule(3);
        if (Input.GetKeyDown(KeyCode.Keypad5)) TrySpawnModule(4);
        if (Input.GetKeyDown(KeyCode.Keypad6)) TrySpawnModule(5);
        if (Input.GetKeyDown(KeyCode.Keypad7)) TrySpawnModule(6);
        if (Input.GetKeyDown(KeyCode.Keypad8)) TrySpawnModule(7);
        if (Input.GetKeyDown(KeyCode.Keypad9)) TrySpawnModule(8);
    }
    private void TrySpawnModule(int index)
    {
        if (modulePrefabs.Count > index && modulePrefabs[index].moduleData != null)
        {
            AddModule(modulePrefabs[index].moduleData, 1);
            Debug.Log($"[DEBUG] Added module: {modulePrefabs[index].displayName}");
        }
        else
        {
            Debug.LogWarning($"[DEBUG] ModulePrefab index {index} is missing.");
        }
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

            if (amount <= 0)
                continue;

            ModulePrefabEntry entry = GetPrefabEntry(data);

            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"No prefab entry found for module: {data.moduleName}");
                continue;
            }

            GameObject newSlotObject = Instantiate(entry.prefab, moduleListParent);

            ModuleButtonUI newSlot = newSlotObject.GetComponent<ModuleButtonUI>();

            if (newSlot == null)
            {
                Debug.LogWarning($"Spawned prefab for {data.moduleName} does not have a ModuleButtonUI component.");
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

    private ModulePrefabEntry GetPrefabEntry(ModuleData moduleData)
    {
        for (int i = 0; i < modulePrefabs.Count; i++)
        {
            if (modulePrefabs[i].moduleData == moduleData)
                return modulePrefabs[i];
        }

        return null;
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
    public bool TryGiveModuleByIndex(int index, int amount = 1)
    {
        if (index < 0 || index >= modulePrefabs.Count)
        {
            Debug.LogWarning($"[Inventory] Invalid module reward index: {index}");
            return false;
        }

        ModulePrefabEntry entry = modulePrefabs[index];

        if (entry == null || entry.moduleData == null)
        {
            Debug.LogWarning($"[Inventory] Module reward index {index} is missing ModuleData.");
            return false;
        }

        AddModule(entry.moduleData, amount);

        Debug.Log($"[Inventory] Reward added: {entry.displayName} x{amount}");

        //if (RewardPopupUI.Instance != null)
        //    RewardPopupUI.Instance.ShowModuleReward(entry.displayName);

        return true;
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