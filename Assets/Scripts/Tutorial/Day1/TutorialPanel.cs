using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPanel : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private List<RectTransform> pages = new List<RectTransform>();

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Page Counter")]
    [SerializeField] private TMP_Text pageCounterText;

    [Header("Tutorial Control")]
    [Tooltip("Script that should be disabled while the tutorial is open.")]
    [SerializeField] private MonoBehaviour scriptToDisable;
    [SerializeField] private MonoBehaviour scriptToDisable2;
    [SerializeField] private MonoBehaviour scriptToDisable3;

    [Header("Slide")]
    [SerializeField] private float slideDistance = 1200f;
    [SerializeField] private float slideDuration = 0.3f;

    private int currentPageIndex = 0;
    private bool isTransitioning = false;

    private void Awake()
    {
        SetupPages();

        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseTutorial);
    }

    private void OnEnable()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (scriptToDisable != null)
            scriptToDisable.enabled = false;

        if (scriptToDisable2 != null)
            scriptToDisable2.enabled = false;

        if (scriptToDisable3 != null)
            scriptToDisable3.enabled = false;
    }

    private void OnDisable()
    {
        if (scriptToDisable != null)
            scriptToDisable.enabled = true;

        if (scriptToDisable2 != null)
            scriptToDisable2.enabled = true;

        if (scriptToDisable3 != null)
            scriptToDisable3.enabled = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SetupPages()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null)
                continue;

            pages[i].anchoredPosition = Vector2.zero;
            pages[i].gameObject.SetActive(i == currentPageIndex);
        }

        UpdateButtons();
    }

    public void NextPage()
    {
        if (isTransitioning)
            return;

        if (currentPageIndex >= pages.Count - 1)
            return;

        StartCoroutine(SlideToPage(currentPageIndex + 1, true));
    }

    public void PreviousPage()
    {
        if (isTransitioning)
            return;

        if (currentPageIndex <= 0)
            return;

        StartCoroutine(SlideToPage(currentPageIndex - 1, false));
    }

    public void CloseTutorial()
    {
        if (isTransitioning)
            return;

        gameObject.SetActive(false);
    }

    private IEnumerator SlideToPage(int newPageIndex, bool movingForward)
    {
        if (newPageIndex < 0 || newPageIndex >= pages.Count)
            yield break;

        isTransitioning = true;

        RectTransform currentPage = pages[currentPageIndex];
        RectTransform newPage = pages[newPageIndex];

        float direction = movingForward ? 1f : -1f;

        Vector2 currentStart = Vector2.zero;
        Vector2 currentEnd = new Vector2(-slideDistance * direction, 0f);

        Vector2 newStart = new Vector2(slideDistance * direction, 0f);
        Vector2 newEnd = Vector2.zero;

        currentPage.anchoredPosition = currentStart;

        newPage.anchoredPosition = newStart;
        newPage.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / slideDuration);

            t = t * t * (3f - 2f * t);

            currentPage.anchoredPosition =
                Vector2.Lerp(currentStart, currentEnd, t);

            newPage.anchoredPosition =
                Vector2.Lerp(newStart, newEnd, t);

            yield return null;
        }

        currentPage.anchoredPosition = Vector2.zero;
        currentPage.gameObject.SetActive(false);

        newPage.anchoredPosition = Vector2.zero;

        currentPageIndex = newPageIndex;

        UpdateButtons();

        isTransitioning = false;
    }

    private void UpdateButtons()
    {
        bool isFirstPage = currentPageIndex == 0;
        bool isLastPage = currentPageIndex == pages.Count - 1;

        if (previousButton != null)
            previousButton.gameObject.SetActive(!isFirstPage);

        if (nextButton != null)
            nextButton.gameObject.SetActive(!isLastPage);

        if (closeButton != null)
            closeButton.gameObject.SetActive(isLastPage);

        if (pageCounterText != null)
            pageCounterText.text = $"({currentPageIndex + 1}/{pages.Count})";
    }
}