using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PerfectClearJudge : MonoBehaviour
{
    [System.Serializable]
    private struct SnapshotState
    {
        public List<Board.BlockState> boardBlocks;
        public Spawner.RuntimeState spawnerState;
        public float timerSeconds;
        public int activePieceIndex;
        public bool activePieceSpawnedFromHold;
        public bool hasEverHadBlock;
    }

    public GameObject clearUIRoot;
    public GameObject undoButtonRoot;
    public GameObject newRecordRoot;
    public Text bestTimeText;
    public string stageSelectSceneName = "TechniqueSelect";
    public string nextStageSceneName = "";
    public bool stopTimeOnClear = true;

    public ClearFaridUI clearFaridUI;

    public Text clearMessageText;
    public Text timeText;

    public Board board;

    public bool IsStageCleared { get; private set; } = false;

    private bool hasEverHadBlock = false;
    readonly List<SnapshotState> snapshotHistory = new List<SnapshotState>();
    Spawner spawner;
    bool suppressSnapshotCapture = false;
    int lastSnapshottedPieceInstanceId = -1;
    bool lastClearWasNewRecord = false;

    void Start()
    {
        spawner = FindObjectOfType<Spawner>();
        if (board == null) board = FindObjectOfType<Board>();

        if (board == null)
        {
            return;
        }

        if (clearUIRoot != null)
            clearUIRoot.SetActive(false);
        if (undoButtonRoot != null)
            undoButtonRoot.SetActive(true);
        if (newRecordRoot != null)
            newRecordRoot.SetActive(false);
        RefreshBestTimeUI();

        if (spawner != null)
            spawner.PieceSpawned += OnPieceSpawned;

        StartCoroutine(CaptureInitialSnapshotNextFrame());
    }

    void OnDestroy()
    {
        if (spawner != null)
            spawner.PieceSpawned -= OnPieceSpawned;
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
        lastClearWasNewRecord = SaveManager.RegisterPerfectClearTime(clearTime);

        int spriteIndex = GetSpriteIndexByTime(clearTime);
        UpdateClearTexts(clearTime, spriteIndex);
        UpdateNewRecordUI();
        RefreshBestTimeUI();

        if (clearUIRoot != null)
            clearUIRoot.SetActive(true);

        if (clearFaridUI != null)
        {
            clearFaridUI.SetImageByIndex(spriteIndex);
            clearFaridUI.Play();
        }

        if (stopTimeOnClear) Time.timeScale = 0f;
    }

    void UpdateNewRecordUI()
    {
        if (newRecordRoot != null)
            newRecordRoot.SetActive(lastClearWasNewRecord);
    }

    void RefreshBestTimeUI()
    {
        if (bestTimeText == null)
            return;

        float bestTime = SaveManager.GetBestPerfectClearTimeSeconds();
        bestTimeText.text = $"BEST\n{FormatBestTime(bestTime)}";
    }

    string FormatBestTime(float seconds)
    {
        if (seconds < 0f)
            return "--:--";

        return FormatTime(seconds);
    }

    IEnumerator CaptureInitialSnapshotNextFrame()
    {
        yield return null;

        if (!isActiveAndEnabled || IsStageCleared)
            yield break;

        Tetromino activePiece = FindObjectOfType<Tetromino>();
        if (activePiece != null)
            CaptureSnapshotForPiece(activePiece);
    }

    void OnPieceSpawned(Tetromino piece)
    {
        if (suppressSnapshotCapture) return;
        if (piece == null || IsStageCleared) return;

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
            activePieceIndex = piece.typeIndex,
            activePieceSpawnedFromHold = piece.spawnedFromHold,
            hasEverHadBlock = hasEverHadBlock
        });
        lastSnapshottedPieceInstanceId = pieceInstanceId;
    }

    public void OnUndoButton()
    {
        if (IsStageCleared) return;
        if (snapshotHistory.Count < 2) return;
        if (board == null || spawner == null) return;

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        snapshotHistory.RemoveAt(snapshotHistory.Count - 1);
        SnapshotState snapshot = snapshotHistory[snapshotHistory.Count - 1];
        RestoreSnapshot(snapshot);
    }

    void RestoreSnapshot(SnapshotState snapshot)
    {
        suppressSnapshotCapture = true;

        RemoveActiveGameplayObjects();
        board.ClearBoardImmediate();

        board.RestoreBlockStates(snapshot.boardBlocks, clearExisting: false);
        Tetromino restoredPiece = spawner.RestoreRuntimeStateAndSpawn(
            snapshot.spawnerState,
            snapshot.activePieceIndex,
            snapshot.activePieceSpawnedFromHold);
        lastSnapshottedPieceInstanceId = restoredPiece != null ? restoredPiece.GetInstanceID() : -1;

        hasEverHadBlock = snapshot.hasEverHadBlock;

        if (GameTimer.Instance != null)
            GameTimer.Instance.SetElapsedTime(snapshot.timerSeconds, true);

        suppressSnapshotCapture = false;
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
            timeText.text = "Time: " +FormatTime(clearTime);

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
