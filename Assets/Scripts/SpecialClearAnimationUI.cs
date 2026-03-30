using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialClearAnimationUI : MonoBehaviour
{
    public enum SpecialClearType
    {
        Tetris,
        TSpinDouble,
        TSpinTriple
    }

    [Header("Layout")]
    public RectTransform animationLayer;
    public Vector2 centerOffset = new Vector2(0f, 160f);

    [Header("Text Style")]
    public TMP_FontAsset fontAsset;
    public float tetrisFontSize = 72f;
    public float subFontSize = 54f;
    public float mainFontSize = 60f;
    public float stackedTextSpacing = 148f;

    [Header("Timing")]
    public float animationDuration = 1.1f;

    [Header("Particles")]
    public int tetrisParticleCount = 28;
    public float tetrisParticleWidth = 3f;
    public float tetrisParticleHeight = 5f;
    public float tetrisParticleDistance = 64f;
    public float tetrisParticleDistanceStep = 10f;
    public float tetrisParticleSwirlDegrees = 540f;
    public float tSpinDoubleBarWidth = 2f;
    public float tSpinDoubleBarHeight = 40f;
    public float tSpinDoubleParticleSize = 5f;
    public float tSpinDoubleParticleDistance = 45f;
    public float tSpinTripleRingParticleSize = 3f;
    public float tSpinTripleCenterParticleSize = 5f;
    public float tSpinTripleRingStartRadius = 30f;
    public float tSpinTripleRingEndRadius = 54f;
    public float tSpinTripleCenterParticleDistance = 40f;

    readonly List<Graphic> activeGraphics = new List<Graphic>();
    Coroutine playingRoutine;
    Sprite squareSprite;
    Sprite circleSprite;

    void Awake()
    {
        if (animationLayer == null)
            animationLayer = GetComponent<RectTransform>();

        squareSprite = CreateSquareSprite();
        circleSprite = CreateCircleSprite();
    }

    public void PlayTetris()
    {
        Play(SpecialClearType.Tetris);
    }

    public void PlayTSpinDouble()
    {
        Play(SpecialClearType.TSpinDouble);
    }

    public void PlayTSpinTriple()
    {
        Play(SpecialClearType.TSpinTriple);
    }

    public void Play(SpecialClearType clearType)
    {
        if (animationLayer == null)
            return;

        if (playingRoutine != null)
            StopCoroutine(playingRoutine);

        ClearActiveGraphics();
        playingRoutine = StartCoroutine(PlayRoutine(clearType));
    }

    IEnumerator PlayRoutine(SpecialClearType clearType)
    {
        switch (clearType)
        {
            case SpecialClearType.Tetris:
                BuildTetris();
                break;
            case SpecialClearType.TSpinDouble:
                BuildTSpinDouble();
                break;
            case SpecialClearType.TSpinTriple:
                BuildTSpinTriple();
                break;
        }

        Vector2 center = centerOffset;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            UpdateAnimation(clearType, center, t);
            yield return null;
        }

        UpdateAnimation(clearType, center, 1f);
        ClearActiveGraphics();
        playingRoutine = null;
    }

    void BuildTetris()
    {
        Color glow = new Color(1f, 0.87f, 0.22f, 1f);
        CreateCenteredText("TETRIS", new Vector2(0f, 0f), tetrisFontSize, FontStyles.Bold, Color.white, glow, 0.65f);

        Color[] palette =
        {
            new Color(1f, 0.95f, 0.4f, 1f),
            new Color(1f, 0.73f, 0.21f, 1f),
            new Color(1f, 0.49f, 0.16f, 1f),
            Color.white
        };

        for (int i = 0; i < tetrisParticleCount; i++)
        {
            Image particle = CreateRectParticle(new Vector2(tetrisParticleWidth, tetrisParticleHeight), palette[i % palette.Length]);
            particle.rectTransform.localRotation = Quaternion.identity;
        }
    }

    void BuildTSpinDouble()
    {
        Color accent = new Color(0.73f, 0.33f, 1f, 1f);
        CreateStackedText("T-SPIN", "DOUBLE", accent);

        for (int i = 0; i < 2; i++)
        {
            Image line = CreateRectParticle(new Vector2(tSpinDoubleBarWidth, tSpinDoubleBarHeight), new Color(accent.r, accent.g, accent.b, 0.75f));
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
        }

        for (int i = 0; i < 2; i++)
            CreateCircleParticle(tSpinDoubleParticleSize, new Color(accent.r, accent.g, accent.b, 0.85f));
    }

    void BuildTSpinTriple()
    {
        Color accent = new Color(0.22f, 0.95f, 1f, 1f);
        CreateStackedText("T-SPIN", "TRIPLE", accent);

        for (int i = 0; i < 18; i++)
            CreateCircleParticle(tSpinTripleRingParticleSize, new Color(accent.r, accent.g, accent.b, 0.72f));

        for (int i = 0; i < 3; i++)
            CreateCircleParticle(tSpinTripleCenterParticleSize, new Color(accent.r, accent.g, accent.b, 0.85f));
    }

    void UpdateAnimation(SpecialClearType clearType, Vector2 center, float t)
    {
        int graphicIndex = 0;

        if (clearType == SpecialClearType.Tetris)
        {
            TMP_Text text = activeGraphics[graphicIndex++] as TMP_Text;
            UpdateCenteredText(text.rectTransform, text, center, 0.85f, 1.45f, t);

            for (int i = 0; i < tetrisParticleCount; i++)
            {
                Image particle = activeGraphics[graphicIndex++] as Image;
                float progress = t;
                float baseAngle = ((float)i / Mathf.Max(tetrisParticleCount - 1, 1)) * Mathf.PI * 2f;
                float swirlAngle = baseAngle + Mathf.Deg2Rad * tetrisParticleSwirlDegrees * progress;
                float distance = tetrisParticleDistance + (i % 8) * tetrisParticleDistanceStep;
                Vector2 offset = new Vector2(Mathf.Cos(swirlAngle), Mathf.Sin(swirlAngle)) * distance * progress;
                particle.rectTransform.anchoredPosition = center + offset;
                float tangentDegrees = swirlAngle * Mathf.Rad2Deg + 90f;
                particle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tangentDegrees);
                particle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, progress);
                SetGraphicAlpha(particle, 1f - progress);
            }
            return;
        }

        TMP_Text topText = activeGraphics[graphicIndex++] as TMP_Text;
        TMP_Text bottomText = activeGraphics[graphicIndex++] as TMP_Text;
        float textEndScale = clearType == SpecialClearType.TSpinDouble ? 1.15f : 1.2f;
        float textStartScale = clearType == SpecialClearType.TSpinDouble ? 0.8f : 0.85f;
        UpdateCenteredText(topText.rectTransform, topText, center + new Vector2(0f, stackedTextSpacing), textStartScale, textEndScale, t);
        UpdateCenteredText(bottomText.rectTransform, bottomText, center + new Vector2(0f, -stackedTextSpacing), textStartScale, textEndScale, t);

        if (clearType == SpecialClearType.TSpinDouble)
        {
            for (int i = 0; i < 2; i++)
            {
                Image line = activeGraphics[graphicIndex++] as Image;
                float baseRotation = i * 90f;
                line.rectTransform.anchoredPosition = center;
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseRotation + 270f * t);
                SetGraphicAlpha(line, Mathf.Lerp(0.7f, 0f, t));
            }

            for (int i = 0; i < 2; i++)
            {
                Image particle = activeGraphics[graphicIndex++] as Image;
                float angle = i * Mathf.PI;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * tSpinDoubleParticleDistance * t;
                particle.rectTransform.anchoredPosition = center + offset;
                SetGraphicAlpha(particle, Mathf.Lerp(0.8f, 0f, t));
            }
            return;
        }

        int ringParticleCount = 18;
        for (int i = 0; i < ringParticleCount; i++)
        {
            Image particle = activeGraphics[graphicIndex++] as Image;
            float baseAngle = ((float)i / ringParticleCount) * Mathf.PI * 2f;
            float angle = baseAngle + Mathf.PI * 4f * t;
            float radius = Mathf.Lerp(tSpinTripleRingStartRadius, tSpinTripleRingEndRadius, t);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            particle.rectTransform.anchoredPosition = center + offset;
            particle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.05f, t);
            SetGraphicAlpha(particle, Mathf.Lerp(0.7f, 0f, t));
        }

        for (int i = 0; i < 3; i++)
        {
            Image particle = activeGraphics[graphicIndex++] as Image;
            float angle = i * Mathf.PI * 2f / 3f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * tSpinTripleCenterParticleDistance * t;
            particle.rectTransform.anchoredPosition = center + offset;
            SetGraphicAlpha(particle, Mathf.Lerp(0.85f, 0f, t));
        }
    }

    void UpdateCenteredText(RectTransform rect, TMP_Text text, Vector2 position, float startScale, float endScale, float t)
    {
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
        SetGraphicAlpha(text, 1f - t);
    }

    TMP_Text CreateCenteredText(string content, Vector2 offset, float fontSize, FontStyles fontStyle, Color textColor, Color glowColor, float glowAlpha)
    {
        GameObject go = new GameObject(content, typeof(RectTransform));
        go.transform.SetParent(animationLayer, false);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = textColor;
        if (fontAsset != null)
            text.font = fontAsset;

        text.enableVertexGradient = false;
        text.fontSharedMaterial = text.fontMaterial;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(glowColor.r, glowColor.g, glowColor.b, glowAlpha);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 120f);
        rect.anchoredPosition = offset;

        activeGraphics.Add(text);
        return text;
    }

    void CreateStackedText(string top, string bottom, Color accent)
    {
        CreateCenteredText(top, new Vector2(0f, stackedTextSpacing), subFontSize, FontStyles.Bold, accent, accent, 0.8f);
        CreateCenteredText(bottom, new Vector2(0f, -stackedTextSpacing), mainFontSize, FontStyles.Bold, accent, accent, 0.8f);
    }

    Image CreateRectParticle(Vector2 size, Color color)
    {
        GameObject go = new GameObject("Particle", typeof(RectTransform));
        go.transform.SetParent(animationLayer, false);

        Image image = go.AddComponent<Image>();
        image.sprite = squareSprite;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = centerOffset;

        activeGraphics.Add(image);
        return image;
    }

    Image CreateCircleParticle(float size, Color color)
    {
        GameObject go = new GameObject("Dot", typeof(RectTransform));
        go.transform.SetParent(animationLayer, false);

        Image image = go.AddComponent<Image>();
        image.sprite = circleSprite != null ? circleSprite : squareSprite;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = centerOffset;

        activeGraphics.Add(image);
        return image;
    }

    void ClearActiveGraphics()
    {
        for (int i = 0; i < activeGraphics.Count; i++)
        {
            if (activeGraphics[i] != null)
                Destroy(activeGraphics[i].gameObject);
        }

        activeGraphics.Clear();
    }

    void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = Mathf.Clamp01(alpha);
        graphic.color = color;
    }

    Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(8, 8, TextureFormat.ARGB32, false);
        Color[] pixels = new Color[8 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = "SpecialClearSquare";

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 8f);
    }

    Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 1f;
        float radiusSqr = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                pixels[y * size + x] = delta.sqrMagnitude <= radiusSqr ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.name = "SpecialClearCircle";

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), size);
    }
}
