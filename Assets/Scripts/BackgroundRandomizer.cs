using UnityEngine;
using UnityEngine.UI;

public class BackgroundRandomizer : MonoBehaviour
{
    [Header("Targets")]
    public Image targetImage;
    public SpriteRenderer targetSpriteRenderer;

    [Header("Background Options")]
    public Sprite[] backgroundSprites;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetSpriteRenderer == null)
            targetSpriteRenderer = GetComponent<SpriteRenderer>();

        ApplyRandomBackground();
    }

    public void ApplyRandomBackground()
    {
        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogWarning("BackgroundRandomizer: backgroundSprites is empty.");
            return;
        }

        Sprite randomSprite = backgroundSprites[Random.Range(0, backgroundSprites.Length)];

        if (targetImage != null)
            targetImage.sprite = randomSprite;

        if (targetSpriteRenderer != null)
            targetSpriteRenderer.sprite = randomSprite;

        if (targetImage == null && targetSpriteRenderer == null)
            Debug.LogWarning("BackgroundRandomizer: No Image or SpriteRenderer target assigned.");
    }
}
