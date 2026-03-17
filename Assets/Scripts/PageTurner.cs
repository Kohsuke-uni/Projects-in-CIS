using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PageTurner : MonoBehaviour
{
    [System.Serializable]
    public struct ReturnSceneRule
    {
        public string nameContains;
        public string sceneName;
    }

    [Header("Pages Parent")]
    public Transform pagesParent;

    [Header("Optional UI")]
    public TMP_Text pageIndicatorText;
    public Button prevButton;
    public Button nextButton;

    [Header("Scene Settings")]
    public string titleSceneName = "Title";
    public ReturnSceneRule[] returnSceneRules;
    [Tooltip("ON なら最終ページで Return ボタンを表示しない")]
    public bool hideReturnButtonOnLastPage = false;

    private GameObject[] pages;
    private int index = 0;

    void Awake()
    {
        RebuildPages();
    }

    void OnEnable()
    {
        if (pages == null || pages.Length == 0)
            RebuildPages();

        index = 0;
        ShowPage(index);
    }

    public void SetPagesParent(Transform newPagesParent)
    {
        pagesParent = newPagesParent;
        RebuildPages();
        index = 0;
        ShowPage(index);
    }

    public void Next()
    {
        // If we're on the last page, go to return scene
        if (index >= pages.Length - 1)
        {
            SceneManager.LoadScene(GetReturnSceneName());
            return;
        }

        index++;
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

        if (nextButton != null)
        {
            TMP_Text buttonText = nextButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                if (i >= pages.Length - 1)
                    buttonText.text = "Return";
                else
                    buttonText.text = "Next";
            }
        }

        if (prevButton != null)
            prevButton.gameObject.SetActive(i > 0);

        if (nextButton != null)
        {
            bool isLastPage = i >= pages.Length - 1;
            bool hideReturn = isLastPage && hideReturnButtonOnLastPage;
            nextButton.gameObject.SetActive(!hideReturn);
        }
    }

    private void RebuildPages()
    {
        if (pagesParent == null)
        {
            pages = System.Array.Empty<GameObject>();
            return;
        }

        int count = pagesParent.childCount;
        pages = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            pages[i] = pagesParent.GetChild(i).gameObject;
        }
    }

    private string GetReturnSceneName()
    {
        string pagesName = pagesParent != null ? pagesParent.name : string.Empty;
        if (!string.IsNullOrWhiteSpace(pagesName) && returnSceneRules != null)
        {
            for (int i = 0; i < returnSceneRules.Length; i++)
            {
                string token = returnSceneRules[i].nameContains;
                string target = returnSceneRules[i].sceneName;
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(target))
                    continue;

                if (pagesName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return target;
            }
        }

        if (!string.IsNullOrWhiteSpace(pagesName) &&
            pagesName.IndexOf("SZ", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "SRS_SZ_Stage Select";
        }
        if (!string.IsNullOrWhiteSpace(pagesName) &&
            pagesName.IndexOf("JL", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "SRS_JL_Stage Select";
        }
        if (!string.IsNullOrWhiteSpace(pagesName) &&
            pagesName.IndexOf("SRS I", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "SRS_I_Stage Select";
        }
        if (!string.IsNullOrWhiteSpace(pagesName) &&
            pagesName.IndexOf("SRS T", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "SRS_T_Stage Select";
        }

        return titleSceneName;
    }
}
