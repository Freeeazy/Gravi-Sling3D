using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ContractCardJitterManager : MonoBehaviour
{
    public static ContractCardJitterManager Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private VerticalLayoutGroup mainVerticalLayoutGroup;

    [Header("Jitter Settings")]
    [SerializeField] private float baseXPosition = 170f;
    [SerializeField] private float jitterAmount = 20f;
    [SerializeField] private float jitterDuration = 0.25f;
    [SerializeField] private float jitterStepTime = 0.035f;
    [SerializeField] private bool resetToBaseXAfterJitter = true;

    private readonly List<RectTransform> jitterTargets = new List<RectTransform>();
    private Coroutine jitterRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterCard(RectTransform cardRect)
    {
        if (cardRect == null)
            return;

        if (!jitterTargets.Contains(cardRect))
            jitterTargets.Add(cardRect);
    }

    public void UnregisterCard(RectTransform cardRect)
    {
        if (cardRect == null)
            return;

        jitterTargets.Remove(cardRect);
    }

    public void JitterAllCards()
    {
        if (jitterRoutine != null)
            StopCoroutine(jitterRoutine);

        jitterRoutine = StartCoroutine(JitterRoutine());
    }

    private IEnumerator JitterRoutine()
    {
        SetLayoutEnabled(false);

        float elapsed = 0f;

        while (elapsed < jitterDuration)
        {
            for (int i = jitterTargets.Count - 1; i >= 0; i--)
            {
                RectTransform target = jitterTargets[i];

                if (target == null)
                {
                    jitterTargets.RemoveAt(i);
                    continue;
                }

                float xOffset = Random.Range(-jitterAmount, jitterAmount);

                target.anchoredPosition = new Vector2(
                    baseXPosition + xOffset,
                    target.anchoredPosition.y
                );
            }

            yield return new WaitForSeconds(jitterStepTime);
            elapsed += jitterStepTime;
        }

        if (resetToBaseXAfterJitter)
        {
            ResetAllCardsToBaseX();
        }

        SetLayoutEnabled(true);

        jitterRoutine = null;
    }

    public void ResetAllCardsToBaseX()
    {
        for (int i = jitterTargets.Count - 1; i >= 0; i--)
        {
            RectTransform target = jitterTargets[i];

            if (target == null)
            {
                jitterTargets.RemoveAt(i);
                continue;
            }

            target.anchoredPosition = new Vector2(
                baseXPosition,
                target.anchoredPosition.y
            );
        }
    }

    private void SetLayoutEnabled(bool enabled)
    {
        if (mainVerticalLayoutGroup != null)
            mainVerticalLayoutGroup.enabled = enabled;
    }
}