using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Use this INSTEAD OF OpenQuestBoard on the radial menu's controller object.
// Inheritance lets this component fit in existing List<OpenQuestBoard> fields.
public class RadialMenuController : OpenQuestBoard
{
    public enum Direction { None, Up, Right, Down, Left }

    [Header("Radial Buttons")]
    public Button upButton;
    public Button rightButton;
    public Button downButton;
    public Button leftButton;

    [Header("Radial Selection")]
    [Tooltip("Circle radius as a fraction of screen HEIGHT. 0.14 is about 151 pixels at 1080p.")]
    [Range(0f, 0.5f)] public float deadZoneRadius = 0.14f;
    public bool clickCenterToClose = true;
    public bool closeAfterSelection = true;
    public KeyCode cancelKey = KeyCode.Escape;

    public Direction SelectedDirection { get; private set; }

    [Header("Radial Visibility")]
    [SerializeField] private CanvasGroup radialGroup;

    [Range(0f, 0.9f)]
    [SerializeField] private float openAlpha = 0.9f;

    // Completely bypass the parent script's rotation behavior.
    protected override bool UsesRotationAnimation => false;

    private PointerEventData pointer;
    private EventSystem pointerEventSystem;
    private Button highlightedButton;
    private Button pressedButton;
    private bool pointerHeld;
    private int openedFrame;

    protected override void Awake()
    {
        // Prefer the CanvasGroup on this same object.
        if (!radialGroup)
            radialGroup = GetComponent<CanvasGroup>();

        // Otherwise look on the assigned visual root.
        if (!radialGroup && questBoardRoot)
            radialGroup = questBoardRoot.GetComponent<CanvasGroup>();

        // Create one if neither location has one.
        if (!radialGroup)
        {
            GameObject target = questBoardRoot
                ? questBoardRoot.gameObject
                : gameObject;

            radialGroup = target.AddComponent<CanvasGroup>();
        }

        // The parent still needs a root reference for its opening checks.
        if (!questBoardRoot)
            questBoardRoot = radialGroup.transform;

        base.Awake();

        // Always start hidden.
        SetVisible(false);

        DisableNavigation(upButton);
        DisableNavigation(rightButton);
        DisableNavigation(downButton);
        DisableNavigation(leftButton);
    }
    private void SetVisible(bool visible)
    {
        if (!radialGroup)
            return;

        radialGroup.alpha = visible ? openAlpha : 0f;
        radialGroup.interactable = visible;
        radialGroup.blocksRaycasts = visible;
    }

    public override void OpenBoard()
    {
        bool wasOpen = IsOpen;
        base.OpenBoard();
        if (wasOpen || !IsOpen)
            return;

        openedFrame = Time.frameCount;
        SetVisible(true);

        // Keep selection controlled entirely by pointer enter/exit events.
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject &&
            questBoardRoot &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(questBoardRoot))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // Run after the EventSystem's Update, so opening a destination panel cannot
    // make that panel receive the same physical mouse event this frame.
    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        if (cancelKey != KeyCode.None && Input.GetKeyDown(cancelKey))
        {
            ForceClose();
            return;
        }

        if (!EventSystem.current)
            return;

        if (pointer == null || pointerEventSystem != EventSystem.current)
        {
            pointerEventSystem = EventSystem.current;
            pointer = new PointerEventData(EventSystem.current);
        }

        pointer.position = Input.mousePosition;
        pointer.button = PointerEventData.InputButton.Left;

        Vector2 offset = pointer.position - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        SelectedDirection = GetDirection(offset, deadZoneRadius * Screen.height);
        SetHighlight(GetButton(SelectedDirection));

        // A click that opened the menu cannot also activate a wedge.
        if (Time.frameCount == openedFrame)
            return;

        if (Input.GetMouseButtonDown(0))
            pointerHeld = true;

        // Dragging while held transfers the Pressed preview to the current zone.
        if (pointerHeld && pressedButton != highlightedButton)
        {
            if (pressedButton)
                pressedButton.OnPointerUp(pointer);
            pressedButton = highlightedButton;
            if (pressedButton)
                pressedButton.OnPointerDown(pointer);
        }

        if (Input.GetMouseButtonUp(0) && pointerHeld)
        {
            Button clicked = pressedButton;
            bool cancel = SelectedDirection == Direction.None;
            pointerHeld = false;
            pressedButton = null;

            if (clicked)
                clicked.OnPointerUp(pointer);

            if (cancel && clickCenterToClose)
            {
                ForceClose();
                return;
            }

            if (clicked && clicked == highlightedButton && IsUsable(clicked))
            {
                // This invokes the button's existing Inspector OnClick actions.
                clicked.onClick.Invoke();
                if (closeAfterSelection && IsOpen)
                    ForceClose();
            }
        }
    }

    // Equal 90-degree sectors. Diagonals split neighboring options; distance
    // beyond the center circle does not matter. Radius and offset use pixels.
    public static Direction GetDirection(Vector2 offset, float radius)
    {
        radius = Mathf.Max(0f, radius);
        if (offset.sqrMagnitude <= radius * radius)
            return Direction.None;

        if (Mathf.Abs(offset.y) >= Mathf.Abs(offset.x))
            return offset.y > 0f ? Direction.Up : Direction.Down;

        return offset.x > 0f ? Direction.Right : Direction.Left;
    }

    public override void ForceClose()
    {
        if (pressedButton && pointer != null)
            pressedButton.OnPointerUp(pointer);

        pressedButton = null;
        pointerHeld = false;
        SetHighlight(null);
        SelectedDirection = Direction.None;

        SetVisible(false);

        base.ForceClose();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ForceClose();
    }

    private Button GetButton(Direction direction)
    {
        switch (direction)
        {
            case Direction.Up: return upButton;
            case Direction.Right: return rightButton;
            case Direction.Down: return downButton;
            case Direction.Left: return leftButton;
            default: return null;
        }
    }

    private void SetHighlight(Button button)
    {
        if (!IsUsable(button))
            button = null;
        if (highlightedButton == button)
            return;

        if (highlightedButton && pointer != null)
            highlightedButton.OnPointerExit(pointer);

        highlightedButton = button;
        if (highlightedButton && pointer != null)
            highlightedButton.OnPointerEnter(pointer);
    }

    private static bool IsUsable(Button button)
    {
        return button && button.isActiveAndEnabled && button.IsInteractable();
    }

    private static void DisableNavigation(Button button)
    {
        if (!button)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        foreach (Graphic graphic in button.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }
}
