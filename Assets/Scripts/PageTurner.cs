using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PageTurner : MonoBehaviour
{
    [Header("Pages Parent")]
    public Transform pagesParent;

    [Header("Optional UI")]
    public TMP_Text pageIndicatorText;
    public Button prevButton;
    public Button nextButton;

    private GameObject[] pages;
    private int index = 0;

    void Awake()
    {
        int count = pagesParent.childCount;
        pages = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            pages[i] = pagesParent.GetChild(i).gameObject;
        }
    }

    void OnEnable()
    {
        index = 0;
        ShowPage(index);
    }

    public void Next()
    {
        index = Mathf.Min(index + 1, pages.Length - 1);
        ShowPage(index);
    }

    public void Prev()
    {
        index = Mathf.Max(index - 1, 0);
        ShowPage(index);
    }

    void ShowPage(int i)
    {
        if (pages == null || pages.Length == 0) return;
        i = Mathf.Clamp(i, 0, pages.Length - 1);
        index = i;

        for (int p = 0; p < pages.Length; p++)
        {
            pages[p].SetActive(p == i);
        }

        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{i + 1} / {pages.Length}";

        if (prevButton != null)
            prevButton.gameObject.SetActive(i > 0);

        if (nextButton != null)
            nextButton.gameObject.SetActive(i < pages.Length - 1);
    }
}