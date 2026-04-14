using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GarbagePreviewLaneUI : MonoBehaviour
{
    [Header("Source")]
    public PendingGarbageSystem pendingGarbageSystem;

    [Header("UI")]
    public RectTransform laneRoot;
    public Image cellPrefab;

    [Header("Display")]
    public int visibleLines = 20;
    public float cellHeight = 24f;
    public float spacing = 2f;

    [Header("Colors")]
    public Color emptyColor = new Color(0f, 0f, 0f, 0f);
    public Color normalColor = new Color(0.75f, 0.75f, 0.75f, 0.95f);
    public Color dangerColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Danger")]
    public int dangerThreshold = 8;
    public bool blinkWhenDanger = true;
    public float blinkSpeed = 6f;

    private readonly List<Image> cells = new List<Image>();
    private int currentPending;

    private void Awake()
    {
        BuildCells();
        Refresh(0);
    }

    private void OnEnable()
    {
        if (pendingGarbageSystem != null)
        {
            pendingGarbageSystem.OnPendingGarbageChanged += Refresh;
            Refresh(pendingGarbageSystem.PendingLineCount);
        }
    }

    private void OnDisable()
    {
        if (pendingGarbageSystem != null)
            pendingGarbageSystem.OnPendingGarbageChanged -= Refresh;
    }

    private void Update()
    {
        if (!blinkWhenDanger || currentPending < dangerThreshold)
            return;

        float a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * blinkSpeed));

        for (int i = 0; i < cells.Count; i++)
        {
            if (i >= currentPending)
                continue;

            Color c = dangerColor;
            c.a *= a;
            cells[i].color = c;
        }
    }

    private void BuildCells()
    {
        if (laneRoot == null || cellPrefab == null)
            return;

        for (int i = laneRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(laneRoot.GetChild(i).gameObject);

        cells.Clear();

        for (int i = 0; i < visibleLines; i++)
        {
            Image img = Instantiate(cellPrefab, laneRoot);
            RectTransform rt = img.rectTransform;

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, i * (cellHeight + spacing));
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, cellHeight);

            img.color = emptyColor;
            cells.Add(img);
        }
    }

    public void Refresh(int pendingLines)
    {
        currentPending = Mathf.Max(0, pendingLines);

        for (int i = 0; i < cells.Count; i++)
        {
            if (i < currentPending)
            {
                cells[i].color = currentPending >= dangerThreshold ? dangerColor : normalColor;
            }
            else
            {
                cells[i].color = emptyColor;
            }
        }
    }
}