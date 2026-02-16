using TMPro;
using UnityEngine;
using System.Globalization;
using System.Text.RegularExpressions;

public class SpeedHUD : MonoBehaviour
{
    public static SpeedHUD Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] private TMP_Text speedText;

    [Header("Format")]
    [SerializeField] private string label = "Speed";
    [SerializeField] private float multiplier = 1f;     // set 0.5f if you want “km/s vibes”

    private void Awake()
    {
        Instance = this;
    }

    public void SetSpeed(float speedUnitsPerSec)
    {
        if (!speedText) return;
        float v = speedUnitsPerSec * multiplier;
        int iv = Mathf.RoundToInt(v * 2);
        speedText.text = iv.ToString("D4");
    }
    public float GetCurrentSpeed()
    {
        if (!speedText || string.IsNullOrEmpty(speedText.text))
            return 0f;

        // Extract first number in the string (handles "Speed: 123.4", "0m/s", etc)
        var match = Regex.Match(speedText.text, @"-?\d+(\.\d+)?");
        if (!match.Success)
            return 0f;

        if (float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        return 0f;
    }

    public void Clear()
    {
        if (!speedText) return;
        speedText.text = $"{label}: --";
    }
}
