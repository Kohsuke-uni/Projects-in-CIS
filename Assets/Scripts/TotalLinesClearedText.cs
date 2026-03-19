using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TotalLinesClearedText : MonoBehaviour
{
    [Header("References")]
    public TMP_Text tmpText;
    public Text uiText;

    [Header("Format")]
    public string prefix = "Total Lines Cleared:\n";

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
        string value = prefix + SaveManager.GetTotalLinesCleared();

        if (tmpText != null)
            tmpText.text = value;

        if (uiText != null)
            uiText.text = value;
    }
}
