using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PerfectClearJudge : MonoBehaviour
{
    public GameObject clearUIRoot;
    public string stageSelectSceneName = "TechniqueSelect";
    public string nextStageSceneName = "";
    public bool stopTimeOnClear = true;

    public ClearFaridUI clearFaridUI;

    public Text clearMessageText;
    public Text timeText;

    public Board board;

    public bool IsStageCleared { get; private set; } = false;

    private bool hasEverHadBlock = false;

    void Start()
    {
        if (board == null) board = FindObjectOfType<Board>();

        if (board == null)
        {
            return;
        }

        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
    }

    void Update()
    {
        if (IsStageCleared) return;
        if (board == null) return;

        bool empty = IsPerfectClear();

        // 最初から空盤面で即クリアしないため
        if (!empty)
        {
            hasEverHadBlock = true;
        }

        if (hasEverHadBlock && empty)
        {
            
            SoundManager.Instance?.PlaySE(SeType.StageClear);
            HandleStageClear();
        }
    }

    bool IsPerfectClear()
    {
        for (int y = 0; y < board.size.y; y++)
        {
            for (int x = 0; x < board.size.x; x++)
            {
                if (board.IsOccupiedInBounds(x, y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    void HandleStageClear()
    {
        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null) controlUI.HideAllUI();

        if (GameTimer.Instance != null) GameTimer.Instance.StopTimer();

        float clearTime = GetClearTimeSeconds();
        SaveManager.AddRecordedTime(clearTime);

        int spriteIndex = GetSpriteIndexByTime(clearTime);
        UpdateClearTexts(clearTime, spriteIndex);

        if (clearUIRoot != null)
            clearUIRoot.SetActive(true);

        if (clearFaridUI != null)
        {
            clearFaridUI.SetImageByIndex(spriteIndex);
            clearFaridUI.Play();
        }

        if (stopTimeOnClear) Time.timeScale = 0f;
    }

    float GetClearTimeSeconds()
    {
        if (GameTimer.Instance == null) return 0f;
        return GameTimer.Instance.GetClearTime();
    }

    int GetSpriteIndexByTime(float seconds)
    {
        if (seconds >= 180f) return 0;
        if (seconds > 120f) return 1;
        if (seconds > 60f) return 2;
        if (seconds > 30f) return 3;
        return 4;
    }

    void UpdateClearTexts(float clearTime, int spriteIndex)
    {
        if (timeText != null)
            timeText.text = FormatTime(clearTime);

        if (clearMessageText != null)
            clearMessageText.text = GetClearComment(spriteIndex);
    }

    string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        int cs = (int)((seconds - Mathf.Floor(seconds)) * 100f);
        return $"{m}:{s:00}.{cs:00}";
    }

    string GetClearComment(int index)
    {
        switch (index)
        {
            case 0: return "You made it! (barely...)";
            case 1: return "You made it! Keep practicing!";
            case 2: return "You made it! Not bad!";
            case 3: return "You made it! Great job!";
            default: return "Perfect Clear!";
        }
    }

    public void OnRetryButton()
    {
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnStageSelectButton()
    {
        if (string.IsNullOrEmpty(stageSelectSceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    public void OnNextStageButton()
    {
        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }
}