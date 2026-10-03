using System.Collections.Generic;
using UnityEngine;

public class OpenQuestBoard : MonoBehaviour
{
    private static readonly HashSet<OpenQuestBoard> openBoards = new HashSet<OpenQuestBoard>();
    private static readonly HashSet<OpenQuestBoard> cursorOwners = new HashSet<OpenQuestBoard>();
    private static CursorLockMode previousCursorLock;
    private static bool previousCursorVisible;

    [Header("Refs (scene objects)")]
    public SimpleMove move;
    public Transform questBoardRoot;

    [Header("UI hooks")]
    public NPCDropdownMover dropdownMover;

    [Header("Neighbor Panels")]
    public List<OpenQuestBoard> siblingBoards = new List<OpenQuestBoard>();

    [Header("Disable While Open")]
    public List<GameObject> disableWhileOpen = new List<GameObject>();
    private readonly Dictionary<GameObject, bool> previousStates =
        new Dictionary<GameObject, bool>();

    [Header("Input")]
    [Tooltip("Set to None to open this panel only through buttons or other scripts.")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Rules")]
    public bool onlyAllowWhenOrbiting = true;

    [Tooltip("Unlock/show the mouse while open. Turn off if another script owns the cursor.")]
    public bool manageCursor = true;

    [Header("Rotation Settings")]
    public float closedXAngle = 90f;
    public float openXAngle = 0f;
    public float rotateSpeed = 6f;

    public bool IsOpen => isOpen;

    // Regular boards rotate. Derived menus can opt out.
    protected virtual bool UsesRotationAnimation => true;

    // Shared by keyboard input, button calls, and automatic closing.
    public bool CanUseBoard =>
        !onlyAllowWhenOrbiting ||
        (SlingshotPlanet3D.Active != null &&
         SlingshotPlanet3D.Active.IsOrbiting &&
         !SlingshotPlanet3D.Active.IsCharging);

    private bool isOpen = false;
    private float targetX;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedState()
    {
        openBoards.Clear();
        cursorOwners.Clear();
        UIBlock.IsUIOpen = false;
    }

    protected virtual void Awake()
    {
        targetX = closedXAngle;

        if (UsesRotationAnimation && questBoardRoot)
        {
            questBoardRoot.localRotation =
                Quaternion.Euler(closedXAngle, 0f, 0f);
        }
    }

    protected virtual void Update()
    {
        if (!questBoardRoot)
            return;

        if (!CanUseBoard && isOpen)
            ForceClose();

        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            ToggleBoard();

        // Radial menus still run the input/orbit logic above,
        // but never run the rotation logic below.
        if (!UsesRotationAnimation)
            return;

        Quaternion targetRot = Quaternion.Euler(targetX, 0f, 0f);

        questBoardRoot.localRotation = Quaternion.Slerp(
            questBoardRoot.localRotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }
    protected virtual void OnDisable()
    {
        ForceClose();

        if (UsesRotationAnimation && questBoardRoot)
        {
            questBoardRoot.localRotation =
                Quaternion.Euler(closedXAngle, 0f, 0f);
        }
    }

    // Available in a Button's OnClick list, or from another script.
    public void ToggleBoard()
    {
        if (isOpen)
            ForceClose();
        else
            OpenBoard();
    }

    // All opening paths go through the same validation and sibling handling.
    public virtual void OpenBoard()
    {
        if (isOpen || !isActiveAndEnabled || !questBoardRoot || !CanUseBoard)
            return;

        isOpen = true;
        openBoards.Add(this);
        AcquireCursor();

        // Register first so sibling closing cannot release the shared UI/cursor lock.
        // Restore the old panel's disabled objects before this panel captures them.
        CloseSiblingBoards();

        if (!isOpen || !isActiveAndEnabled)
            return;

        targetX = openXAngle;
        UIBlock.IsUIOpen = true;

        SetDisabledObjects(true);
    }

    public virtual void ForceClose()
    {
        if (!isOpen)
            return;

        isOpen = false;
        targetX = closedXAngle;
        openBoards.Remove(this);
        UIBlock.IsUIOpen = openBoards.Count > 0;
        ReleaseCursor();
        dropdownMover?.ResetDropdown();

        SetDisabledObjects(false);
    }

    private void AcquireCursor()
    {
        if (!manageCursor)
            return;

        if (cursorOwners.Count == 0)
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
        }

        cursorOwners.Add(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorOwners.Remove(this) || cursorOwners.Count > 0)
            return;

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void CloseSiblingBoards()
    {
        for (int i = 0; i < siblingBoards.Count; i++)
        {
            var sibling = siblingBoards[i];
            if (sibling != null && sibling != this && sibling.IsOpen)
                sibling.ForceClose();
        }
    }

    private void SetDisabledObjects(bool boardOpen)
    {
        if (boardOpen)
        {
            for (int i = 0; i < disableWhileOpen.Count; i++)
            {
                var obj = disableWhileOpen[i];
                if (obj == null)
                    continue;

                if (!previousStates.ContainsKey(obj))
                    previousStates[obj] = obj.activeSelf;

                obj.SetActive(false);
            }
        }
        else
        {
            // Restore everything captured, even if the inspector list changed.
            foreach (var entry in previousStates)
            {
                if (entry.Key != null)
                    entry.Key.SetActive(entry.Value);
            }

            previousStates.Clear();
        }
    }
}
