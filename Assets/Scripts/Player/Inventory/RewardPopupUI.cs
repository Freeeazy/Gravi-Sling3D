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

    public void ShowModuleReward(string rewardName)
    {
        if (!rewardText)
            return;

        string message = $"{rewardName} Received";
        ShowMessage(message);
    }

    public void ShowCreditReward(string deliveryQuality, float creditsReceived)
    {
        if (!rewardText)
            return;

        string message = $"{deliveryQuality} Delivery\n+{creditsReceived:0} Credits";
        ShowMessage(message);
    }

    public void ShowDeliveryReward(string deliveryQuality, float creditsReceived, bool gotModule, string moduleName = "")
    {
        if (!rewardText)
            return;

        string message = $"{deliveryQuality}\n+{creditsReceived:0} Credits";

        if (gotModule && !string.IsNullOrEmpty(moduleName))
            message += $"\n{moduleName} Received";

        ShowMessage(message);
    }

    private void ShowMessage(string message)
    {
        if (_popupRoutine != null)
            StopCoroutine(_popupRoutine);

        _popupRoutine = StartCoroutine(PlayPopup(message));
    }
    private IEnumerator PlayPopup(string message)
    {
        int visibleCharacters = CountVisibleCharacters(message);

        rewardText.text = "";

        // Type in
        for (int i = 0; i <= visibleCharacters; i++)
        {
            rewardText.text = GetRichTextSubstring(message, i);
            yield return new WaitForSeconds(typeSpeed);
        }

        yield return new WaitForSeconds(visibleTime);

        // Delete out
        for (int i = visibleCharacters; i >= 0; i--)
        {
            rewardText.text = GetRichTextSubstring(message, i);
            yield return new WaitForSeconds(deleteSpeed);
        }

        rewardText.text = "";
        _popupRoutine = null;
    }
    private int CountVisibleCharacters(string text)
    {
        int count = 0;
        bool insideTag = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                insideTag = true;
                continue;
            }

            if (text[i] == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                count++;
        }

        return count;
    }

    private string GetRichTextSubstring(string text, int visibleCharacterLimit)
    {
        int visibleCount = 0;
        System.Text.StringBuilder result = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int closingBracketIndex = text.IndexOf('>', i);

                if (closingBracketIndex == -1)
                    break;

                // Copy the whole TMP tag instantly.
                result.Append(text.Substring(i, closingBracketIndex - i + 1));
                i = closingBracketIndex;
                continue;
            }

            if (visibleCount >= visibleCharacterLimit)
                break;

            result.Append(text[i]);
            visibleCount++;
        }

        return result.ToString();
    }
}