using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AsteroidSFXPitch : MonoBehaviour
{
    [Header("Pitch Range")]
    [SerializeField] private float minPitch = 0.6f;
    [SerializeField] private float maxPitch = 1.4f;

    private AudioSource audioSource;

    private void OnEnable()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.pitch = Random.Range(minPitch, maxPitch);
    }
}