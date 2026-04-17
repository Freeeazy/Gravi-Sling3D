using UnityEngine;

public class TutorialRing : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private bool disableAfterHit = true;

    private bool used = false;

    private void OnTriggerEnter(Collider other)
    {
        if (used)
            return;

        if (!other.CompareTag("Player"))
            return;

        used = true;

        if (tutorialManager != null)
            tutorialManager.RegisterRingHit();

        if (disableAfterHit)
            gameObject.SetActive(false);
    }
}