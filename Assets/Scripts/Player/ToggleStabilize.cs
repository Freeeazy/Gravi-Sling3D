using TMPro;
using UnityEngine;

public class ToggleStabilize : MonoBehaviour
{
    [Header("References")]
    public SimpleMove simpleMove;      // Manually drag your SimpleMove object here
    public TMP_Text stateText;         // Drag your TMP text here

    [Header("Text")]
    public string onText = "Auto-Stabilize - On";
    public string offText = "Auto-Stabilize - Off";

    private void Start()
    {
        RefreshText();
    }

    // Wire this to your UI Button OnClick()
    public void ToggleAutoStabilize()
    {
        if (simpleMove == null)
        {
            Debug.LogWarning("ToggleStabilize: SimpleMove reference is missing.");
            return;
        }

        simpleMove.autoStabilizeRoll = !simpleMove.autoStabilizeRoll;

        // Optional: if turning it off, also stop any active stabilization state
        if (!simpleMove.autoStabilizeRoll)
            simpleMove.isAutoStabilizingRoll = false;

        RefreshText();
    }

    public void RefreshText()
    {
        if (stateText == null || simpleMove == null)
            return;

        stateText.text = simpleMove.autoStabilizeRoll ? onText : offText;
    }
}