using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PracticeJudge : MonoBehaviour
{
    public enum PracticeType
    {
        TSpinDouble,
        TSpinTriple,
        SZSingle,
        SZDouble,
        SZTriple,
        JLSingle,
        JLDouble,
        JLTriple,
        ISingle,
        IFull,
        TSingle,
        TDouble
    }

    [Header("Practice Type")]
    public PracticeType practiceType = PracticeType.TSpinDouble;

    [Header("UI / Scene Settings")]
    public GameObject clearUIRoot;
    public string stageSelectSceneName = "TechniqueSelect";
    public string nextStageSceneName = "";
    public bool stopTimeOnClear = true;

    [Header("Clear Animation")]
    public ClearFaridUI clearFaridUI;

    [Header("Clear Texts")]
    public Text clearMessageText;
    public Text timeText;

    [Header("Easy Detection")]
    public bool detectEasyLikeModeFromSceneName = true;
    public string[] easySceneKeywords;

    public bool IsStageCleared { get; private set; } = false;

    bool isEasyLikeMode = false;

    void Start()
    {
        isEasyLikeMode = ResolveEasyLikeMode();

        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
    }

    public void OnPieceLocked(Tetromino piece, int linesCleared)
    {
        if (IsStageCleared) return;
        if (!IsTargetPiece(piece)) return;

        if (isEasyLikeMode)
        {
            if (IsSuccessLineCount(linesCleared))
            {
                SoundManager.Instance?.PlaySE(SeType.StageClear);
                HandleStageClear();
            }
            else
            {
                SoundManager.Instance?.PlaySE(SeType.StageFail);
                ForceRestartScene();
            }
        }
        else
        {
            if (IsSuccessLineCount(linesCleared))
            {
                SoundManager.Instance?.PlaySE(SeType.StageClear);
                HandleStageClear();
            }
        }
    }

    bool IsTargetPiece(Tetromino piece)
    {
        if (piece == null) return false;

        if (practiceType == PracticeType.TSpinDouble || practiceType == PracticeType.TSpinTriple)
            return piece.typeIndex == 5; // T
        if (practiceType == PracticeType.SZSingle || practiceType == PracticeType.SZDouble || practiceType == PracticeType.SZTriple)
            return piece.typeIndex == 4 || piece.typeIndex == 6; // S or Z
        if (practiceType == PracticeType.JLSingle || practiceType == PracticeType.JLDouble || practiceType == PracticeType.JLTriple)
            return piece.typeIndex == 1 || piece.typeIndex == 2; // J or L
        if (practiceType == PracticeType.ISingle || practiceType == PracticeType.IFull)
            return piece.typeIndex == 0; // I
        if (practiceType == PracticeType.TSingle || practiceType == PracticeType.TDouble)
            return piece.typeIndex == 5; // T
        return piece.typeIndex == 5; // デフォルトは T
    }

    bool IsSuccessLineCount(int linesCleared)
    {
        switch (practiceType)
        {
            case PracticeType.TSpinDouble:
                return linesCleared == 2;
            case PracticeType.TSpinTriple:
                return linesCleared == 3;
            case PracticeType.SZSingle:
                return linesCleared == 1;
            case PracticeType.SZDouble:
                return linesCleared == 2;
            case PracticeType.SZTriple:
                return linesCleared == 3;
            case PracticeType.JLSingle:
                return linesCleared == 1;
            case PracticeType.JLDouble:
                return linesCleared == 2;
            case PracticeType.JLTriple:
                return linesCleared == 3;
            case PracticeType.ISingle:
                return linesCleared == 1;
            case PracticeType.IFull:
                return linesCleared == 4;
            case PracticeType.TSingle:
                return linesCleared == 1;
            case PracticeType.TDouble:
                return linesCleared == 2;
            default:
                return linesCleared == 2;
        }
    }

    bool ResolveEasyLikeMode()
    {
        if (!detectEasyLikeModeFromSceneName) return false;

        string sceneName = SceneManager.GetActiveScene().name;
        string[] keywords = (easySceneKeywords != null && easySceneKeywords.Length > 0)
            ? easySceneKeywords
            : GetDefaultEasyKeywords();

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (!string.IsNullOrEmpty(keyword) && sceneName.Contains(keyword))
                return true;
        }

        return false;
    }

    string[] GetDefaultEasyKeywords()
    {
        if (practiceType == PracticeType.TSpinTriple)
            return new[] { "TST_E", "TST_B" };
        if (practiceType == PracticeType.SZDouble
            || practiceType == PracticeType.SZTriple 
            || practiceType == PracticeType.SZSingle)
            return new[] { "SRS_SZ" };
        if (practiceType == PracticeType.JLSingle
            || practiceType == PracticeType.JLDouble
            || practiceType == PracticeType.JLTriple)
            return new[] { "SRS_JL" };
        if (practiceType == PracticeType.ISingle || practiceType == PracticeType.IFull)
            return new[] { "SRS_I" };
        if (practiceType == PracticeType.TSingle || practiceType == PracticeType.TDouble)
            return new[] { "SRS_T" };

        return new[] { "TSD_E", "TSD_B" };
    }

    void HandleStageClear()
    {
        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null)
            controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        float clearTime = GetClearTimeSeconds();
        int spriteIndex = GetSpriteIndexByTime(clearTime, isEasyLikeMode);
        UpdateClearTexts(clearTime, spriteIndex);

        if (clearFaridUI == null)
        {
            if (clearUIRoot != null)
                clearUIRoot.SetActive(true);

            if (stopTimeOnClear)
                Time.timeScale = 0f;

            return;
        }

        clearFaridUI.SetImageByIndex(spriteIndex);
        clearFaridUI.Play();

        if (stopTimeOnClear)
            Time.timeScale = 0f;
    }

    float GetClearTimeSeconds()
    {
        if (GameTimer.Instance == null)
            return 0f;

        return GameTimer.Instance.GetClearTime();
    }

    int GetSpriteIndexByTime(float seconds, bool easyMode)
    {
        if (easyMode)
        {
            if (seconds >= 6f) return 0;
            if (seconds > 4f) return 1;
            if (seconds > 3f) return 2;
            if (seconds > 2f) return 3;
            return 4;
        }

        if (seconds >= 180f) return 0;
        if (seconds > 120f) return 1;
        if (seconds > 60f) return 2;
        if (seconds > 30f) return 3;
        return 4;
    }

    void UpdateClearTexts(float clearTime, int spriteIndex)
    {
        if (timeText != null)
            timeText.text = $"You took {clearTime:F2} seconds";

        if (clearMessageText != null)
            clearMessageText.text = GetTimeCommentByIndex(spriteIndex);
    }

    string GetTimeCommentByIndex(int index)
    {
        switch (index)
        {
            case 0:
                return ".....Well, unfortunately the reality is cruel";
            case 1:
                return "Sigh... You better try harder than that...";
            case 2:
                return "Not bad, but could be better...";
            case 3:
                return "Wow, you're pretty good at this!";
            default:
                return "You are a God Tier Tetris Player";
        }
    }

    void ForceRestartScene()
    {
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void OnRetryButton()
    {
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
        if (clearFaridUI != null)
            clearFaridUI.gameObject.SetActive(false);

        if (GameTimer.Instance != null)
            GameTimer.Instance.ResetTimer();

        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void OnNextStageButton()
    {
        string targetSceneName = GetAutoNextSceneNameOrFallback();
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("PracticeJudge: 次ステージ名を解決できませんでした。");
            return;
        }

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        Time.timeScale = 1f;
        SceneManager.LoadScene(targetSceneName);
    }

    public void OnStageSelectButton()
    {
        if (string.IsNullOrEmpty(stageSelectSceneName))
        {
            Debug.LogWarning("PracticeJudge: stageSelectSceneName が設定されていません。");
            return;
        }

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    string GetAutoNextSceneNameOrFallback()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (TryBuildNextSceneName(currentSceneName, out string autoNext)
            && Application.CanStreamedLevelBeLoaded(autoNext))
        {
            return autoNext;
        }

        if (!string.IsNullOrEmpty(nextStageSceneName)
            && Application.CanStreamedLevelBeLoaded(nextStageSceneName))
        {
            return nextStageSceneName;
        }

        const string titleSceneName = "Title";
        if (Application.CanStreamedLevelBeLoaded(titleSceneName))
            return titleSceneName;

        return string.Empty;
    }

    bool TryBuildNextSceneName(string sceneName, out string nextSceneName)
    {
        nextSceneName = string.Empty;
        if (string.IsNullOrEmpty(sceneName)) return false;

        int end = sceneName.Length - 1;
        int start = end;

        while (start >= 0 && char.IsDigit(sceneName[start]))
            start--;

        start++;
        if (start > end) return false;

        string numberPart = sceneName.Substring(start, end - start + 1);
        if (!int.TryParse(numberPart, out int currentNumber)) return false;

        int nextNumber = currentNumber + 1;
        string prefix = sceneName.Substring(0, start);
        nextSceneName = prefix + nextNumber;
        return true;
    }
}
