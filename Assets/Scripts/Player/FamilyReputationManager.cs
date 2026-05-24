using System.Collections;
using TMPro;
using UnityEngine;

public class FamilyReputationManager : MonoBehaviour
{
    public static FamilyReputationManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text reputationText;

    [Header("Reputation")]
    [Tooltip("Current reputation inside the current rank, 0-100.")]
    [Range(0, 100)]
    public int reputationPercent = 0;

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

    [Header("Visual Bar")]
    public int barSegments = 10;
    public string filledChar = "█";
    public string emptyChar = "░";

    [Header("Animation")]
    public float tickDelay = 0.08f;

    [Header("Debug")]
    public bool enableDebugRankKeys = true;
    public int debugReputationStep = 25;

    private int _pendingChange;
    private Coroutine _animateRoutine;

    private void Awake()
    {
        Instance = this;
        RefreshText();
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
            int step = _pendingChange > 0 ? 1 : -1;

            reputationPercent += step;
            _pendingChange -= step;

            HandleRankBounds();

            RefreshText(_pendingChange);

            yield return new WaitForSeconds(tickDelay);
        }

        RefreshText();
        _animateRoutine = null;
    }

    private void HandleRankBounds()
    {
        while (reputationPercent >= 100)
        {
            if (rankIndex < rankNames.Length - 1)
            {
                rankIndex++;
                reputationPercent -= 100;
            }
            else
            {
                reputationPercent = 100;
                _pendingChange = 0;
                break;
            }
        }

        while (reputationPercent < 0)
        {
            if (rankIndex > 0)
            {
                rankIndex--;
                reputationPercent += 100;
            }
            else
            {
                reputationPercent = 0;
                _pendingChange = 0;
                break;
            }
        }
    }

    private void RefreshText(int pendingChange = 0)
    {
        if (reputationText == null)
            return;

        string rankName = GetCurrentRankName();
        string bar = BuildProgressBar();

        string pendingText = "";

        if (pendingChange > 0)
            pendingText = $" <color=#4DFF88>+ {pendingChange}%</color>";
        else if (pendingChange < 0)
            pendingText = $" <color=#FF3D3D>- {Mathf.Abs(pendingChange)}%</color>";

        reputationText.text =
            $"FAMILY REPUTATION\n" +
            $"[{rankName}] {bar} {reputationPercent}%{pendingText}";
    }

    private string BuildProgressBar()
    {
        int filledSegments = Mathf.RoundToInt((reputationPercent / 100f) * barSegments);
        filledSegments = Mathf.Clamp(filledSegments, 0, barSegments);

        int emptySegments = barSegments - filledSegments;

        return new string(filledChar[0], filledSegments) +
               new string(emptyChar[0], emptySegments);
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
}