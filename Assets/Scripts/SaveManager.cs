using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class GraviSaveData
{
    public int version = 1;
    public int slot;

    public string saveName;
    public string playerName;

    public double playtimeSeconds;

    // Lifetime delivery payouts, separate from spendable credits.
    public double creditsEarned;
    public float walletCredits;

    public int reputationExp;
    public int rankIndex;
    public string rankName = "Rookie";

    public int deliveriesCompleted;
    public string lastPlayedUtc;

    public int seed;

    public bool tutorialsEnabled;
    public bool tutorialCompleted;
    public bool uncleDanteSeen;
}

[DefaultExecutionOrder(-1000)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public const int SlotCount = 3;

    private const string TutorialKey = "TutorialCompleted";
    private const string DanteKey = "MainGameTutorialSeen";

    [Header("Scenes")]
    [SerializeField] private string tutorialScene = "Tutorial";
    [SerializeField] private string mainScene = "MainGame";

    [Header("Saving")]
    [SerializeField, Min(5f)] private float autosaveSeconds = 30f;

    public GraviSaveData Current { get; private set; }
    public string LastError { get; private set; } = "";

    // Draft setting: changing this does not modify an existing save
    // or the global PlayerPrefs until a new game starts.
    public bool NewGameTutorialEnabled { get; private set; } = true;

    public string SaveFolder =>
        Path.Combine(Application.persistentDataPath, "Saves");

    private readonly System.Random seedRandom = new System.Random();

    private ModuleInventoryManager inventory;
    private FamilyReputationManager reputation;
    private float autosaveTimer;
    private int previousRolledSeed;

    private bool InGameplay
    {
        get
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene == tutorialScene || scene == mainScene;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void LateUpdate()
    {
        if (Current == null || !InGameplay)
            return;

        // Counts real seconds during focused, unpaused gameplay.
        if (Application.isFocused && Time.timeScale > 0f)
            Current.playtimeSeconds += Time.unscaledDeltaTime;

        // Keep an in-memory snapshot before scene objects disappear.
        CaptureRuntime();

        autosaveTimer += Time.unscaledDeltaTime;

        if (autosaveTimer >= Mathf.Max(5f, autosaveSeconds))
        {
            autosaveTimer = 0f;
            SaveCurrent();
        }
    }

    public void ToggleTutorial()
    {
        NewGameTutorialEnabled = !NewGameTutorialEnabled;
    }

    // Can also receive a Unity Toggle's dynamic bool event.
    public void SetTutorialEnabled(bool enabled)
    {
        NewGameTutorialEnabled = enabled;
    }

    public int RerollSeed()
    {
        int next;

        do
        {
            next = seedRandom.Next(100000000, 1000000000);
        }
        while (next == previousRolledSeed);

        previousRolledSeed = next;
        return next;
    }

    private string SlotPath(int slot)
    {
        return Path.Combine(SaveFolder, $"slot_{slot}.json");
    }

    // true + null means the slot is empty.
    // false means the slot could not be read safely.
    public bool TryReadSlot(
        int slot,
        out GraviSaveData data,
        out string error)
    {
        data = null;
        error = "";

        if (slot < 1 || slot > SlotCount)
        {
            error = "Save slot must be 1, 2, or 3.";
            return false;
        }

        string path = SlotPath(slot);

        if (!File.Exists(path))
            return true;

        try
        {
            data = JsonUtility.FromJson<GraviSaveData>(
                File.ReadAllText(path));

            if (data == null ||
                data.version != 1 ||
                data.slot != slot ||
                string.IsNullOrWhiteSpace(data.saveName) ||
                string.IsNullOrWhiteSpace(data.playerName) ||
                data.playtimeSeconds < 0 ||
                double.IsNaN(data.playtimeSeconds) ||
                double.IsInfinity(data.playtimeSeconds) ||
                data.creditsEarned < 0 ||
                double.IsNaN(data.creditsEarned) ||
                double.IsInfinity(data.creditsEarned) ||
                data.walletCredits < 0 ||
                float.IsNaN(data.walletCredits) ||
                float.IsInfinity(data.walletCredits) ||
                data.reputationExp < 0 ||
                data.rankIndex < 0 ||
                data.deliveriesCompleted < 0)
            {
                throw new InvalidDataException(
                    "Invalid or unsupported save data.");
            }

            return true;
        }
        catch (Exception ex)
        {
            data = null;
            error = $"Couldn't read slot {slot}: {ex.Message}";
            return false;
        }
    }

    // Occupied slot: loads its existing data.
    // Empty slot: creates a new save using the supplied fields.
    // Unreadable slots are never treated as empty or overwritten.
    public bool StartSlot(
        int slot,
        string saveName,
        string playerName,
        string seedText)
    {
        LastError = "";

        if (!TryReadSlot(slot, out GraviSaveData data, out string error))
            return Fail(error);

        if (data == null)
        {
            if (string.IsNullOrWhiteSpace(saveName))
            {
                return Fail("Enter a save name.");
            } 
            else if(string.IsNullOrWhiteSpace(playerName))
            {
                return Fail("Enter a player name.");
            }

                int seed;

            if (string.IsNullOrWhiteSpace(seedText))
            {
                seed = RerollSeed();
            }
            else if (!int.TryParse(
                         seedText.Trim(),
                         NumberStyles.None,
                         CultureInfo.InvariantCulture,
                         out seed))
            {
                return Fail("Seed must be a number from 0 to 2147483647.");
            }

            data = new GraviSaveData
            {
                slot = slot,
                saveName = saveName.Trim(),
                playerName = playerName.Trim(),
                seed = seed,

                tutorialsEnabled = NewGameTutorialEnabled,

                // Off means skip both introductions.
                tutorialCompleted = !NewGameTutorialEnabled,
                uncleDanteSeen = !NewGameTutorialEnabled
            };
        }

        string targetScene =
            data.tutorialCompleted ? mainScene : tutorialScene;

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
            return Fail($"Scene '{targetScene}' is missing from the build.");

        if (Current != null && !SaveCurrent())
            return false;

        // Make sure storage works before committing to the new session.
        if (!WriteSave(data))
            return false;

        Current = data;
        inventory = null;
        reputation = null;
        autosaveTimer = 0f;

        // These are the exact keys used by your existing scripts.
        PlayerPrefs.SetInt(
            TutorialKey, Current.tutorialCompleted ? 1 : 0);

        PlayerPrefs.SetInt(
            DanteKey, Current.uncleDanteSeen ? 1 : 0);

        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(targetScene);
        return true;
    }

    public void ResetSlot(int slot)
    {
        if (slot < 1 || slot > SlotCount)
        {
            Fail("Save slot must be 1, 2, or 3.");
            return;
        }

        // Prevent autosaving from recreating a deleted active slot.
        if (Current != null && Current.slot == slot)
        {
            Fail("Return to the title screen before resetting this slot.");
            return;
        }

        try
        {
            string path = SlotPath(slot);

            // Remove any temporary file left by an interrupted save.
            File.Delete(path + ".tmp");

            // File.Delete also succeeds if the file doesn't exist.
            File.Delete(path);

            LastError = "";
            Debug.Log($"[SaveManager] Slot {slot} reset.");
        }
        catch (Exception ex)
        {
            Fail($"Couldn't reset slot {slot}: {ex.Message}");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // This implementation expects your existing single-scene flow.
        if (mode == LoadSceneMode.Additive || Current == null)
            return;

        // Capture completion flags changed by the outgoing tutorial.
        CaptureTutorialFlags();

        inventory = null;
        reputation = null;

        if (scene.name != tutorialScene && scene.name != mainScene)
        {
            // End the session when returning to the title/menu.
            // Keep Current available if the disk write fails.
            if (WriteSave(Current))
                Current = null;

            return;
        }

        inventory = ModuleInventoryManager.Instance;
        reputation = FamilyReputationManager.Instance;

        if (inventory != null)
            inventory.RestoreSavedCredits(Current.walletCredits);

        if (reputation != null)
        {
            reputation.RestoreSavedReputation(
                Current.rankIndex,
                Current.reputationExp);
        }

        SaveCurrent();
    }

    private void CaptureTutorialFlags()
    {
        if (Current == null)
            return;

        Current.tutorialCompleted =
            PlayerPrefs.GetInt(TutorialKey, 0) == 1;

        Current.uncleDanteSeen =
            PlayerPrefs.GetInt(DanteKey, 0) == 1;
    }

    private void CaptureRuntime()
    {
        if (Current == null)
            return;

        CaptureTutorialFlags();

        if (inventory != null)
            Current.walletCredits = inventory.credits;

        if (reputation != null)
        {
            reputation.GetSaveReputation(
                out int rank,
                out int xp);

            Current.rankIndex = rank;
            Current.reputationExp = xp;

            string[] names = reputation.rankNames;

            Current.rankName =
                names != null && rank >= 0 && rank < names.Length
                    ? names[rank]
                    : "Unknown";
        }
    }

    public void RecordDelivery(float creditsPaid)
    {
        if (Current == null)
            return;

        Current.deliveriesCompleted++;
        Current.creditsEarned += Math.Max(0d, creditsPaid);
    }

    public bool SaveCurrent()
    {
        if (Current == null)
            return true;

        CaptureRuntime();
        return WriteSave(Current);
    }

    // Unity Button events need a void method.
    public void SaveNow()
    {
        SaveCurrent();
    }

    private bool WriteSave(GraviSaveData data)
    {
        string path = SlotPath(data.slot);
        string temporaryPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(SaveFolder);

            data.lastPlayedUtc = DateTime.UtcNow.ToString(
                "O", CultureInfo.InvariantCulture);

            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(data, true));

            // Finish writing before replacing the existing slot.
            // No permanent backup file: three final JSON files maximum.
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);

            LastError = "";
            return true;
        }
        catch (Exception ex)
        {
            return Fail($"Couldn't save slot {data.slot}: {ex.Message}");
        }
    }

    private bool Fail(string message)
    {
        LastError = message;
        Debug.LogError($"[SaveManager] {message}");
        return false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveCurrent();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            SaveCurrent();
    }

    private void OnApplicationQuit()
    {
        SaveCurrent();
    }
}