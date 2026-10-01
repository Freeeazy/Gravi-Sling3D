using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    [Header("Slot Labels")]
    public TMP_Text saveName;
    public TMP_Text playerName;
    public TMP_Text playtime;
    public TMP_Text creditsEarned;
    public TMP_Text reputation;
    public TMP_Text deliveriesCompleted;
    public TMP_Text lastPlayed;

    [Header("Slot Buttons")]
    public GameObject deleteSlotButton;
}