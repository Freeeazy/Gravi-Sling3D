using System.Collections;
using TMPro;
using UnityEngine;

public class StationDeliveryReactionEmitter : MonoBehaviour
{
    [Header("Refs")]
    public GameObject popupPrefab;
    public Transform popupParent;

    [Header("Popup Settings")]
    public float typeSpeed = 0.025f;
    public float visibleTime = 4f;
    public float driftSpeed = 0.25f;
    public Vector3 localDriftDirection = Vector3.up;

    [Header("Reaction Lines")]
    [TextArea] public string[] failedLines;
    [TextArea] public string[] barelyDeliveredLines;
    [TextArea] public string[] damagedLines;
    [TextArea] public string[] goodLines;
    [TextArea] public string[] perfectLines;

    public void ShowReaction(int qualityIndex)
    {
        if (popupPrefab == null || popupParent == null)
        {
            Debug.LogWarning("[StationDeliveryReactionEmitter] Missing popup prefab or popup parent.");
            return;
        }

        string message = GetRandomLine(qualityIndex);

        if (string.IsNullOrEmpty(message))
            return;

        GameObject popupObject = Instantiate(popupPrefab, popupParent);
        popupObject.transform.localPosition = Vector3.zero;
        popupObject.transform.localRotation = Quaternion.identity;
        popupObject.transform.localScale = Vector3.one;

        TMP_Text text = popupObject.GetComponentInChildren<TMP_Text>();

        if (text == null)
        {
            Debug.LogWarning("[StationDeliveryReactionEmitter] Popup prefab has no TMP_Text child.");
            Destroy(popupObject);
            return;
        }

        StartCoroutine(PlayPopup(popupObject.transform, text, message));
    }

    private string GetRandomLine(int qualityIndex)
    {
        string[] selectedLines = qualityIndex switch
        {
            0 => failedLines,
            1 => barelyDeliveredLines,
            2 => damagedLines,
            3 => goodLines,
            4 => perfectLines,
            _ => null
        };

        if (selectedLines == null || selectedLines.Length == 0)
            return GetFallbackLine(qualityIndex);

        return selectedLines[Random.Range(0, selectedLines.Length)];
    }

    private string GetFallbackLine(int qualityIndex)
    {
        switch (qualityIndex)
        {
            case 0:
                return "THE PACKAGE IS GONE. HOW DID YOU DELIVER NOTHING?";
            case 1:
                return "It arrived. I wish it hadn't.";
            case 2:
                return "The box says fragile. You saw that part, right?";
            case 3:
                return "Acceptable. Alarmingly acceptable.";
            case 4:
                return "Package intact. Courier suspiciously competent.";
            default:
                return "";
        }
    }

    private IEnumerator PlayPopup(Transform popupTransform, TMP_Text text, string message)
    {
        text.text = "";

        for (int i = 0; i <= message.Length; i++)
        {
            text.text = message.Substring(0, i);

            if (popupTransform != null)
                popupTransform.localPosition += localDriftDirection.normalized * driftSpeed * Time.deltaTime;

            yield return new WaitForSeconds(typeSpeed);
        }

        float timer = 0f;

        while (timer < visibleTime)
        {
            timer += Time.deltaTime;

            if (popupTransform != null)
                popupTransform.localPosition += localDriftDirection.normalized * driftSpeed * Time.deltaTime;

            yield return null;
        }

        if (popupTransform != null)
            Destroy(popupTransform.gameObject);

        Debug.Log("Playing PopUp for delivery");
    }
}