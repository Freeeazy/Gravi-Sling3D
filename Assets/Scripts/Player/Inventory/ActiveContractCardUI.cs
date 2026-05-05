using TMPro;
using UnityEngine;

public class ActiveContractCardUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text contractText;

    public void SetInfo(
        string deliveryItem,
        string destinationName,
        float distance,
        float integrity,
        string timeBonusText = "--:--")
    {
        if (!contractText)
            return;

        contractText.text =
            $"───────────────────────\n" +
            $"Deliver {deliveryItem} to {destinationName}\n" +
            $"───────────────────────\n" +
            $"Distance: {distance:0} Units\n" +
            $"Integrity: {integrity:0}%\n" +
            $"Time Bonus: {timeBonusText}";
    }

    public void Clear()
    {
        if (contractText)
            contractText.text = "";
    }
}