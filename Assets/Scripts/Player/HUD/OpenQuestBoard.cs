using UnityEngine;

public class OpenQuestBoard : MonoBehaviour
{
    [Header("Refs (scene objects)")]
    public SimpleMove move;                 // assign in inspector
    public Transform questBoardRoot;       // assign in inspector (the thing you want to toggle)

    [Header("UI hooks")]
    public NPCDropdownMover dropdownMover;

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Rules")]
    public bool onlyAllowWhenOrbiting = true;

    [Header("Rotation Settings")]
    public float closedXAngle = 90f;   // folded up
    public float openXAngle = 0f;      // flat / readable
    public float rotateSpeed = 6f;

    private bool isOpen = false;
    private float targetX;

    private void Awake()
    {
        if (questBoardRoot)
        {
            targetX = closedXAngle;
            questBoardRoot.localRotation = Quaternion.Euler(closedXAngle, 0f, 0f);
        }

        UIBlock.IsUIOpen = false;
    }

    private void Update()
    {
        if (!questBoardRoot) return;

        // If you only want this at stations/orbit:
        if (onlyAllowWhenOrbiting)
        {
            // SlingshotPlanet3D.Active is set when orbit starts, cleared on exit
            if (SlingshotPlanet3D.Active == null || !SlingshotPlanet3D.Active.IsOrbiting || SlingshotPlanet3D.Active.IsCharging)
                return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            bool wasOpen = isOpen;

            isOpen = !isOpen;
            targetX = isOpen ? openXAngle : closedXAngle;
            UIBlock.IsUIOpen = isOpen;

            // If we just CLOSED, reset the dropdown panel
            if (wasOpen && !isOpen)
            {
                dropdownMover?.ResetDropdown();
                // or if you later add Instance:
                // NPCDropdownMover.Instance?.ResetDropdown();
            }
        }

        // Smooth rotate toward target
        Quaternion targetRot = Quaternion.Euler(targetX, 0f, 0f);
        questBoardRoot.localRotation = Quaternion.Slerp(
            questBoardRoot.localRotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }
}
