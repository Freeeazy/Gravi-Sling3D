using UnityEngine;

public class OpenQuestBoard : MonoBehaviour
{
    [Header("Refs (scene objects)")]
    public SimpleMove move;                 // assign in inspector
    public GameObject questBoardRoot;       // assign in inspector (the thing you want to toggle)

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Rules")]
    public bool onlyAllowWhenOrbiting = true;

    private void Awake()
    {
        if (questBoardRoot) questBoardRoot.SetActive(false);
        UIBlock.IsUIOpen = false;
    }

    private void Update()
    {
        // If you only want this at stations/orbit:
        if (onlyAllowWhenOrbiting)
        {
            // SlingshotPlanet3D.Active is set when orbit starts, cleared on exit
            if (SlingshotPlanet3D.Active == null || !SlingshotPlanet3D.Active.IsOrbiting)
                return;
        }

        if (!questBoardRoot) return;

        if (Input.GetKeyDown(toggleKey))
        {
            bool newState = !questBoardRoot.activeSelf;
            questBoardRoot.SetActive(newState);
            UIBlock.IsUIOpen = newState;
        }

        // Safety: if board was closed externally, keep flag in sync
        if (UIBlock.IsUIOpen != questBoardRoot.activeSelf)
            UIBlock.IsUIOpen = questBoardRoot.activeSelf;
    }
}
