using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FortyLineJudge : MonoBehaviour
{
    [Header("UI / Scene Settings")]
    public GameObject clearUIRoot;
    public bool stopTimeOnClear = true;

    [Header("Clear Animation")]
    public ClearFaridUI clearFaridUI;

    [Header("Clear Texts")]
    public Text clearMessageText;
    public Text timeText;

    [Header("Line Counter UI")]
    public TMP_Text linesRemainingText;
    public TMP_Text ppsText;

    public bool IsStageCleared { get; private set; } = false;
    public bool IsStageFailed { get; private set; } = false;

    [Header("40 Line Mode Settings")]
    public int targetLines = 40;
    int totalLinesCleared = 0;
    int totalPiecesLocked = 0;

    bool isEasyLikeMode = false;

    void Start()
    {
        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
        UpdateLinesRemainingText();
        UpdatePpsText();
    }

    void Update()
    {
        if (IsStageCleared || IsStageFailed) return;
        UpdatePpsText();
    }

    // Tetromino がロックされたときに Tetromino 側から呼ぶ
    public void OnPieceLocked(Tetromino piece, int linesCleared)
    {
        if (IsStageCleared || IsStageFailed) return;

        totalPiecesLocked++;

        // 加算
        totalLinesCleared += linesCleared;
        Debug.Log($"[40L] Total Lines: {totalLinesCleared}/{targetLines}");
        UpdateLinesRemainingText();
        UpdatePpsText();

        if (totalLinesCleared >= targetLines)
        {
            SoundManager.Instance?.PlaySE(SeType.StageClear);
            HandleStageClear();
        }
    }

    // Tetromino のスポーンに失敗したときに呼ぶ（Top Out）
    public void OnTopOut()
    {
        if (IsStageCleared || IsStageFailed) return;

        IsStageFailed = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null)
            controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        float failTime = GetClearTimeSeconds();
        UpdatePpsText();

        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(failTime / 60f);
            float seconds = failTime % 60f;
            timeText.text = $"Top Out: {minutes}:{seconds:00.00}";
        }

        if (clearMessageText != null)
            clearMessageText.text = "You topped out...";

        if (clearUIRoot != null)
            clearUIRoot.SetActive(true);

        SoundManager.Instance?.PlaySE(SeType.StageFail);

        if (stopTimeOnClear)
            Time.timeScale = 0f;

        if (clearFaridUI != null)
        {
            clearFaridUI.SetImageByIndex(1);
            clearFaridUI.Play();
        }
    }

    void HandleStageClear()
    {
        Debug.Log("HandleStageClear START");

        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null)
            controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        float clearTime = GetClearTimeSeconds();
        int spriteIndex = GetSpriteIndexByTime(clearTime, isEasyLikeMode);
        UpdatePpsText();

        // 先にテキストを更新
        UpdateClearTexts(clearTime, spriteIndex);

        if (clearFaridUI == null)
        {
            if (clearUIRoot != null)
                clearUIRoot.SetActive(true);

            if (stopTimeOnClear)
                Time.timeScale = 0f;

            Debug.Log("HandleStageClear: FaridUI=null -> panel only");
            return;
        }

        Debug.Log($"HandleStageClear: time={clearTime:F2}, easy={isEasyLikeMode}, spriteIndex={spriteIndex}");

        clearFaridUI.SetImageByIndex(spriteIndex);
        clearFaridUI.Play();

        if (stopTimeOnClear)
            Time.timeScale = 0f;

        Debug.Log("HandleStageClear END");
    }

    float GetClearTimeSeconds()
    {
        if (GameTimer.Instance == null)
            return 0f;

        return GameTimer.Instance.GetClearTime();
    }

    int GetSpriteIndexByTime(float seconds, bool easyMode)
    {
        // 40L スプリント基準
        if (seconds >= 180f) return 0;  // 3分以上
        if (seconds > 120f)  return 1;
        if (seconds > 90f)   return 2;
        if (seconds > 60f)   return 3;
        return 4; // 1分以内
    }

    void UpdateClearTexts(float clearTime, int spriteIndex)
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(clearTime / 60f);
            float seconds = clearTime % 60f;
            timeText.text = $"Time: {minutes}:{seconds:00.00}";
        }

        if (clearMessageText != null)
            clearMessageText.text = GetTimeCommentByIndex(spriteIndex);
    }

    string GetTimeCommentByIndex(int index)
    {
        // REN の雰囲気に合わせたコメント
        switch (index)
        {
            case 0:
                return ".....Well, unfortunately the reality is cruel";
            case 1:
                return "Sigh... You better try harder than that...";
            case 2:
                return "Not bad, but could be better...";
            case 3:
                return "Wow, you're pretty good at T-Spin!";
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

        totalLinesCleared = 0;
        totalPiecesLocked = 0;
        UpdateLinesRemainingText();
        UpdatePpsText();

        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void OnTitleSelectButton()
    {
        string titleSceneName = "Title";
        if (string.IsNullOrEmpty(titleSceneName))
        {
            Debug.LogWarning("FortyLineJudge: titleSceneName が設定されていません。");
            return;
        }

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    void UpdateLinesRemainingText()
    {
        if (linesRemainingText == null) return;

        int remaining = Mathf.Max(0, targetLines - totalLinesCleared);
        linesRemainingText.text = remaining.ToString();
    }

    void UpdatePpsText()
    {
        if (ppsText == null) return;

        float seconds = GetClearTimeSeconds();
        float pps = seconds > 0f ? totalPiecesLocked / seconds : 0f;
        ppsText.text = $"PPS: {pps:F2}";
    }
}
