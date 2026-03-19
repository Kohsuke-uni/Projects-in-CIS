using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TotalRecordedTimeText : MonoBehaviour
{
    [Header("References")]
    public TMP_Text tmpText;
    public Text uiText;

    [Header("Format")]
    public string prefix = "Total Time Played:\n";

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
        float totalSeconds = SaveManager.GetTotalRecordedTimeSeconds();
        string value = prefix + FormatTime(totalSeconds);

        if (tmpText != null)
            tmpText.text = value;

        if (uiText != null)
            uiText.text = value;
    }

    private string FormatTime(float totalSeconds)
    {
        int wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(totalSeconds));
        int hours = wholeSeconds / 3600;
        int minutes = (wholeSeconds % 3600) / 60;
        int seconds = wholeSeconds % 60;

        if (hours > 0)
            return $"{hours}:{minutes:00}:{seconds:00}";

        return $"{minutes}:{seconds:00}";
    }
}
