using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClearFaridUI : MonoBehaviour
{
    [System.Serializable]
    public struct SpriteRectOverride
    {
        public bool overrideRect;
        public float x;
        public float y;
        public float startY;
        public float endY;
        public float width;
        public float height;
    }

    public RectTransform characterPanel;   // 動かすパネル
    public Image characterImage;           // Farid の画像

    public Sprite[] characterSprites;      // 0〜4 くらいで表情を入れる
    public SpriteRectOverride[] spriteRectOverrides;
    public int currentSpriteIndex = 0;

    public float slideDuration = 0.5f;
    public float xPosition = 0f;
    public float startY = -400f;
    public float endY = 200f;

    public bool usePopScale = true;
    public float popScale = 1.1f;
    public float popDuration = 0.15f;

    public GameObject stageClearPanel;
    public float panelDelay = 0.1f;

    bool isPlaying = false;
    Vector2 defaultImageAnchoredPosition;
    Vector2 defaultImageSizeDelta;
    Vector2 currentHiddenPosition;
    Vector2 currentShownPosition;

    void Awake()
    {
        if (characterPanel == null)
            characterPanel = GetComponent<RectTransform>();

        if (characterPanel != null)
        {
            RefreshPanelPositions();
            characterPanel.anchoredPosition = currentHiddenPosition;
            characterPanel.localScale = Vector3.one;
        }

        if (characterImage != null)
        {
            RectTransform imageRect = characterImage.rectTransform;
            defaultImageAnchoredPosition = imageRect.anchoredPosition;
            defaultImageSizeDelta = imageRect.sizeDelta;
        }

        if (characterImage != null && characterSprites.Length > 0)
        {
            currentSpriteIndex = Mathf.Clamp(currentSpriteIndex, 0, characterSprites.Length - 1);
            characterImage.sprite = characterSprites[currentSpriteIndex];
            ApplySpriteRectOverride(currentSpriteIndex);
        }

        gameObject.SetActive(false);

        if (stageClearPanel != null)
            stageClearPanel.SetActive(false);
    }

    // RENJudge から呼んで表情を選ぶ
    public void SetImageByIndex(int index)
    {
        if (characterSprites == null || characterSprites.Length == 0) return;

        index = Mathf.Clamp(index, 0, characterSprites.Length - 1);
        currentSpriteIndex = index;

        if (characterImage != null)
        {
            characterImage.sprite = characterSprites[currentSpriteIndex];
            ApplySpriteRectOverride(currentSpriteIndex);
        }
    }

    public void Play()
    {
        if (isPlaying) return;
        isPlaying = true;

        gameObject.SetActive(true);

        if (characterPanel != null)
        {
            RefreshPanelPositions();
            characterPanel.anchoredPosition = currentHiddenPosition;
            characterPanel.localScale = Vector3.one;
        }

        StartCoroutine(PlaySequenceRoutine());
    }

    IEnumerator PlaySequenceRoutine()
    {
        float t = 0f;

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;

            float ratio = Mathf.Clamp01(t / slideDuration);
            float eased = 1f - Mathf.Pow(1f - ratio, 3f);

            characterPanel.anchoredPosition =
                Vector2.Lerp(currentHiddenPosition, currentShownPosition, eased);

            yield return null;
        }

        characterPanel.anchoredPosition = currentShownPosition;

        if (usePopScale)
        {
            float tt = 0f;
            Vector3 start = Vector3.one * popScale;
            Vector3 end = Vector3.one;

            characterPanel.localScale = start;

            while (tt < popDuration)
            {
                tt += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(tt / popDuration);
                float eased = 1f - Mathf.Pow(1f - ratio, 3f);

                characterPanel.localScale = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            characterPanel.localScale = Vector3.one;
        }

        if (panelDelay > 0f)
            yield return new WaitForSecondsRealtime(panelDelay);

        if (stageClearPanel != null)
            stageClearPanel.SetActive(true);

        isPlaying = false;
    }

    void RefreshPanelPositions()
    {
        float targetX = xPosition;
        float targetStartY = startY;
        float targetEndY = endY;

        if (spriteRectOverrides != null &&
            currentSpriteIndex >= 0 &&
            currentSpriteIndex < spriteRectOverrides.Length &&
            spriteRectOverrides[currentSpriteIndex].overrideRect)
        {
            SpriteRectOverride rectOverride = spriteRectOverrides[currentSpriteIndex];
            targetX = rectOverride.x;
            targetStartY = rectOverride.startY;
            targetEndY = rectOverride.endY;
        }

        currentHiddenPosition = new Vector2(targetX, targetStartY);
        currentShownPosition = new Vector2(targetX, targetEndY);
    }

    void ApplySpriteRectOverride(int index)
    {
        if (characterImage == null)
            return;

        RectTransform imageRect = characterImage.rectTransform;
        RefreshPanelPositions();

        if (spriteRectOverrides == null || index < 0 || index >= spriteRectOverrides.Length || !spriteRectOverrides[index].overrideRect)
        {
            imageRect.anchoredPosition = defaultImageAnchoredPosition;
            imageRect.sizeDelta = defaultImageSizeDelta;
            return;
        }

        SpriteRectOverride rectOverride = spriteRectOverrides[index];
        imageRect.anchoredPosition = new Vector2(rectOverride.x, rectOverride.y);
        imageRect.sizeDelta = new Vector2(rectOverride.width, rectOverride.height);
    }
}
