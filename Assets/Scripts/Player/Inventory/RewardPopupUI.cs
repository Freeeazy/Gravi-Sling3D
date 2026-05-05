using System.Collections;
using TMPro;
using UnityEngine;

public class RewardPopupUI : MonoBehaviour
{
    public static RewardPopupUI Instance { get; private set; }

    [Header("Refs")]
    public TMP_Text rewardText;

    [Header("Settings")]
    public float typeSpeed = 0.035f;
    public float visibleTime = 3f;
    public float deleteSpeed = 0.02f;

    private Coroutine _popupRoutine;

    private void Awake()
    {
        Instance = this;

        if (rewardText)
            rewardText.text = "";
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowReward(string rewardName)
    {
        if (!rewardText)
            return;

        string message = $"{rewardName} Received";

        if (_popupRoutine != null)
            StopCoroutine(_popupRoutine);

        _popupRoutine = StartCoroutine(PlayPopup(message));
    }

    private IEnumerator PlayPopup(string message)
    {
        rewardText.text = "";

        // Type in
        for (int i = 0; i <= message.Length; i++)
        {
            rewardText.text = message.Substring(0, i);
            yield return new WaitForSeconds(typeSpeed);
        }

        // Sit visible
        yield return new WaitForSeconds(visibleTime);

        // Delete out
        for (int i = message.Length; i >= 0; i--)
        {
            rewardText.text = message.Substring(0, i);
            yield return new WaitForSeconds(deleteSpeed);
        }

        rewardText.text = "";
        _popupRoutine = null;
    }
}