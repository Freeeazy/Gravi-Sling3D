using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RadialMenuManager : MonoBehaviour
{
    public static RadialMenuManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private NPCQuestManager questManager;

    [Header("Radial Buttons")]
    [SerializeField] private Button safeButton;
    [SerializeField] private Button weirdButton;
    [SerializeField] private Button riskyButton;

    [Header("Quest Count Text")]
    [SerializeField] private TMP_Text safeCountText;
    [SerializeField] private TMP_Text weirdCountText;
    [SerializeField] private TMP_Text riskyCountText;

    [Header("Risky Heat Range")]
    [Tooltip("Lowest available heat. Place five star objects directly under this container.")]
    [SerializeField] private Transform leftHeatStarsRoot;

    [Tooltip("Highest available heat. Place five star objects directly under this container.")]
    [SerializeField] private Transform rightHeatStarsRoot;

    [Header("Debug")]
    [SerializeField] private bool logSelections = false;

    // -------------------------------------------------
    // QUEST LISTS
    // -------------------------------------------------

    private readonly List<NPCQuestManager.QuestOffer> safeQuests = new();
    private readonly List<NPCQuestManager.QuestOffer> weirdQuests = new();
    private readonly List<NPCQuestManager.QuestOffer> riskyQuests = new();

    // Temporary list populated by NPCQuestManager.
    private readonly List<NPCQuestManager.QuestOffer> availableOffers = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (safeButton != null)
            safeButton.onClick.AddListener(AcceptRandomSafeQuest);

        if (weirdButton != null)
            weirdButton.onClick.AddListener(AcceptRandomWeirdQuest);

        if (riskyButton != null)
            riskyButton.onClick.AddListener(AcceptRandomRiskyQuest);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (safeButton != null)
            safeButton.onClick.RemoveListener(AcceptRandomSafeQuest);

        if (weirdButton != null)
            weirdButton.onClick.RemoveListener(AcceptRandomWeirdQuest);

        if (riskyButton != null)
            riskyButton.onClick.RemoveListener(AcceptRandomRiskyQuest);
    }

    // -------------------------------------------------
    // REFRESH / SORT
    // -------------------------------------------------

    public void RefreshQuestLists()
    {
        safeQuests.Clear();
        weirdQuests.Clear();
        riskyQuests.Clear();
        availableOffers.Clear();

        if (questManager == null)
        {
            Debug.LogWarning(
                "[RadialMenuManager] NPCQuestManager reference is missing."
            );

            RefreshCountText();
            return;
        }

        questManager.GetCurrentAvailableOffers(availableOffers);

        for (int i = 0; i < availableOffers.Count; i++)
        {
            NPCQuestManager.QuestOffer offer = availableOffers[i];

            // Heat takes priority, including any future heat + cargo-effect quests.
            // Each offer belongs to exactly one category.
            if (offer.heatLevel > 0)
            {
                riskyQuests.Add(offer);
            }
            else if (offer.cargoEffectType != CargoEffectType.None)
            {
                weirdQuests.Add(offer);
            }
            else
            {
                safeQuests.Add(offer);
            }
        }

        RefreshCountText();
    }

    // -------------------------------------------------
    // BUTTON ACTIONS
    // -------------------------------------------------

    public void AcceptRandomSafeQuest()
    {
        AcceptRandomQuest(safeQuests, "Safe");
    }

    public void AcceptRandomWeirdQuest()
    {
        AcceptRandomQuest(weirdQuests, "Weird");
    }

    public void AcceptRandomRiskyQuest()
    {
        AcceptRandomQuest(riskyQuests, "Risky");
    }

    private void AcceptRandomQuest(
        List<NPCQuestManager.QuestOffer> questList,
        string categoryName)
    {
        if (questManager == null)
            return;

        if (questList == null || questList.Count == 0)
        {
            if (logSelections)
                Debug.Log(
                    $"[RadialMenuManager] No {categoryName} quests available."
                );

            RefreshQuestLists();
            return;
        }

        int randomIndex = Random.Range(0, questList.Count);

        NPCQuestManager.QuestOffer selectedQuest =
            questList[randomIndex];

        bool accepted =
            questManager.AcceptQuest(selectedQuest.npcId);

        if (accepted)
        {
            if (logSelections)
            {
                Debug.Log(
                    $"[RadialMenuManager] Accepted random {categoryName} quest: " +
                    $"{selectedQuest.questTitle} " +
                    $"from npcId={selectedQuest.npcId}"
                );
            }
        }
        else
        {
            Debug.LogWarning(
                $"[RadialMenuManager] Failed to accept {categoryName} quest " +
                $"from npcId={selectedQuest.npcId}."
            );
        }

        // Rebuild afterward so the accepted quest disappears
        // from the available pool and the UI updates.
        RefreshQuestLists();
    }

    // -------------------------------------------------
    // UI
    // -------------------------------------------------

    private void RefreshCountText()
    {
        if (safeCountText != null)
            safeCountText.text = $"[ {safeQuests.Count} ]";

        if (weirdCountText != null)
            weirdCountText.text = $"[ {weirdQuests.Count} ]";

        if (riskyCountText != null)
            riskyCountText.text = $"[ {riskyQuests.Count} ]";

        if (safeButton != null)
            safeButton.interactable = safeQuests.Count > 0;

        if (weirdButton != null)
            weirdButton.interactable = weirdQuests.Count > 0;

        if (riskyButton != null)
            riskyButton.interactable = riskyQuests.Count > 0;

        RefreshHeatRange();
    }

    private void RefreshHeatRange()
    {
        int lowestHeat = 0;
        int highestHeat = 0;

        if (riskyQuests.Count > 0)
        {
            lowestHeat = riskyQuests[0].heatLevel;
            highestHeat = lowestHeat;

            for (int i = 1; i < riskyQuests.Count; i++)
            {
                int heat = riskyQuests[i].heatLevel;

                lowestHeat = Mathf.Min(lowestHeat, heat);
                highestHeat = Mathf.Max(highestHeat, heat);
            }
        }

        SetActiveStars(leftHeatStarsRoot, lowestHeat);
        SetActiveStars(rightHeatStarsRoot, highestHeat);
    }

    private void SetActiveStars(Transform container, int heatLevel)
    {
        if (container == null)
            return;

        int activeStarCount = Mathf.Clamp(
            heatLevel,
            0,
            Mathf.Min(5, container.childCount)
        );

        for (int i = 0; i < container.childCount; i++)
        {
            container.GetChild(i).gameObject.SetActive(i < activeStarCount);
        }
    }
}