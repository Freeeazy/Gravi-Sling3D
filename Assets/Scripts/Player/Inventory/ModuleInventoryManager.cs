using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Inventory Counter")]
    public TMP_Text inventoryCounterText;

    [Header("UI Parent")]
    public Transform moduleListParent;

    [Header("Runtime Drag References")]
    public RectTransform dragCanvas;
    public ScrollRect parentScrollRect;
    public Camera uiCamera;

    private readonly Dictionary<ModuleData, int> ownedModules = new Dictionary<ModuleData, int>();

    private void Awake()
    {
        Instance = this;

        if (moduleListParent == null)
            moduleListParent = transform;

        RefreshInventoryUI();
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

        if (RewardPopupUI.Instance != null)
            RewardPopupUI.Instance.ShowReward(entry.displayName);

        return true;
    }
}