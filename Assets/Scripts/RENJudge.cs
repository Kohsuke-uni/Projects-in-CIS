using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RENJudge : MonoBehaviour
{
    [System.Serializable]
    private struct SnapshotState
    {
        public List<Board.BlockState> boardBlocks;
        public Spawner.RuntimeState spawnerState;
        public float timerSeconds;
        public int currentRen;
        public int maxRen;
        public int activePieceIndex;
        public bool activePieceSpawnedFromHold;
    }

    [Header("UI / Scene Settings")]
    public GameObject clearUIRoot;
    public GameObject clearButtonsRoot;
    public GameObject newRecordRoot;
    public string stageSelectSceneName = "REN_StageSelect";
    public string nextStageSceneName = "";
    public bool stopTimeOnClear = true;

    [Header("Clear Animation")]
    public ClearFaridUI clearFaridUI;

    [Header("UI (In-Game REN)")]
    public Text renNowText;
    public TMP_Text bestRenText;
    public GameObject undoButtonRoot;

    [Header("UI (Result)")]
    public Text renCountText;
    public Text clearMessageText;
    public Text timeText;

    [Header("Requirements")]
    public int normalRequiredRen = 3;
    public int hardRequiredRen = 5;

    public bool IsStageCleared { get; private set; } = false;

    int currentRen = 0;
    int maxRen = 0;
    bool lastRunWasNewRecord = false;
    readonly List<SnapshotState> snapshotHistory = new List<SnapshotState>();
    Spawner spawner;
    Board board;
    bool suppressSnapshotCapture = false;
    int lastSnapshottedPieceInstanceId = -1;


    public enum RENMode
    {
        Easy,
        Normal,
        Hard
    }
    public RENMode renMode = RENMode.Easy;

    // Legacy mode detection flags
    // bool isEasyMode = false;
    // bool isNormalMode = false;
    // bool isHardMode = false;

    void OnEnable()
    {
        SetBestRenTextVisible(true);
        SetUndoButtonVisible(true);
        RefreshBestRenText();
    }

    void OnDisable()
    {
        SetBestRenTextVisible(false);
        SetUndoButtonVisible(false);
    }

    void Start()
    {
        spawner = FindObjectOfType<Spawner>();
        board = spawner != null && spawner.board != null
            ? spawner.board
            : FindObjectOfType<Board>();

        // Legacy support for mode detection
        // string sceneName = SceneManager.GetActiveScene().name;
        // isEasyMode   = sceneName.Contains("REN_E");
        // isNormalMode = sceneName.Contains("REN_N");
        // isHardMode   = sceneName.Contains("REN_H");

        if (clearUIRoot != null) clearUIRoot.SetActive(false);
        if (newRecordRoot != null) newRecordRoot.SetActive(false);
        if (renNowText != null) renNowText.text = "";
        RefreshBestRenText();

        if (spawner != null)
            spawner.PieceSpawned += OnPieceSpawned;

        StartCoroutine(CaptureInitialSnapshotNextFrame());
    }

    void OnDestroy()
    {
        if (spawner != null)
            spawner.PieceSpawned -= OnPieceSpawned;
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

    public void OnPieceLocked(Tetromino piece, int linesCleared)
    {
        if (IsStageCleared) return;

        if (linesCleared > 0)
        {
            currentRen++;
            int displayedRen = Mathf.Max(0, currentRen - 1);
            if (displayedRen > maxRen)
                maxRen = displayedRen;

            if (SaveManager.RegisterBestRen(displayedRen))
            {
                lastRunWasNewRecord = true;
                RefreshBestRenText();
            }

            if (renNowText != null) renNowText.text = displayedRen > 0 ? $"{displayedRen} REN" : "";
            return;
        }

        if (renNowText != null) renNowText.text = "";

        bool cleared = false;

        if (renMode == RENMode.Easy) cleared = true;
        else if (renMode == RENMode.Normal) cleared = (maxRen >= normalRequiredRen);
        else if (renMode == RENMode.Hard) cleared = (maxRen >= hardRequiredRen);

        currentRen = 0;

        if (cleared) HandleStageClear();
    }

    void HandleStageClear()
    {
        Debug.Log("Stage cleared.");
        IsStageCleared = true;

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null) controlUI.HideAllUI();

        if (GameTimer.Instance != null)
            GameTimer.Instance.StopTimer();

        float clearTime = GetClearTimeSeconds();
        SaveManager.AddRecordedTime(clearTime);
        RefreshBestRenText();
        UpdateNewRecordUI();

        UpdateClearMessage(clearTime);

        int spriteIndex = GetSpriteIndexByRen(maxRen);

        if (clearFaridUI != null)
        {
            clearFaridUI.SetImageByIndex(spriteIndex);
            clearFaridUI.Play();
        }

        if (clearUIRoot != null) clearUIRoot.SetActive(true);
        if (clearButtonsRoot != null) clearButtonsRoot.SetActive(true);

        if (stopTimeOnClear) Time.timeScale = 0f;
    }

    int GetSpriteIndexByRen(int ren)
    {
        if (renMode == RENMode.Easy)
        {
            if (ren == 0) return 0;
            if (ren <= 3) return 1;
            if (ren <= 5) return 2;
            if (ren <= 10) return 3;
            return 4;
        }
        else
        {
            if (ren == 0) return 0;
            if (ren == 1) return 1;
            if (ren == 2) return 2;
            if (ren <= 4) return 3;
            return 4;
        }
    }

    string GetRenComment(int ren)
    {
        if (ren == 0) return "You can do better than that! Try again!";
        if (ren <= 3) return "Kind of disappointing tbh...";
        if (ren <= 5) return "Not bad, but could be better...";
        if (ren <= 10) return "Wow, you're pretty good at REN";
        return "You are a God Tier Tetris Player";
    }

    void UpdateClearMessage(float clearTime)
    {
        if (renCountText != null) renCountText.text = $"You did {maxRen} REN";

        if (clearMessageText != null)
        {
            if (renMode == RENMode.Normal || renMode == RENMode.Hard)
                clearMessageText.text = "You are now pro at REN!";
            else
                clearMessageText.text = GetRenComment(maxRen);
        }

        if (timeText != null) timeText.text = "";
    }

    float GetClearTimeSeconds()
    {
        if (GameTimer.Instance == null)
            return 0f;

        return GameTimer.Instance.GetClearTime();
    }

    void RefreshBestRenText()
    {
        if (bestRenText == null)
            return;

        int bestRen = SaveManager.GetBestRen();
        bestRenText.text = bestRen >= 0 ? $"Best REN: {bestRen}" : "Best REN: 0";
    }

    void SetBestRenTextVisible(bool isVisible)
    {
        if (bestRenText == null)
            return;

        bestRenText.gameObject.SetActive(isVisible);
    }

    void SetUndoButtonVisible(bool isVisible)
    {
        if (undoButtonRoot == null)
            return;

        undoButtonRoot.SetActive(isVisible);
    }

    void UpdateNewRecordUI()
    {
        if (newRecordRoot == null)
            return;

        newRecordRoot.SetActive(lastRunWasNewRecord);
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

    public void OnRetryButton()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnNextStageButton()
    {
        ExerciseSceneLoader loader = FindObjectOfType<ExerciseSceneLoader>();
        if (loader != null && loader.TryAdvanceRenStage())
        {
            Time.timeScale = 1f;
            return;
        }

        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }

    public void OnStageSelectButton()
    {
        if (string.IsNullOrEmpty(stageSelectSceneName)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
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
            currentRen = currentRen,
            maxRen = maxRen,
            activePieceIndex = piece.typeIndex,
            activePieceSpawnedFromHold = piece.spawnedFromHold
        });
        lastSnapshottedPieceInstanceId = pieceInstanceId;
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

        currentRen = snapshot.currentRen;
        maxRen = snapshot.maxRen;

        if (GameTimer.Instance != null)
            GameTimer.Instance.SetElapsedTime(snapshot.timerSeconds, true);

        if (renNowText != null)
        {
            int displayedRen = Mathf.Max(0, currentRen - 1);
            renNowText.text = displayedRen > 0 ? $"{displayedRen} REN" : "";
        }

        suppressSnapshotCapture = false;
    }
}
