using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FortyLineJudge : MonoBehaviour
{
    [System.Serializable]
    private struct DecisionRecord
    {
        public int pieceTypeIndex;
        public int linesCleared;
        public float decisionTimeSeconds;
        public int snapshotIndex;
    }

    [System.Serializable]
    private struct SnapshotState
    {
        public List<Board.BlockState> boardBlocks;
        public Spawner.RuntimeState spawnerState;
        public float timerSeconds;
        public int totalLinesCleared;
        public int totalPiecesLocked;
        public int activePieceIndex;
        public bool activePieceSpawnedFromHold;
    }

    [Header("UI / Scene Settings")]
    public GameObject clearUIRoot;
    public bool stopTimeOnClear = true;

    [Header("Clear Animation")]
    public ClearFaridUI clearFaridUI;

    [Header("Clear Texts")]
    public Text clearMessageText;
    public Text timeText;
    public GameObject newRecordRoot;
    public BestFortyLineTimeText bestTimeText;

    [Header("Line Counter UI")]
    public TMP_Text linesRemainingText;
    public TMP_Text ppsText;
    public GameObject linesRemainingRoot;
    public GameObject mobileUiRoot;
    public GameObject undoButtonRoot;

    [Header("Slow Decision Review")]
    public GameObject reviewPanelRoot;
    public TMP_Text reviewDetailText;
    public TMP_Text reviewPageText;
    public Button nextReviewButton;
    public TMP_Text nextReviewButtonText;

    public bool IsStageCleared { get; private set; } = false;
    public bool IsStageFailed { get; private set; } = false;

    [Header("40 Line Mode Settings")]
    public int targetLines = 40;
    int totalLinesCleared = 0;
    int totalPiecesLocked = 0;

    bool isEasyLikeMode = false;
    bool lastClearWasNewRecord = false;

    private readonly List<SnapshotState> snapshotHistory = new List<SnapshotState>();
    private readonly List<DecisionRecord> decisionHistory = new List<DecisionRecord>();
    private readonly List<DecisionRecord> slowestReviewRecords = new List<DecisionRecord>();
    private Spawner spawner;
    private Board board;
    private bool suppressSnapshotCapture = false;
    private int lastSnapshottedPieceInstanceId = -1;
    private List<Board.BlockState> finalBoardBlocks;
    private int reviewRecordIndex = -1;

    void Start()
    {
        spawner = FindObjectOfType<Spawner>();
        board = spawner != null && spawner.board != null
            ? spawner.board
            : FindObjectOfType<Board>();

        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
        if (newRecordRoot != null)
            newRecordRoot.SetActive(false);
        if (reviewPanelRoot != null)
            reviewPanelRoot.SetActive(false);
        UpdateLinesRemainingText();
        UpdatePpsText();

        if (spawner != null)
            spawner.PieceSpawned += OnPieceSpawned;

        Tetromino activePiece = FindObjectOfType<Tetromino>();
        if (activePiece != null)
            CaptureSnapshotForPiece(activePiece);
    }

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.PieceSpawned -= OnPieceSpawned;
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
        SaveManager.AddLinesCleared(linesCleared);
        RecordDecision(piece, linesCleared);
        Debug.Log($"[40L] Total Lines: {totalLinesCleared}/{targetLines}");
        UpdateLinesRemainingText();
        UpdatePpsText();

        if (totalLinesCleared >= targetLines)
        {
            float clearTime = GetClearTimeSeconds();
            SaveManager.AddRecordedTime(clearTime);
            lastClearWasNewRecord = SaveManager.RegisterFortyLineTime(clearTime);

            SoundManager.Instance?.PlaySE(SeType.StageClear);
            HandleStageClear(clearTime);
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

        CaptureFinalBoardState();
        BuildSlowestReviewRecords();
    }

    void HandleStageClear(float clearTime)
    {
        Debug.Log("HandleStageClear START");

        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null)
            controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        int spriteIndex = GetSpriteIndexByTime(clearTime, isEasyLikeMode);
        UpdatePpsText();
        CaptureFinalBoardState();
        BuildSlowestReviewRecords();
        UpdateNewRecordUI();
        if (bestTimeText != null)
            bestTimeText.Refresh();

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

    void UpdateNewRecordUI()
    {
        if (newRecordRoot != null)
            newRecordRoot.SetActive(lastClearWasNewRecord);
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
        lastClearWasNewRecord = false;
        snapshotHistory.Clear();
        decisionHistory.Clear();
        slowestReviewRecords.Clear();
        finalBoardBlocks = null;
        reviewRecordIndex = -1;
        lastSnapshottedPieceInstanceId = -1;
        if (newRecordRoot != null)
            newRecordRoot.SetActive(false);
        if (reviewPanelRoot != null)
            reviewPanelRoot.SetActive(false);
        UpdateLinesRemainingText();
        UpdatePpsText();

        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void OnUndoButton()
    {
        if (IsStageCleared || IsStageFailed) return;
        if (snapshotHistory.Count < 2) return;
        if (board == null || spawner == null) return;

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        snapshotHistory.RemoveAt(snapshotHistory.Count - 1);
        SnapshotState snapshot = snapshotHistory[snapshotHistory.Count - 1];

        if (decisionHistory.Count > 0)
        {
            DecisionRecord lastDecision = decisionHistory[decisionHistory.Count - 1];
            SaveManager.AddLinesCleared(-lastDecision.linesCleared);
            decisionHistory.RemoveAt(decisionHistory.Count - 1);
        }

        RestoreSnapshot(snapshot);
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

    public void OnOpenSlowestReviewButton()
    {
        if (slowestReviewRecords.Count == 0 || reviewPanelRoot == null)
            return;

        reviewPanelRoot.SetActive(true);
        SetReviewHudState(true);
        ShowSlowestReview(0);
    }

    public void OnCloseSlowestReviewButton()
    {
        if (reviewPanelRoot != null)
            reviewPanelRoot.SetActive(false);

        SetReviewHudState(false);
        RestoreFinalBoardStateForReview();
    }

    public void OnNextSlowestReviewButton()
    {
        if (slowestReviewRecords.Count == 0)
            return;

        if (reviewRecordIndex >= slowestReviewRecords.Count - 1)
        {
            OnCloseSlowestReviewButton();
            return;
        }

        ShowSlowestReview(Mathf.Min(reviewRecordIndex + 1, slowestReviewRecords.Count - 1));
    }

    public void OnPreviousSlowestReviewButton()
    {
        if (slowestReviewRecords.Count == 0)
            return;

        ShowSlowestReview(Mathf.Max(reviewRecordIndex - 1, 0));
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

    void OnPieceSpawned(Tetromino piece)
    {
        if (suppressSnapshotCapture) return;
        if (piece == null || IsStageCleared || IsStageFailed) return;

        CaptureSnapshotForPiece(piece);
    }

    void CaptureSnapshotForPiece(Tetromino piece)
    {
        if (piece == null || spawner == null)
            return;

        if (piece.board != null)
            board = piece.board;

        if (board == null)
            return;

        int pieceInstanceId = piece.GetInstanceID();
        if (pieceInstanceId == lastSnapshottedPieceInstanceId)
            return;

        snapshotHistory.Add(new SnapshotState
        {
            boardBlocks = board.CaptureBlockStates(),
            spawnerState = spawner.CaptureRuntimeState(),
            timerSeconds = GetClearTimeSeconds(),
            totalLinesCleared = totalLinesCleared,
            totalPiecesLocked = totalPiecesLocked,
            activePieceIndex = piece.typeIndex,
            activePieceSpawnedFromHold = piece.spawnedFromHold
        });
        lastSnapshottedPieceInstanceId = pieceInstanceId;
    }

    void RecordDecision(Tetromino piece, int linesCleared)
    {
        if (snapshotHistory.Count == 0 || piece == null)
            return;

        SnapshotState currentSnapshot = snapshotHistory[snapshotHistory.Count - 1];
        float decisionTime = Mathf.Max(0f, GetClearTimeSeconds() - currentSnapshot.timerSeconds);

        decisionHistory.Add(new DecisionRecord
        {
            pieceTypeIndex = piece.typeIndex,
            linesCleared = linesCleared,
            decisionTimeSeconds = decisionTime,
            snapshotIndex = snapshotHistory.Count - 1
        });
    }

    void BuildSlowestReviewRecords()
    {
        slowestReviewRecords.Clear();

        if (decisionHistory.Count == 0)
            return;

        List<DecisionRecord> sorted = new List<DecisionRecord>(decisionHistory);
        sorted.Sort((a, b) => b.decisionTimeSeconds.CompareTo(a.decisionTimeSeconds));

        int count = Mathf.Min(10, sorted.Count);
        for (int i = 0; i < count; i++)
            slowestReviewRecords.Add(sorted[i]);
    }

    void CaptureFinalBoardState()
    {
        if (board == null)
            return;

        finalBoardBlocks = board.CaptureBlockStates();
    }

    void ShowSlowestReview(int index)
    {
        if (index < 0 || index >= slowestReviewRecords.Count)
            return;

        reviewRecordIndex = index;
        DecisionRecord record = slowestReviewRecords[index];
        if (record.snapshotIndex < 0 || record.snapshotIndex >= snapshotHistory.Count)
            return;

        RestoreSnapshotForReview(snapshotHistory[record.snapshotIndex]);
        UpdateReviewTexts(record, index, slowestReviewRecords.Count);
    }

    void RestoreSnapshotForReview(SnapshotState snapshot)
    {
        suppressSnapshotCapture = true;

        RemoveActiveGameplayObjects();
        board.ClearBoardImmediate();
        board.RestoreBlockStates(snapshot.boardBlocks, clearExisting: false);

        Tetromino restoredPiece = spawner.RestoreRuntimeStateAndSpawn(snapshot.spawnerState, snapshot.activePieceIndex, snapshot.activePieceSpawnedFromHold);
        lastSnapshottedPieceInstanceId = restoredPiece != null ? restoredPiece.GetInstanceID() : -1;

        if (restoredPiece != null)
        {
            restoredPiece.enablePlayerInput = false;
            restoredPiece.enabled = false;

            if (restoredPiece.ghost != null)
            {
                restoredPiece.ghost.gameObject.SetActive(false);
                Destroy(restoredPiece.ghost.gameObject);
                restoredPiece.ghost = null;
            }
        }

        suppressSnapshotCapture = false;
    }

    void RestoreFinalBoardStateForReview()
    {
        if (board == null)
            return;

        suppressSnapshotCapture = true;

        RemoveActiveGameplayObjects();
        board.ClearBoardImmediate();
        board.RestoreBlockStates(finalBoardBlocks, clearExisting: false);

        suppressSnapshotCapture = false;
    }

    void UpdateReviewTexts(DecisionRecord record, int index, int total)
    {
        if (reviewPageText != null)
            reviewPageText.text = $"{index + 1}/{total}";

        UpdateNextReviewButtonState(index, total);

        if (reviewDetailText != null)
        {
            reviewDetailText.text =
                $"Decision Time:\n{FormatDecisionTime(record.decisionTimeSeconds)}\n";
        }
    }

    string FormatDecisionTime(float secondsValue)
    {
        int minutes = Mathf.FloorToInt(secondsValue / 60f);
        float seconds = secondsValue % 60f;
        return $"{minutes}:{seconds:00.00}";
    }

    void UpdateNextReviewButtonState(int index, int total)
    {
        bool isLastPage = total > 0 && index >= total - 1;

        if (nextReviewButtonText != null)
            nextReviewButtonText.text = isLastPage ? "Return" : "Next";

        if (nextReviewButton != null)
            nextReviewButton.interactable = total > 0;
    }

    void SetReviewHudState(bool inReview)
    {
        if (linesRemainingRoot != null)
            linesRemainingRoot.SetActive(!inReview);

        if (mobileUiRoot != null && Application.isMobilePlatform)
            mobileUiRoot.SetActive(!inReview);

        if (undoButtonRoot != null)
            undoButtonRoot.SetActive(!inReview);

        if (clearUIRoot != null)
            clearUIRoot.SetActive(!inReview);

        if (clearFaridUI != null)
            clearFaridUI.gameObject.SetActive(!inReview);

        if (reviewPanelRoot != null && inReview)
            reviewPanelRoot.SetActive(true);
    }

    void RemoveActiveGameplayObjects()
    {
        Tetromino[] activePieces = FindObjectsOfType<Tetromino>();
        for (int i = 0; i < activePieces.Length; i++)
        {
            Tetromino activePiece = activePieces[i];
            if (activePiece == null)
                continue;

            if (activePiece.ghost != null)
            {
                activePiece.ghost.gameObject.SetActive(false);
                Destroy(activePiece.ghost.gameObject);
            }

            activePiece.gameObject.SetActive(false);
            Destroy(activePiece.gameObject);
        }

        GhostPiece[] ghosts = FindObjectsOfType<GhostPiece>();
        for (int i = 0; i < ghosts.Length; i++)
        {
            GhostPiece ghost = ghosts[i];
            if (ghost == null)
                continue;

            ghost.gameObject.SetActive(false);
            Destroy(ghost.gameObject);
        }
    }

    private void RestoreSnapshot(SnapshotState snapshot)
    {
        suppressSnapshotCapture = true;

        RemoveActiveGameplayObjects();
        board.ClearBoardImmediate();

        board.RestoreBlockStates(snapshot.boardBlocks, clearExisting: false);
        Tetromino restoredPiece = spawner.RestoreRuntimeStateAndSpawn(snapshot.spawnerState, snapshot.activePieceIndex, snapshot.activePieceSpawnedFromHold);
        lastSnapshottedPieceInstanceId = restoredPiece != null ? restoredPiece.GetInstanceID() : -1;

        totalLinesCleared = snapshot.totalLinesCleared;
        totalPiecesLocked = snapshot.totalPiecesLocked;

        if (GameTimer.Instance != null)
            GameTimer.Instance.SetElapsedTime(snapshot.timerSeconds, true);

        UpdateLinesRemainingText();
        UpdatePpsText();

        suppressSnapshotCapture = false;
    }

    string GetPieceLetter(int typeIndex)
    {
        switch (typeIndex)
        {
            case 0: return "I";
            case 1: return "J";
            case 2: return "L";
            case 3: return "O";
            case 4: return "S";
            case 5: return "T";
            case 6: return "Z";
            default: return "?";
        }
    }
}
