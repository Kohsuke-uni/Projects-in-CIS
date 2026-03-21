using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MinoAppearanceButton : MonoBehaviour
{
    [Header("Selection")]
    public bool selectsClassicMinos;
    public MinoAppearanceButton[] buttonGroup;

    [Header("Visuals")]
    public float selectedAlpha = 1f;
    public float unselectedAlpha = 0.8f;

    [Header("Persistence")]
    public bool loadSelectionFromSaveOnEnable = true;
    public bool saveSelectionOnClick = true;

    private void OnEnable()
    {
        if (!loadSelectionFromSaveOnEnable)
            return;

        bool useClassicMinos = SaveManager.GetUseClassicMinos();
        ApplyGroupVisuals(useClassicMinos);
    }

    public void OnClick()
    {
        if (saveSelectionOnClick)
            SaveManager.SetUseClassicMinos(selectsClassicMinos);

        ApplyGroupVisuals(selectsClassicMinos);
    }

    private void ApplyGroupVisuals(bool useClassicMinos)
    {
        if (buttonGroup == null || buttonGroup.Length == 0)
        {
            SetChildrenAlpha(selectsClassicMinos == useClassicMinos ? selectedAlpha : unselectedAlpha);
            return;
        }

        for (int i = 0; i < buttonGroup.Length; i++)
        {
            MinoAppearanceButton button = buttonGroup[i];
            if (button == null)
                continue;

            bool isSelected = button.selectsClassicMinos == useClassicMinos;
            button.SetChildrenAlpha(isSelected ? selectedAlpha : unselectedAlpha);
        }
    }

    private void SetChildrenAlpha(float alpha)
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            Color color = sr.color;
            color.a = alpha;
            sr.color = color;
        }

        TMP_Text[] tmpTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text tmp = tmpTexts[i];
            Color color = tmp.color;
            color.a = alpha;
            tmp.color = color;
        }
    }
}
