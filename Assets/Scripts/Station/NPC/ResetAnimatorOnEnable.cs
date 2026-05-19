using UnityEngine;

public class ResetAnimatorOnEnable : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string defaultStateName = "Normal";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (animator == null) return;

        animator.Play(defaultStateName, 0, 0f);
        animator.Update(0f);
    }
}