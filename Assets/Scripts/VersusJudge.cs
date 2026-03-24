using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VersusJudge : MonoBehaviour
{
    [Header("Boards")]
    public Board playerBoard;
    public Board cpuBoard;

    [Header("UI")]
    public GameObject clearUIRoot;
    public Text resultMessageText;
    public Text clearMessageText;
    public Text timeText;

    [Header("Optional Effects")]
    public ClearFaridUI clearFaridUI;

    [Header("Scene Settings")]
    public string retrySceneName = "";
    public string stageSelectSceneName = "StageSelect";
    public bool stopTimeOnFinish = true;

    [Header("Win Messages")]
    [TextArea(1, 3)] public string winResultMessage = "YOU WIN";
    [TextArea(1, 3)] public string winClearMessage = "You defeated the CPU!";

    [Header("Lose Messages")]
    [TextArea(1, 3)] public string loseResultMessage = "YOU LOSE";
    [TextArea(1, 3)] public string loseClearMessage = "You were topped out...";

    public bool IsStageCleared { get; private set; } = false;
    public bool PlayerWon { get; private set; } = false;
    public bool PlayerLost { get; private set; } = false;

    private int playerRen = 0;
    private int cpuRen = 0;
    private bool playerLastWasB2B = false;
    private bool cpuLastWasB2B = false;

    private void Start()
    {
        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);

        if (clearFaridUI != null)
            clearFaridUI.gameObject.SetActive(false);

        IsStageCleared = false;
        PlayerWon = false;
        PlayerLost = false;

        playerRen = 0;
        cpuRen = 0;
        playerLastWasB2B = false;
        cpuLastWasB2B = false;
    }

    public void OnTopOut(Board board)
    {
        if (IsStageCleared) return;
        if (board == null) return;

        if (board == playerBoard)
        {
            PlayerLost = true;
            PlayerWon = false;
            HandleFinish(false);
            return;
        }

        if (board == cpuBoard)
        {
            PlayerWon = true;
            PlayerLost = false;
            HandleFinish(true);
        }
    }

    public void OnLinesCleared(Tetromino piece, Board sender, int linesCleared)
    {
        if (IsStageCleared) return;
        if (piece == null) return;
        if (sender == null) return;

        if (linesCleared <= 0)
        {
            ResetRenForBoard(sender);
            ResetB2BIfNeeded(sender, false);
            return;
        }

        int garbage = CalculateGarbage(piece, sender, linesCleared);
        if (garbage <= 0) return;

        if (sender == playerBoard && cpuBoard != null)
        {
            cpuBoard.AddGarbageLines(garbage);
        }
        else if (sender == cpuBoard && playerBoard != null)
        {
            playerBoard.AddGarbageLines(garbage);
        }
    }

    private int CalculateGarbage(Tetromino piece, Board sender, int lines)
    {
        bool isTSpin = IsTSpin(piece, lines);
        bool isB2BAction = false;
        int baseGarbage = 0;

        if (isTSpin)
        {
            switch (lines)
            {
                case 1:
                    baseGarbage = 2;
                    break;
                case 2:
                    baseGarbage = 4;
                    break;
                case 3:
                    baseGarbage = 6;
                    break;
                default:
                    baseGarbage = 0;
                    break;
            }
            isB2BAction = lines > 0;
        }
        else
        {
            switch (lines)
            {
                case 1:
                    baseGarbage = 0;
                    break;
                case 2:
                    baseGarbage = 1;
                    break;
                case 3:
                    baseGarbage = 2;
                    break;
                case 4:
                    baseGarbage = 4;
                    isB2BAction = true;
                    break;
                default:
                    baseGarbage = 0;
                    break;
            }
        }

        int ren = IncreaseRenForBoard(sender);
        int renBonus = GetRenBonus(ren);

        int b2bBonus = 0;
        bool lastWasB2B = GetLastB2BForBoard(sender);
        if (isB2BAction && lastWasB2B)
            b2bBonus = 1;

        SetLastB2BForBoard(sender, isB2BAction);

        int total = baseGarbage + renBonus + b2bBonus;
        Debug.Log($"VersusJudge: sender={sender.name}, lines={lines}, tspin={isTSpin}, ren={ren}, renBonus={renBonus}, b2bBonus={b2bBonus}, totalGarbage={total}");
        return Mathf.Max(0, total);
    }

    private bool IsTSpin(Tetromino piece, int lines)
    {
        if (piece == null) return false;
        if (lines <= 0) return false;
        return piece.typeIndex == 5 && piece.lastMoveWasRotation;
    }

    private int IncreaseRenForBoard(Board sender)
    {
        if (sender == playerBoard)
        {
            playerRen++;
            return playerRen;
        }

        if (sender == cpuBoard)
        {
            cpuRen++;
            return cpuRen;
        }

        return 0;
    }

    private void ResetRenForBoard(Board sender)
    {
        if (sender == playerBoard)
            playerRen = 0;
        else if (sender == cpuBoard)
            cpuRen = 0;
    }

    private bool GetLastB2BForBoard(Board sender)
    {
        if (sender == playerBoard) return playerLastWasB2B;
        if (sender == cpuBoard) return cpuLastWasB2B;
        return false;
    }

    private void SetLastB2BForBoard(Board sender, bool value)
    {
        if (sender == playerBoard)
            playerLastWasB2B = value;
        else if (sender == cpuBoard)
            cpuLastWasB2B = value;
    }

    private void ResetB2BIfNeeded(Board sender, bool value)
    {
        if (sender == playerBoard)
            playerLastWasB2B = value;
        else if (sender == cpuBoard)
            cpuLastWasB2B = value;
    }

    private int GetRenBonus(int ren)
    {
        if (ren <= 1) return 0;
        if (ren == 2) return 1;
        if (ren == 3) return 1;
        if (ren == 4) return 2;
        if (ren == 5) return 2;
        if (ren == 6) return 3;
        return 4;
    }

    private void HandleFinish(bool isWin)
    {
        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null)
            controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        float finishTime = GetFinishTimeSeconds();
        UpdateTexts(isWin, finishTime);

        if (clearFaridUI != null)
        {
            clearFaridUI.gameObject.SetActive(true);
            clearFaridUI.SetImageByIndex(isWin ? 4 : 0);
            clearFaridUI.Play();
        }

        if (clearUIRoot != null)
            clearUIRoot.SetActive(true);

        if (stopTimeOnFinish)
            Time.timeScale = 0f;
    }

    private float GetFinishTimeSeconds()
    {
        if (GameTimer.Instance == null)
            return 0f;

        return GameTimer.Instance.GetClearTime();
    }

    private void UpdateTexts(bool isWin, float finishTime)
    {
        if (timeText != null)
            timeText.text = $"You took {finishTime:F2} seconds";

        if (resultMessageText != null)
            resultMessageText.text = isWin ? winResultMessage : loseResultMessage;

        if (clearMessageText != null)
            clearMessageText.text = isWin ? winClearMessage : loseClearMessage;
    }

    public void OnRetryButton()
    {
        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);

        if (clearFaridUI != null)
            clearFaridUI.gameObject.SetActive(false);

        if (GameTimer.Instance != null)
            GameTimer.Instance.ResetTimer();

        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(retrySceneName))
        {
            SceneManager.LoadScene(retrySceneName);
        }
        else
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }

    public void OnStageSelectButton()
    {
        if (string.IsNullOrEmpty(stageSelectSceneName))
        {
            Debug.LogWarning("VersusJudge: stageSelectSceneName が設定されていません。");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }
}