using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AwakeChildrenObject : MonoBehaviour
{
    [Header("Settings")]
    public float delayBetweenChildren = 0.1f;
    public bool includeInactive = true;

    private void OnEnable()
    {
        StartCoroutine(EnableChildrenSequentially());
    }

    IEnumerator EnableChildrenSequentially()
    {
        List<Transform> children = new List<Transform>();

        // Collect direct children in hierarchy order
        foreach (Transform child in transform)
        {
            if (includeInactive || child.gameObject.activeSelf)
            {
                children.Add(child);
            }
        }

        // Disable them first (so we can re-enable in sequence)
        foreach (Transform child in children)
        {
            child.gameObject.SetActive(false);
        }

        // Sequentially enable
        for (int i = 0; i < children.Count; i++)
        {
            children[i].gameObject.SetActive(true);
            yield return new WaitForSeconds(delayBetweenChildren);
        }
    }
}