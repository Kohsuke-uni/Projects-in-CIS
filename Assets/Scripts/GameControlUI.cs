using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameControlUI : MonoBehaviour
{
    // 他のスクリプトから参照する用のフラグ
    public static bool IsPaused { get; private set; } = false;

    [Header("Optional")]
    [Tooltip("ポーズ中にうっすら表示するオーバーレイ（なければ空でOK）")]
    public GameObject pauseOverlay;

    [Header("Mobile UI")]
    [Tooltip("モバイル操作ボタンをまとめた親オブジェクト")]
    public GameObject mobileControlsRoot;

    [Header("Pause / Play Button")]
    [Tooltip("右側の一時停止ボタンの Image コンポーネント")]
    public Image pauseButtonImage;   // Pause_Button の Image

    [Tooltip("通常再生中に表示する『一時停止』アイコン")]
    public Sprite pauseSprite;       // 「||」 のアイコン

    [Tooltip("一時停止中に表示する『再生』アイコン")]
    public Sprite playSprite;        // 「▶」 のアイコン

    // 内部用フラグ（ボタン表示の切り替えなどに使用）
    private bool isPaused = false;

    private void Start()
    {
        UpdateMobileControlsVisibility();

        // シーン開始時は必ず再生状態に戻す
        SetPause(false);
    }

    // 🔁 左のリスタートボタン用
    public void OnRestartButton()
    {
        // 再スタート前に必ずポーズ解除
        SetPause(false);

        var current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    // ⏸ / ▶ 右のボタン用（同じボタンでトグル）
    public void OnPauseButton()
    {
        SetPause(!isPaused);
    }

    public void OnRotateCWButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputRotateCW();
    }

    public void OnRotateCCWButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputRotateCCW();
    }

    public void OnMoveLeftButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputMoveLeft();
    }

    public void OnMoveRightButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputMoveRight();
    }

    public void OnStopHorizontalButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputStopHorizontal();
    }

    public void OnSoftDropStartButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputSoftDropStart();
    }

    public void OnSoftDropEndButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputSoftDropEnd();
    }

    public void OnHardDropButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputHardDrop();
    }

    public void OnHoldButton()
    {
        Tetromino activePiece = GetActiveTetromino();
        if (activePiece != null)
            activePiece.InputHold();
    }

    /// <summary>
    /// ポーズ状態をまとめて切り替える中核メソッド
    /// </summary>
    private void SetPause(bool pause)
    {
        isPaused = pause;
        IsPaused = pause;                         // 外部参照用フラグ

        // 時間を止める / 再開
        Time.timeScale = pause ? 0f : 1f;

        // オーバーレイ（あれば）をON/OFF
        if (pauseOverlay != null)
            pauseOverlay.SetActive(pause);

        // ボタンアイコンの見た目を更新
        UpdatePauseButtonVisual();

        // 🔴 ミノ系スクリプトを強制的にON/OFFする
        ApplyPauseToPieces();
    }

    /// <summary>
    /// 現在シーン内に存在する Tetromino / GhostPiece をまとめて有効/無効にする
    /// </summary>
    private void ApplyPauseToPieces()
    {
        // Tetromino（ミノの本体操作）を停止/再開
        var tetrominoes = FindObjectsOfType<Tetromino>();
        foreach (var t in tetrominoes)
        {
            // クリア後などで null の可能性もあるのでチェック
            if (t != null)
                t.enabled = !isPaused;
        }

        // GhostPiece（ゴースト表示）も一緒に止めておく（任意だが安全）
        var ghosts = FindObjectsOfType<GhostPiece>();
        foreach (var g in ghosts)
        {
            if (g != null)
                g.enabled = !isPaused;
        }
    }

    // ボタンのアイコンを、再生中/停止中で切り替える
    private void UpdatePauseButtonVisual()
    {
        if (pauseButtonImage == null) return;

        // 再生中 → 「||」、一時停止中 → 「▶」
        pauseButtonImage.sprite = isPaused ? playSprite : pauseSprite;
    }

    private Tetromino GetActiveTetromino()
    {
        var tetrominoes = FindObjectsOfType<Tetromino>();
        for (int i = 0; i < tetrominoes.Length; i++)
        {
            Tetromino tetromino = tetrominoes[i];
            if (tetromino == null || !tetromino.enabled || !tetromino.enablePlayerInput)
                continue;

            return tetromino;
        }

        return null;
    }

    private void UpdateMobileControlsVisibility()
    {
        if (mobileControlsRoot == null) return;

        mobileControlsRoot.SetActive(Application.isMobilePlatform);
    }

    // クリアUIから呼ばれて、右側の操作UIを全部消したいとき用
    public void HideAllUI()
    {
        gameObject.SetActive(false);
    }
}
