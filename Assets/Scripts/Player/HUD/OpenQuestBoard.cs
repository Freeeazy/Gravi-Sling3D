using System.Collections.Generic;
using UnityEngine;

public class OpenQuestBoard : MonoBehaviour
{
    [Header("Refs (scene objects)")]
    public SimpleMove move;                 // assign in inspector
    public Transform questBoardRoot;       // assign in inspector (the thing you want to toggle)

    [Header("UI hooks")]
    public NPCDropdownMover dropdownMover;

    [Header("Neighbor Panels")]
    public List<OpenQuestBoard> siblingBoards = new List<OpenQuestBoard>();

    [Header("Disable While Open")]
    public List<GameObject> disableWhileOpen = new List<GameObject>();

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Rules")]
    public bool onlyAllowWhenOrbiting = true;

    [Header("Rotation Settings")]
    public float closedXAngle = 90f;   // folded up
    public float openXAngle = 0f;      // flat / readable
    public float rotateSpeed = 6f;
    public bool IsOpen => isOpen;

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

        if (onlyAllowWhenOrbiting)
        {
            if (SlingshotPlanet3D.Active == null || !SlingshotPlanet3D.Active.IsOrbiting || SlingshotPlanet3D.Active.IsCharging)
                return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (!isOpen)
            {
                CloseSiblingBoards();
                OpenBoard();
            }
            else
            {
                ForceClose();
            }
        }

        Quaternion targetRot = Quaternion.Euler(targetX, 0f, 0f);
        questBoardRoot.localRotation = Quaternion.Slerp(
            questBoardRoot.localRotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }

    private void OpenBoard()
    {
        isOpen = true;
        targetX = openXAngle;
        UIBlock.IsUIOpen = true;

        SetDisabledObjects(true);
    }

    public void ForceClose()
    {
        if (!isOpen) return;

        isOpen = false;
        targetX = closedXAngle;
        UIBlock.IsUIOpen = false;
        dropdownMover?.ResetDropdown();

        SetDisabledObjects(false);
    }

    private void CloseSiblingBoards()
    {
        for (int i = 0; i < siblingBoards.Count; i++)
        {
            if (siblingBoards[i] != null && siblingBoards[i] != this && siblingBoards[i].IsOpen)
                siblingBoards[i].ForceClose();
        }
    }

    private void SetDisabledObjects(bool boardOpen)
    {
        for (int i = 0; i < disableWhileOpen.Count; i++)
        {
            if (disableWhileOpen[i] != null)
                disableWhileOpen[i].SetActive(!boardOpen);
        }
    }
}
