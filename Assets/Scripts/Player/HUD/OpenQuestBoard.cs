using UnityEngine;

public class OpenQuestBoard : MonoBehaviour
{
    [Header("Refs (scene objects)")]
    public SimpleMove move;                 // assign in inspector
    public Transform questBoardRoot;       // assign in inspector (the thing you want to toggle)

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
            if (SlingshotPlanet3D.Active == null || !SlingshotPlanet3D.Active.IsOrbiting)
                return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            isOpen = !isOpen;
            targetX = isOpen ? openXAngle : closedXAngle;
            UIBlock.IsUIOpen = isOpen;
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
