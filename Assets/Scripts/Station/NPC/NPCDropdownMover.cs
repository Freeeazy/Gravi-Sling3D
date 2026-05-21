using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NPCDropdownMover : MonoBehaviour
{
    public static NPCDropdownMover Instance { get; private set; }

    [Header("Dropdown UI")]
    public NPCQuestDropdownUI dropdownUI;

    [Header("Wiring")]
    [Tooltip("Assign the same 10 rows the NPCManager uses (or fewer if you want).")]
    public NPCUILink[] rows;

    [Tooltip("The dropdown panel GameObject that gets moved under the clicked row.")]
    public RectTransform dropdownPanel;

    [Tooltip("Where the dropdown should live when 'reset' (usually bottom of Content).")]
    public Transform parkedParent;

    [Tooltip("Optional: the layout root to rebuild (usually ScrollView Content).")]
    public RectTransform layoutRoot;

    [Header("Behavior")]
    public bool collapseOnSameRowClick = true;

    [Header("Auto Scroll")]
    public ScrollRect scrollRect;
    public RectTransform viewportRect;

    [Tooltip("The ScrollRect content object. In your hierarchy, this is usually Content.")]
    public RectTransform scrollContentRect;

    public float revealPadding = 20f;
    public bool autoRevealDropdown = true;

    private int _currentRowIndex = -1;
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private void Awake()
    {
        if (dropdownPanel)
        {
            _originalParent = dropdownPanel.parent;
            _originalSiblingIndex = dropdownPanel.GetSiblingIndex();
        }
    }

    private void OnEnable()
    {
        HookRowButtons();
        ResetDropdown(); // whenever board opens, start collapsed
    }

    private void OnDisable()
    {
        // QuestBoard closed -> reset
        ResetDropdown();
        UnhookRowButtons();
    }

    private void HookRowButtons()
    {
        if (rows == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (!row || !row.rowButton) continue;

            int captured = i;
            row.rowButton.onClick.AddListener(() => OnRowClicked(captured));
        }
    }

    public void UnhookRowButtons()
    {
        if (rows == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (!row || !row.rowButton) continue;

            // simplest + safest for prototype: clear and re-hook on enable
            row.rowButton.onClick.RemoveAllListeners();
        }
    }

    private void OnRowClicked(int rowIndex)
    {
        if (!dropdownPanel) return;

        if (rows == null || rowIndex < 0 || rowIndex >= rows.Length) return;
        if (!rows[rowIndex] || !rows[rowIndex].gameObject.activeInHierarchy) return;

        // Collapse if clicking same row
        if (collapseOnSameRowClick && _currentRowIndex == rowIndex && dropdownPanel.gameObject.activeSelf)
        {
            ResetDropdown();
            return;
        }

        _currentRowIndex = rowIndex;

        Transform parent = rows[rowIndex].transform.parent; // "List"

        // 1) Put dropdown at the end first (so indices are stable)
        dropdownPanel.SetParent(parent, false);
        dropdownPanel.SetAsLastSibling();

        // 2) Now insert it directly after the clicked row, using ARRAY index
        int desiredIndex = rows[rowIndex].transform.GetSiblingIndex() + 1;
        dropdownPanel.SetSiblingIndex(desiredIndex);

        dropdownPanel.gameObject.SetActive(true);

        // IMPORTANT: tell dropdown which NPC is selected
        int npcId = rows[rowIndex].BoundNpcId;   // requires BoundNpcId in NPCUILink
        dropdownUI?.ShowForNpc(npcId);

        //Debug.Log($"[NPC CLICK] rowIndex={rowIndex} | rowName={rows[rowIndex]?.name} | dropSibling={dropdownPanel.GetSiblingIndex()}");

        ForceLayoutRefresh();

        if (autoRevealDropdown)
            StartCoroutine(RevealDropdownNextFrame());
    }

    public void ResetDropdown()
    {
        _currentRowIndex = -1;

        if (!dropdownPanel) return;

        dropdownPanel.gameObject.SetActive(false);

        // park it somewhere sensible so it doesn't break ordering next open
        if (parkedParent)
        {
            dropdownPanel.SetParent(parkedParent, worldPositionStays: false);
            dropdownPanel.SetAsLastSibling();
        }
        else if (_originalParent)
        {
            dropdownPanel.SetParent(_originalParent, worldPositionStays: false);
            dropdownPanel.SetSiblingIndex(_originalSiblingIndex);
        }

        dropdownUI?.ResetUI();

        ForceLayoutRefresh();
    }

    private void ForceLayoutRefresh()
    {
        if (!layoutRoot) return;

        // Rebuild immediately so the scroll list doesn't “pop” a frame later.
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }
    private IEnumerator RevealDropdownNextFrame()
    {
        // Wait one frame so the layout group/content size fitter catches up.
        yield return null;

        ForceLayoutRefresh();
        Canvas.ForceUpdateCanvases();

        RevealDropdownInView();
    }
    private void RevealDropdownInView()
    {
        if (!scrollRect || !viewportRect || !scrollContentRect || !layoutRoot || !dropdownPanel)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

        Vector3[] dropdownWorldCorners = new Vector3[4];
        dropdownPanel.GetWorldCorners(dropdownWorldCorners);

        // Convert dropdown bottom/top into viewport-local space
        Vector2 dropdownBottomLocal = viewportRect.InverseTransformPoint(dropdownWorldCorners[0]);
        Vector2 dropdownTopLocal = viewportRect.InverseTransformPoint(dropdownWorldCorners[1]);

        Rect viewportRectLocal = viewportRect.rect;

        float viewportBottom = viewportRectLocal.yMin;
        float viewportTop = viewportRectLocal.yMax;

        Vector2 contentPos = scrollContentRect.anchoredPosition;

        // Dropdown bottom is below viewport bottom, so scroll content upward.
        if (dropdownBottomLocal.y < viewportBottom + revealPadding)
        {
            float difference = (viewportBottom + revealPadding) - dropdownBottomLocal.y;
            contentPos.y += difference;
        }
        // Dropdown top is above viewport top, so scroll content downward.
        else if (dropdownTopLocal.y > viewportTop - revealPadding)
        {
            float difference = dropdownTopLocal.y - (viewportTop - revealPadding);
            contentPos.y -= difference;
        }

        scrollContentRect.anchoredPosition = contentPos;
    }
}