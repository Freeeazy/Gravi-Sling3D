using System.Collections;
using TMPro;
using UnityEngine;

public class ActiveContractCardUI : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text contractText;

    [Header("Damage Flash")]
    [SerializeField] private float damageTextHoldTime = 1f;
    [SerializeField] private string damageColor = "#FF3D3D";

    private string currentDeliveryItem;
    private string currentDestinationName;
    private float currentDistance;
    private float currentIntegrity;
    private string currentTimeBonusText = "--:--";

    private string integrityExtraText = "";
    private Coroutine damageTextRoutine;

    public void SetInfo(
        string deliveryItem,
        string destinationName,
        float distance,
        float integrity,
        string timeBonusText = "--:--")
    {
        currentDeliveryItem = deliveryItem;
        currentDestinationName = destinationName;
        currentDistance = distance;
        currentIntegrity = integrity;
        currentTimeBonusText = timeBonusText;

        RefreshText();
    }

    public void ShowIntegrityDamage(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        if (damageTextRoutine != null)
            StopCoroutine(damageTextRoutine);

        integrityExtraText = $" <color={damageColor}>-{damageAmount:0}%</color>";
        RefreshText();

        damageTextRoutine = StartCoroutine(ClearDamageTextAfterDelay());
    }

    private IEnumerator ClearDamageTextAfterDelay()
    {
        yield return new WaitForSeconds(damageTextHoldTime);

        integrityExtraText = "";
        RefreshText();

        damageTextRoutine = null;
    }

    private void RefreshText()
    {
        if (!contractText)
            return;

        contractText.text =
            $"───────────────────────\n" +
            $"Deliver {currentDeliveryItem} to {currentDestinationName}\n" +
            $"───────────────────────\n" +
            $"Distance: {currentDistance:0} Units\n" +
            $"Integrity: {currentIntegrity:0}%{integrityExtraText}\n" +
            $"Time Bonus: {currentTimeBonusText}";
    }

    public void Clear()
    {
        if (damageTextRoutine != null)
        {
            StopCoroutine(damageTextRoutine);
            damageTextRoutine = null;
        }

        integrityExtraText = "";

        if (contractText)
            contractText.text = "";
    }
}