using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveMenuUI : MonoBehaviour
{
    [Header("Slot instances, ordered: 1, 2, 3")]
    [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[3];

    [Header("New Save Fields")]
    [SerializeField] private TMP_InputField saveNameInput;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField seedInput;

    [Header("Buttons")]
    [SerializeField] private Button tutorialToggleButton;
    [SerializeField] private Button seedRerollButton;
    [SerializeField] private Button startButton;

    [Header("Visuals")]
    [SerializeField] private GameObject tutorialCheckmark;
    [SerializeField] private TMP_Text errorText;

    private int selectedSlot = 1;
    private bool editingNewSave;

    private void OnEnable()
    {
        if (SaveManager.Instance == null)
            return;

        RefreshSlots();
        SelectSlot(selectedSlot);
    }

    public void RefreshSlots()
    {
        var manager = SaveManager.Instance;
        if (manager == null || slots == null)
            return;

        for (int i = 0; i < Mathf.Min(slots.Length, SaveManager.SlotCount); i++)
        {
            SaveSlotUI labels = slots[i];
            if (labels == null)
                continue;

            bool readable = manager.TryReadSlot(
                i + 1, out GraviSaveData data, out _);

            bool hasSave = readable && data != null;

            if (labels.deleteSlotButton != null)
                labels.deleteSlotButton.SetActive(hasSave || !readable);

            SetText(labels.saveName,
                !readable ? "[ Unreadable Save ]" :
                hasSave ? data.saveName : "[ Empty ]");

            SetText(labels.playerName,
                hasSave ? data.playerName : "");

            SetText(labels.playtime,
                hasSave ? FormatPlaytime(data.playtimeSeconds) : "");

            SetText(labels.creditsEarned,
                hasSave ? $"{data.creditsEarned:N0} credits earned" : "");

            SetText(labels.reputation,
                hasSave ? $"{data.rankName} • {data.reputationExp:N0} XP" : "");

            SetText(labels.deliveriesCompleted,
                hasSave ? $"{data.deliveriesCompleted:N0} deliveries" : "");

            SetText(labels.lastPlayed,
                hasSave ? FormatLastPlayed(data.lastPlayedUtc) : "");
        }
    }

    public void SelectSlot(int slot)
    {
        var manager = SaveManager.Instance;
        if (manager == null)
            return;

        if (slot < 1 || slot > SaveManager.SlotCount)
            return;

        selectedSlot = slot;

        bool readable = manager.TryReadSlot(
            slot, out GraviSaveData data, out string error);

        editingNewSave = readable && data == null;

        SetError(readable ? "" : error);

        saveNameInput.SetTextWithoutNotify(data?.saveName ?? "");
        playerNameInput.SetTextWithoutNotify(data?.playerName ?? "");

        seedInput.SetTextWithoutNotify(
            data != null
                ? data.seed.ToString(CultureInfo.InvariantCulture)
                : readable
                    ? manager.RerollSeed().ToString(
                        CultureInfo.InvariantCulture)
                    : "");

        saveNameInput.interactable = editingNewSave;
        playerNameInput.interactable = editingNewSave;
        seedInput.interactable = editingNewSave;

        if (tutorialToggleButton != null)
            tutorialToggleButton.interactable = editingNewSave;

        if (seedRerollButton != null)
            seedRerollButton.interactable = editingNewSave;

        if (startButton != null)
            startButton.interactable = readable;

        ShowTutorialCheck(
            data != null
                ? data.tutorialsEnabled
                : readable && manager.NewGameTutorialEnabled);
    }
    public void ResetSlot(int slot)
    {
        var manager = SaveManager.Instance;
        if (manager == null)
            return;

        manager.ResetSlot(slot);
        string error = manager.LastError;

        // Update labels and delete-button visibility.
        RefreshSlots();

        // Refresh the currently selected slot's input fields.
        SelectSlot(selectedSlot);

        // Preserve any deletion error after refreshing the selection.
        if (!string.IsNullOrEmpty(error))
            SetError(error);
    }
    public void ToggleTutorial()
    {
        if (!editingNewSave || SaveManager.Instance == null)
            return;

        SaveManager.Instance.ToggleTutorial();

        ShowTutorialCheck(
            SaveManager.Instance.NewGameTutorialEnabled);
    }

    public void RerollSeed()
    {
        if (!editingNewSave || SaveManager.Instance == null)
            return;

        seedInput.SetTextWithoutNotify(
            SaveManager.Instance.RerollSeed().ToString(
                CultureInfo.InvariantCulture));
    }

    public void StartSelectedSlot()
    {
        var manager = SaveManager.Instance;
        if (manager == null)
            return;

        if (!manager.StartSlot(
                selectedSlot,
                saveNameInput.text,
                playerNameInput.text,
                seedInput.text))
        {
            SetError(manager.LastError);
        }
    }

    private void ShowTutorialCheck(bool enabled)
    {
        if (tutorialCheckmark != null)
            tutorialCheckmark.SetActive(enabled);
    }

    private void SetError(string message)
    {
        SetText(errorText, message);

        if (errorText != null)
            errorText.gameObject.SetActive(
                !string.IsNullOrEmpty(message));
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
            label.text = value;
    }

    private static string FormatPlaytime(double seconds)
    {
        long totalMinutes = (long)(Math.Max(0d, seconds) / 60d);
        return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
    }

    private static string FormatLastPlayed(string utc)
    {
        if (DateTime.TryParse(
                utc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime time))
        {
            return time.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        }

        return "—";
    }
}