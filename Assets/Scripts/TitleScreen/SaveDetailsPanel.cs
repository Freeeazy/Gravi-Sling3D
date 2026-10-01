using System.Collections;
using UnityEngine;

public class SaveDetailsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform saveSlotContainer;
    [SerializeField] private RectTransform detailsPanel;
    [SerializeField] private CanvasGroup detailsCanvasGroup;
    [SerializeField] private CanvasGroup parentCanvasGroup;

    private Coroutine animationRoutine;
    private bool hasOpened = false;

    /// <summary>
    /// Call this from the button's OnClick().
    /// </summary>
    public void OpenDetails()
    {
        if (hasOpened)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        hasOpened = true;
        animationRoutine = StartCoroutine(OpenDetailsRoutine());
    }

    private IEnumerator OpenDetailsRoutine()
    {
        const float duration = 0.35f;

        const float saveStartWidth = 1450f;
        const float saveEndWidth = 750f;

        const float detailsStartWidth = 200f;
        const float detailsEndWidth = 690f;

        // Wait until the save container has shrunk by this amount.
        const float detailsOpenThreshold = detailsStartWidth;

        float totalSaveMovement = saveStartWidth - saveEndWidth;
        float elapsed = 0f;

        TurnOffCanvasGroup();

        SetWidth(saveSlotContainer, saveStartWidth);
        SetWidth(detailsPanel, detailsStartWidth);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float saveProgress = Mathf.SmoothStep(0f, 1f, t);

            float currentSaveWidth = Mathf.Lerp(
                saveStartWidth,
                saveEndWidth,
                saveProgress
            );

            SetWidth(saveSlotContainer, currentSaveWidth);

            float distanceMoved = saveStartWidth - currentSaveWidth;

            // Stays at 0 until 200 units have moved.
            // Reaches 1 when the save container finishes shrinking.
            float detailsProgress = Mathf.InverseLerp(
                detailsOpenThreshold,
                totalSaveMovement,
                distanceMoved
            );

            SetWidth(
                detailsPanel,
                Mathf.Lerp(
                    detailsStartWidth,
                    detailsEndWidth,
                    detailsProgress
                )
            );

            if (detailsCanvasGroup != null)
                detailsCanvasGroup.alpha = detailsProgress;

            yield return null;
        }

        SetWidth(saveSlotContainer, saveEndWidth);
        SetWidth(detailsPanel, detailsEndWidth);

        if (detailsCanvasGroup != null)
        {
            detailsCanvasGroup.alpha = 1f;
            detailsCanvasGroup.interactable = true;
            detailsCanvasGroup.blocksRaycasts = true;
        }

        animationRoutine = null;
    }

    private void SetWidth(RectTransform rect, float width)
    {
        if (rect == null)
            return;

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
    }
    public void ResetDetails()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        SetWidth(saveSlotContainer, 1450f);
        SetWidth(detailsPanel, 200f);

        hasOpened = false;
    }

    public void TurnOffCanvasGroup()
    {
        if (detailsCanvasGroup != null)
        {
            detailsCanvasGroup.alpha = 0f;
            detailsCanvasGroup.interactable = false;
            detailsCanvasGroup.blocksRaycasts = false;
        }
    }

    public void TurnOffParentCanvas()
    {
        if (parentCanvasGroup != null)
        {
            parentCanvasGroup.alpha = 0f;
            parentCanvasGroup.interactable = false;
            parentCanvasGroup.blocksRaycasts = false;
        }
    }
    
    public void TurnOnParentCanvas()
    {
        if (parentCanvasGroup != null)
        {
            parentCanvasGroup.alpha = 1f;
            parentCanvasGroup.interactable = true;
            parentCanvasGroup.blocksRaycasts = true;
        }
    }
}