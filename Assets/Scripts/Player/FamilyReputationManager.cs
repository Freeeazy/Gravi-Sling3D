using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class FamilyReputationManager : MonoBehaviour
{
    public static FamilyReputationManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text repRankText;
    public TMP_Text repRankPercText;

    [Tooltip("XP amount text shown over the empty/background portion of the bar.")]
    public TMP_Text repXpAmountTextLight;

    [Tooltip("XP amount text revealed over the filled portion of the bar.")]
    public TMP_Text repXpAmountTextDark;

    [Tooltip("The RectTransform of the foreground fill image, not the background.")]
    public RectTransform repFillBar;

    [Tooltip("If 0, the script will grab the fill bar's starting width on Awake.")]
    public float maxFillWidth = 140f;

    [Header("Reputation")]
    [Tooltip("Current reputation XP inside the current rank.")]
    [Min(0)]
    public int reputationExp = 0;

    [Tooltip("Reputation XP required to move from each rank to the next rank. One entry per rank-up.")]
    public int[] reputationExpToNextRank =
    {
        1000,
        10000,
        50000,
        250000
    };

    [Tooltip("Current family rank index.")]
    public int rankIndex = 0;

    public string[] rankNames =
    {
        "Rookie",
        "Runner",
        "Trusted",
        "Made Courier",
        "Family Legend"
    };

    [Header("Animation")]
    public float tickDelay = 0.08f;
    public int reputationExpTickStep = 100;

    [Header("Debug")]
    public bool enableDebugRankKeys = true;
    public int debugReputationStep = 25;

    private int _pendingChange;
    private Coroutine _animateRoutine;

    private void Awake()
    {
        Instance = this;

        if (repFillBar != null && maxFillWidth <= 0f)
            maxFillWidth = repFillBar.sizeDelta.x;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
    private void Update()
    {
        if (!enableDebugRankKeys)
            return;

        if (Input.GetKeyDown(KeyCode.Minus))
        {
            AddReputation(-debugReputationStep);
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            AddReputation(debugReputationStep);
        }
    }
    public void AddReputation(int amount)
    {
        AddReputationExp(amount);
    }

    public void AddReputationExp(int amount)
    {
        if (amount == 0)
            return;

        _pendingChange += amount;

        if (_animateRoutine == null)
            _animateRoutine = StartCoroutine(AnimateReputationChange());
    }

    private IEnumerator AnimateReputationChange()
    {
        while (_pendingChange != 0)
        {
            int tickSize = Mathf.Max(1, reputationExpTickStep);
            int step = Mathf.Clamp(_pendingChange, -tickSize, tickSize);

            reputationExp += step;
            _pendingChange -= step;

            HandleRankBounds();
            RefreshUI(_pendingChange);

            yield return new WaitForSeconds(tickDelay);
        }

        RefreshUI();
        _animateRoutine = null;
    }

    private void HandleRankBounds()
    {
        while (rankIndex < GetMaxRankIndex() && reputationExp >= GetCurrentRankExpRequirement())
        {
            reputationExp -= GetCurrentRankExpRequirement();
            rankIndex++;
        }

        while (reputationExp < 0)
        {
            if (rankIndex > 0)
            {
                rankIndex--;
                reputationExp += GetCurrentRankExpRequirement();
            }
            else
            {
                reputationExp = 0;
                _pendingChange = 0;
                break;
            }
        }

        if (rankIndex >= GetMaxRankIndex())
        {
            reputationExp = Mathf.Max(0, reputationExp);
            _pendingChange = Mathf.Max(0, _pendingChange);
        }
    }

    private void RefreshUI(int pendingChange = 0)
    {
        string rankName = GetCurrentRankName();
        string expAmountText = BuildExpAmountText();

        if (repRankText != null)
            repRankText.text = $"[{rankName}]";

        if (repRankPercText != null)
            repRankPercText.text = BuildExpText(pendingChange);

        if (repXpAmountTextLight != null)
            repXpAmountTextLight.text = expAmountText;

        if (repXpAmountTextDark != null)
            repXpAmountTextDark.text = expAmountText;

        UpdateFillBar();
    }

    private string BuildExpText(int pendingChange)
    {
        string progressText = $"{GetCurrentRankProgressPercent():0}%";

        if (pendingChange > 0)
            return $"{progressText} <color=#4DFF88>+{GetPendingProgressPercent(pendingChange):0}%</color>";

        if (pendingChange < 0)
            return $"{progressText} <color=#FF3D3D>-{Mathf.Abs(GetPendingProgressPercent(pendingChange)):0}%</color>";

        return progressText;
    }
    private string BuildExpAmountText()
    {
        if (rankIndex >= GetMaxRankIndex())
            return "MAX";

        int requirement = GetCurrentRankExpRequirement();

        return $"{reputationExp:N0} / {requirement:N0}";
    }
    private float GetCurrentRankProgressPercent()
    {
        if (rankIndex >= GetMaxRankIndex())
            return 100f;

        return Mathf.Clamp01((float)reputationExp / GetCurrentRankExpRequirement()) * 100f;
    }
    private float GetPendingProgressPercent(int pendingChange)
    {
        if (rankIndex >= GetMaxRankIndex())
            return 0f;

        return ((float)pendingChange / GetCurrentRankExpRequirement()) * 100f;
    }
    private void UpdateFillBar()
    {
        if (repFillBar == null)
            return;

        float fillPercent = rankIndex >= GetMaxRankIndex()
            ? 1f
            : Mathf.Clamp01((float)reputationExp / GetCurrentRankExpRequirement());
        float targetWidth = maxFillWidth * fillPercent;

        Vector2 size = repFillBar.sizeDelta;
        size.x = targetWidth;
        repFillBar.sizeDelta = size;
    }

    private string GetCurrentRankName()
    {
        if (rankNames == null || rankNames.Length == 0)
            return "Unknown";

        rankIndex = Mathf.Clamp(rankIndex, 0, rankNames.Length - 1);
        return rankNames[rankIndex];
    }

    public int GetCurrentRankIndex()
    {
        if (rankNames == null || rankNames.Length == 0)
            return 0;

        rankIndex = Mathf.Clamp(rankIndex, 0, rankNames.Length - 1);
        return rankIndex;
    }

    public int GetCurrentRankExpRequirement()
    {
        int maxRankIndex = GetMaxRankIndex();

        if (rankIndex >= maxRankIndex)
            return 0;

        if (reputationExpToNextRank == null || reputationExpToNextRank.Length == 0)
            return 1000;

        int requirementIndex = Mathf.Clamp(rankIndex, 0, reputationExpToNextRank.Length - 1);
        return Mathf.Max(1, reputationExpToNextRank[requirementIndex]);
    }

    private int GetMaxRankIndex()
    {
        if (rankNames == null || rankNames.Length == 0)
            return 0;

        return rankNames.Length - 1;
    }
}