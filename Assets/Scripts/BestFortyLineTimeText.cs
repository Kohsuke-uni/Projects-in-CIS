using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BestFortyLineTimeText : MonoBehaviour
{
    [Header("References")]
    public TMP_Text tmpText;
    public Text uiText;

    [Header("Format")]
    public string prefix = "Best Time:\n";
    public string noRecordText = "--:--";

    private void Awake()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (uiText == null)
            uiText = GetComponent<Text>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        float bestTime = SaveManager.GetBestFortyLineTimeSeconds();
        string value = prefix + FormatTime(bestTime);

        if (tmpText != null)
            tmpText.text = value;

        if (uiText != null)
            uiText.text = value;
    }

    private string FormatTime(float seconds)
    {
        if (seconds < 0f)
            return noRecordText;

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes}:{remainingSeconds:00.00}";
    }
}
