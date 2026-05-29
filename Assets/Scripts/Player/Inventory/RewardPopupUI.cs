using System.Collections;
using System.Collections.Generic;
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

    public void ShowModuleReward(ModuleData rewardModule)
    {
        if (!rewardText || rewardModule == null)
            return;

        string tierHex = GetTierHexColor(rewardModule.moduleTier);

        string message =
            $"<color=#{tierHex}>Tier {rewardModule.moduleTier} {rewardModule.moduleType}</color>" +
            $"\n<color=#{tierHex}>Module Received</color>";

        ShowMessage(message);
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

    public void ShowDeliveryReward(string deliveryQuality, float creditsReceived, ModuleData rewardModule)
    {
        if (!rewardText)
            return;

        string message = $"{deliveryQuality}\n+{creditsReceived:0} Credits";

        if (rewardModule != null)
        {
            string tierHex = GetTierHexColor(rewardModule.moduleTier);

            message +=
                $"\n<color=#{tierHex}>Tier {rewardModule.moduleTier} {rewardModule.moduleType} Module</color>";
        }

        ShowMessage(message);
    }

    private string GetTierHexColor(int tier)
    {
        Color tierColor = Color.white;

        if (ModuleInventoryManager.Instance != null)
            tierColor = ModuleInventoryManager.Instance.GetTierColor(tier);

        return ColorUtility.ToHtmlStringRGB(tierColor);
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

        for (int i = 0; i <= visibleCharacters; i++)
        {
            rewardText.text = GetRichTextSubstring(message, i);
            yield return new WaitForSeconds(typeSpeed);
        }

        yield return new WaitForSeconds(visibleTime);

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
        Stack<string> openTags = new Stack<string>();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int closingBracketIndex = text.IndexOf('>', i);

                if (closingBracketIndex == -1)
                    break;

                string tag = text.Substring(i, closingBracketIndex - i + 1);
                result.Append(tag);

                TrackRichTextTag(tag, openTags);

                i = closingBracketIndex;
                continue;
            }

            if (visibleCount >= visibleCharacterLimit)
                break;

            result.Append(text[i]);
            visibleCount++;
        }

        while (openTags.Count > 0)
        {
            result.Append(openTags.Pop());
        }

        return result.ToString();
    }

    private void TrackRichTextTag(string tag, Stack<string> openTags)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        if (tag.StartsWith("</"))
        {
            if (openTags.Count > 0)
                openTags.Pop();

            return;
        }

        if (tag.StartsWith("<color"))
        {
            openTags.Push("</color>");
        }
        else if (tag.StartsWith("<b>"))
        {
            openTags.Push("</b>");
        }
        else if (tag.StartsWith("<i>"))
        {
            openTags.Push("</i>");
        }
        else if (tag.StartsWith("<u>"))
        {
            openTags.Push("</u>");
        }
    }
}