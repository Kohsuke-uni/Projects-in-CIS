using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Make Shape Mode のジャッジコンポーネント。
///
/// ルール:
///  - 指定ミノのみを出す（デフォルト: Z ミノ）
///  - 自動落下なし・ハードドロップ時のみ確定（シーン名に "MakeShape" を含めることで Tetromino.cs が自動設定）
///  - 時間制限なし
///  - シェイプ外にブロックが置かれたら強制リトライ（最後のミノ＝クリア時はOK）
///  - 全ターゲットセルが埋まったらクリア
/// </summary>
public class MakeShapeJudge : MonoBehaviour
{
    // ===========================================================
    //  形の種類
    // ===========================================================
    public enum ShapeType
    {
        Heart,
        Diamond,
        Arrow,
        Custom
    }

    // ===========================================================
    //  Inspector フィールド
    // ===========================================================

    [Header("Shape Settings")]
    [Tooltip("使用する形の種類")]
    public ShapeType shapeType = ShapeType.Heart;

    [Tooltip("Custom 選択時のターゲットセル一覧（グリッド座標 x,y）")]
    public Vector2Int[] customTargetCells;

    [Tooltip("全形状に適用するグリッド座標オフセット（形を移動したいとき）")]
    public Vector2Int shapeOffset = Vector2Int.zero;

    [Header("Spawn Settings")]
    [Tooltip("常に出すミノの index。I=0 J=1 L=2 O=3 S=4 T=5 Z=6")]
    public int spawnOnlyPieceIndex = 6; // Z ミノ

    [Header("Target Overlay Visual")]
    [Tooltip("ターゲットセルに表示するプレハブ（SpriteRenderer 付き）。未設定時は白い四角で代替")]
    public GameObject targetCellPrefab;

    [Tooltip("未充填セルのオーバーレイ色（ステップ色が無効なときに使用）")]
    public Color pendingColor = new Color(1f, 0.4f, 0.4f, 0.35f);

    [Tooltip("充填済みセルのオーバーレイ色")]
    public Color filledColor = new Color(0.3f, 1f, 0.3f, 0.25f);

    [Header("Step Hint")]
    [Tooltip("ステップ番号ごとに色を変えてミノの置き順を示す（Heart 形のみ）")]
    public bool showStepHint = true;

    [Tooltip("ステップ 1〜6 のヒント色（順番通り）")]
    public Color[] stepHintColors = new Color[]
    {
        new Color(1.00f, 0.25f, 0.25f, 0.60f), // 1: 赤
        new Color(1.00f, 0.60f, 0.10f, 0.60f), // 2: オレンジ
        new Color(1.00f, 1.00f, 0.10f, 0.60f), // 3: 黄
        new Color(0.10f, 0.85f, 0.20f, 0.60f), // 4: 緑
        new Color(0.10f, 0.80f, 1.00f, 0.60f), // 5: シアン
        new Color(0.70f, 0.20f, 1.00f, 0.60f), // 6: 紫
    };

    [Header("UI / Scene Settings")]
    public GameObject clearUIRoot;
    public GameObject newRecordRoot;
    public Text bestTimeText;
    public string stageSelectSceneName = "TechniqueSelect";
    public string nextStageSceneName = "";
    public bool stopTimeOnClear = true;

    [Header("Clear Animation")]
    public ClearFaridUI clearFaridUI;

    [Header("Clear Texts")]
    public Text clearMessageText;
    public Text timeText;

    [Header("References")]
    public Board board;

    // ===========================================================
    //  公開プロパティ
    // ===========================================================
    public bool IsStageCleared { get; private set; } = false;

    // ===========================================================
    //  内部状態
    // ===========================================================
    Vector2Int[] targetCells;
    HashSet<Vector2Int> targetCellSet; // 高速な所属チェック用
    readonly List<GameObject> overlayObjects = new List<GameObject>();
    int[] cellStepIndex; // overlayObjects と同順で、何ステップ目のセルか（-1=未割当）
    bool lastClearWasNewRecord = false;

    // ===========================================================
    //  Unity ライフサイクル
    // ===========================================================

    void Awake()
    {
        // Spawner.Start() より先に forcedPieceIndex を設定することで
        // 初回スポーン時から Z ミノが出るようにする
        var spawner = FindObjectOfType<Spawner>();
        if (spawner != null)
            spawner.forcedPieceIndex = spawnOnlyPieceIndex;
    }

    void Start()
    {
        if (board == null) board = FindObjectOfType<Board>();
        if (board == null)
        {
            Debug.LogError("[MakeShapeJudge] Board が見つかりません。Inspector で設定してください。");
            return;
        }

        // ライン消去を無効化（シェイプのブロックが消えないように）
        board.disableLineClear = true;

        // ターゲット形状を解決
        targetCells = ResolveTargetCells();
        targetCellSet = new HashSet<Vector2Int>(targetCells);

        // オーバーレイを表示
        SpawnTargetOverlay();

        // ステップヒント色を適用
        if (showStepHint && shapeType == ShapeType.Heart)
        {
            cellStepIndex = BuildStepIndex();
            ApplyStepColors();
        }

        if (clearUIRoot != null) clearUIRoot.SetActive(false);
        if (newRecordRoot != null) newRecordRoot.SetActive(false);
        RefreshBestTimeUI();
    }

    // ===========================================================
    //  外部 API（Tetromino.Lock() から呼ばれる）
    // ===========================================================

    public void OnPieceLocked(Tetromino piece, int linesCleared)
    {
        if (IsStageCleared) return;
        if (targetCells == null || targetCells.Length == 0) return;

        UpdateOverlayColors();

        // 1. クリア判定を先にチェック（最後のミノははみ出てもOK）
        if (IsShapeComplete())
        {
            SoundManager.Instance?.PlaySE(SeType.StageClear);
            HandleStageClear();
            return;
        }

        // 2. はみ出し判定（シェイプ外にブロックがあれば強制リトライ）
        if (HasBlocksOutsideShape())
        {
            SoundManager.Instance?.PlaySE(SeType.StageFail);
            ForceRestartScene();
        }
    }

    // ===========================================================
    //  ボタンコールバック
    // ===========================================================

    public void OnRetryButton()
    {
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnNextStageButton()
    {
        string target = ResolveNextSceneName();
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[MakeShapeJudge] 次ステージ名を解決できませんでした。");
            return;
        }
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(target);
    }

    public void OnStageSelectButton()
    {
        if (string.IsNullOrEmpty(stageSelectSceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        Time.timeScale = 1f;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    // ===========================================================
    //  クリア・リトライ処理
    // ===========================================================

    void HandleStageClear()
    {
        IsStageCleared = true;
        RemoveOverlay();

        var controlUI = FindObjectOfType<GameControlUI>();
        if (controlUI != null) controlUI.HideAllUI();

        if (GameTimer.Instance != null) GameTimer.Instance.StopTimer();

        float clearTime = GetClearTimeSeconds();
        SaveManager.AddRecordedTime(clearTime);
        lastClearWasNewRecord = shapeType == ShapeType.Heart &&
            SaveManager.RegisterMakeShapeHeartTime(clearTime);

        int spriteIndex = GetSpriteIndexByTime(clearTime);
        UpdateClearTexts(clearTime, spriteIndex);
        UpdateNewRecordUI();
        RefreshBestTimeUI();

        if (clearFaridUI != null)
        {
            clearFaridUI.SetImageByIndex(spriteIndex);
            clearFaridUI.Play();
        }
        else if (clearUIRoot != null)
        {
            clearUIRoot.SetActive(true);
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

        if (shapeType != ShapeType.Heart)
        {
            bestTimeText.text = "BEST\n--:--";
            return;
        }

        float bestTime = SaveManager.GetBestMakeShapeHeartTimeSeconds();
        bestTimeText.text = $"BEST\n{FormatBestTime(bestTime)}";
    }

    string FormatBestTime(float seconds)
    {
        if (seconds < 0f)
            return "--:--";

        return FormatTime(seconds);
    }

    void ForceRestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ===========================================================
    //  判定ロジック
    // ===========================================================

    /// <summary>全ターゲットセルが埋まっているか</summary>
    bool IsShapeComplete()
    {
        foreach (var cell in targetCells)
        {
            if (!board.IsOccupiedInBounds(cell.x, cell.y))
                return false;
        }
        return true;
    }

    /// <summary>ターゲット外のセルにブロックが置かれているか</summary>
    bool HasBlocksOutsideShape()
    {
        for (int y = 0; y < board.size.y; y++)
        {
            for (int x = 0; x < board.size.x; x++)
            {
                if (board.IsOccupiedInBounds(x, y) &&
                    !targetCellSet.Contains(new Vector2Int(x, y)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ===========================================================
    //  オーバーレイ
    // ===========================================================

    void SpawnTargetOverlay()
    {
        if (targetCells == null) return;

        Sprite defaultSprite = null;

        foreach (var cell in targetCells)
        {
            Vector3 worldPos = board.GridToWorld(cell);
            worldPos.z = 0f;

            GameObject obj;
            if (targetCellPrefab != null)
            {
                obj = Instantiate(targetCellPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                obj = new GameObject("TargetCell");
                obj.transform.position = worldPos;
                var sr = obj.AddComponent<SpriteRenderer>();
                if (defaultSprite == null) defaultSprite = CreateDefaultSprite();
                sr.sprite = defaultSprite;
                sr.sortingOrder = 10; // 背景より手前に表示
            }

            var renderer = obj.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = pendingColor;

            overlayObjects.Add(obj);
        }
    }

    void UpdateOverlayColors()
    {
        for (int i = 0; i < overlayObjects.Count; i++)
        {
            var obj = overlayObjects[i];
            if (obj == null) continue;
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            bool occupied = board.IsOccupiedInBounds(targetCells[i].x, targetCells[i].y);
            if (occupied)
            {
                sr.color = filledColor;
            }
            else
            {
                sr.color = GetPendingColor(i);
            }
        }
    }

    /// <summary>未充填セルの色を返す（ステップヒントが有効ならステップ色、そうでなければ pendingColor）</summary>
    Color GetPendingColor(int overlayIndex)
    {
        if (showStepHint && cellStepIndex != null)
        {
            int step = cellStepIndex[overlayIndex];
            if (step >= 0 && step < stepHintColors.Length)
                return stepHintColors[step];
        }
        return pendingColor;
    }

    void RemoveOverlay()
    {
        foreach (var obj in overlayObjects)
            if (obj != null) Destroy(obj);
        overlayObjects.Clear();
    }

    // ===========================================================
    //  ステップヒント
    // ===========================================================

    /// <summary>
    /// targetCells の各セルが何ステップ目かを解決して返す
    /// </summary>
    int[] BuildStepIndex()
    {
        var steps = GetHeartStepCells();
        int[] index = new int[targetCells.Length];
        for (int i = 0; i < index.Length; i++) index[i] = -1;

        for (int s = 0; s < steps.Length; s++)
        {
            foreach (var sc in steps[s])
            {
                Vector2Int cell = sc + shapeOffset;
                for (int i = 0; i < targetCells.Length; i++)
                {
                    if (targetCells[i] == cell) { index[i] = s; break; }
                }
            }
        }
        return index;
    }

    /// <summary>初回オーバーレイにステップ色を適用する</summary>
    void ApplyStepColors()
    {
        if (cellStepIndex == null) return;
        for (int i = 0; i < overlayObjects.Count; i++)
        {
            if (overlayObjects[i] == null) continue;
            var sr = overlayObjects[i].GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            sr.color = GetPendingColor(i);
        }
    }

    /// <summary>
    /// ハート形のステップ定義（Z ミノ 6 個の置き順）
    ///  col: 0 1 2 3 4 5 6 7 8 9
    ///  y=4: . . 3 3 . . 6 6 . .
    ///  y=3: . 2 2 3 3 5 5 6 6 .
    ///  y=2: . . 2 2 4 4 5 5 . .
    ///  y=1: . . . 1 1 4 4 . . .
    ///  y=0: . . . . 1 1 . . . .
    /// </summary>
    Vector2Int[][] GetHeartStepCells()
    {
        return new Vector2Int[][]
        {
            new[] { new Vector2Int(3,1), new Vector2Int(4,1), new Vector2Int(4,0), new Vector2Int(5,0) }, // 1
            new[] { new Vector2Int(1,3), new Vector2Int(2,3), new Vector2Int(2,2), new Vector2Int(3,2) }, // 2
            new[] { new Vector2Int(2,4), new Vector2Int(3,4), new Vector2Int(3,3), new Vector2Int(4,3) }, // 3
            new[] { new Vector2Int(4,2), new Vector2Int(5,2), new Vector2Int(5,1), new Vector2Int(6,1) }, // 4
            new[] { new Vector2Int(5,3), new Vector2Int(6,3), new Vector2Int(6,2), new Vector2Int(7,2) }, // 5
            new[] { new Vector2Int(6,4), new Vector2Int(7,4), new Vector2Int(7,3), new Vector2Int(8,3) }, // 6
        };
    }

    Sprite CreateDefaultSprite()
    {
        var tex = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    // ===========================================================
    //  ユーティリティ
    // ===========================================================

    float GetClearTimeSeconds()
    {
        if (GameTimer.Instance == null) return 0f;
        return GameTimer.Instance.GetClearTime();
    }

    int GetSpriteIndexByTime(float seconds)
    {
        if (seconds >= 180f) return 0;
        if (seconds > 120f) return 1;
        if (seconds > 60f)  return 2;
        if (seconds > 30f)  return 3;
        return 4;
    }

    void UpdateClearTexts(float clearTime, int spriteIndex)
    {
        if (timeText != null)
            timeText.text = "Time: " +FormatTime(clearTime);

        if (clearMessageText != null)
            clearMessageText.text = GetClearComment(spriteIndex);
    }

    /// <summary>秒数を m:ss.cc 形式にフォーマットする（タイマー表示と統一）</summary>
    string FormatTime(float seconds)
    {
        int m  = (int)(seconds / 60f);
        int s  = (int)(seconds % 60f);
        int cs = (int)((seconds - Mathf.Floor(seconds)) * 100f);
        return $"{m}:{s:00}.{cs:00}";
    }

    string GetClearComment(int index)
    {
        switch (index)
        {
            case 0:  return "You made it! (barely...)";
            case 1:  return "You made it! Keep practicing!";
            case 2:  return "You made it! Not bad!";
            case 3:  return "You made it! Great job!";
            default: return "You made it! Perfect!";
        }
    }

    string ResolveNextSceneName()
    {
        if (!string.IsNullOrEmpty(nextStageSceneName)
            && Application.CanStreamedLevelBeLoaded(nextStageSceneName))
            return nextStageSceneName;

        string current = SceneManager.GetActiveScene().name;
        if (TryBuildNextSceneName(current, out string autoNext)
            && Application.CanStreamedLevelBeLoaded(autoNext))
            return autoNext;

        return string.Empty;
    }

    bool TryBuildNextSceneName(string sceneName, out string nextSceneName)
    {
        nextSceneName = string.Empty;
        if (string.IsNullOrEmpty(sceneName)) return false;

        int end = sceneName.Length - 1;
        int start = end;
        while (start >= 0 && char.IsDigit(sceneName[start])) start--;
        start++;
        if (start > end) return false;

        string numberPart = sceneName.Substring(start, end - start + 1);
        if (!int.TryParse(numberPart, out int cur)) return false;

        nextSceneName = sceneName.Substring(0, start) + (cur + 1);
        return true;
    }

    // ===========================================================
    //  形の定義
    // ===========================================================

    Vector2Int[] ResolveTargetCells()
    {
        Vector2Int[] raw;
        switch (shapeType)
        {
            case ShapeType.Heart:   raw = GetHeartCells();   break;
            case ShapeType.Diamond: raw = GetDiamondCells(); break;
            case ShapeType.Arrow:   raw = GetArrowCells();   break;
            case ShapeType.Custom:  raw = customTargetCells ?? new Vector2Int[0]; break;
            default:                raw = GetHeartCells();   break;
        }

        if (shapeOffset != Vector2Int.zero)
        {
            for (int i = 0; i < raw.Length; i++)
                raw[i] += shapeOffset;
        }

        return raw;
    }

    /// <summary>
    /// ハート形（24セル）
    ///  col: 0 1 2 3 4 5 6 7 8 9
    ///  y=4: . . # # . . # # . .
    ///  y=3: . # # # # # # # # .
    ///  y=2: . . # # # # # # . .
    ///  y=1: . . . # # # # . . .
    ///  y=0: . . . . # # . . . .
    /// </summary>
    Vector2Int[] GetHeartCells()
    {
        return new Vector2Int[]
        {
            new Vector2Int(2,4), new Vector2Int(3,4), new Vector2Int(6,4), new Vector2Int(7,4),
            new Vector2Int(1,3), new Vector2Int(2,3), new Vector2Int(3,3), new Vector2Int(4,3),
            new Vector2Int(5,3), new Vector2Int(6,3), new Vector2Int(7,3), new Vector2Int(8,3),
            new Vector2Int(2,2), new Vector2Int(3,2), new Vector2Int(4,2),
            new Vector2Int(5,2), new Vector2Int(6,2), new Vector2Int(7,2),
            new Vector2Int(3,1), new Vector2Int(4,1), new Vector2Int(5,1), new Vector2Int(6,1),
            new Vector2Int(4,0), new Vector2Int(5,0),
        };
    }

    /// <summary>
    /// ダイヤモンド形（32セル / 8ミノ相当）
    ///  col: 0 1 2 3 4 5 6 7 8 9
    /// y=10: . . . . # # . . . .
    ///  y=9: . . . # # # # . . .
    ///  y=8: . . # # # # # # . .
    ///  y=7: . # # # # # # # # .
    ///  y=6: . . # # # # # # . .
    ///  y=5: . . . # # # # . . .
    ///  y=4: . . . . # # . . . .
    /// </summary>
    Vector2Int[] GetDiamondCells()
    {
        return new Vector2Int[]
        {
            new Vector2Int(4,10), new Vector2Int(5,10),
            new Vector2Int(3,9),  new Vector2Int(4,9),  new Vector2Int(5,9),  new Vector2Int(6,9),
            new Vector2Int(2,8),  new Vector2Int(3,8),  new Vector2Int(4,8),  new Vector2Int(5,8),
            new Vector2Int(6,8),  new Vector2Int(7,8),
            new Vector2Int(1,7),  new Vector2Int(2,7),  new Vector2Int(3,7),  new Vector2Int(4,7),
            new Vector2Int(5,7),  new Vector2Int(6,7),  new Vector2Int(7,7),  new Vector2Int(8,7),
            new Vector2Int(2,6),  new Vector2Int(3,6),  new Vector2Int(4,6),  new Vector2Int(5,6),
            new Vector2Int(6,6),  new Vector2Int(7,6),
            new Vector2Int(3,5),  new Vector2Int(4,5),  new Vector2Int(5,5),  new Vector2Int(6,5),
            new Vector2Int(4,4),  new Vector2Int(5,4),
        };
    }

    /// <summary>
    /// 上向き矢印形（26セル）
    ///  col: 0 1 2 3 4 5 6 7 8 9
    /// y=10: . . . . # # . . . .
    ///  y=9: . . . # # # # . . .
    ///  y=8: . . # # # # # # . .
    ///  y=7: # # . . # # . . # #  ← 翼
    ///  y=6: . . . . # # . . . .
    ///  y=5: . . . . # # . . . .
    ///  y=4: . . . . # # . . . .
    ///  y=3: . . . . # # . . . .
    /// </summary>
    Vector2Int[] GetArrowCells()
    {
        return new Vector2Int[]
        {
            new Vector2Int(4,10), new Vector2Int(5,10),
            new Vector2Int(3,9),  new Vector2Int(4,9),  new Vector2Int(5,9),  new Vector2Int(6,9),
            new Vector2Int(2,8),  new Vector2Int(3,8),  new Vector2Int(4,8),  new Vector2Int(5,8),
            new Vector2Int(6,8),  new Vector2Int(7,8),
            new Vector2Int(0,7),  new Vector2Int(1,7),  new Vector2Int(4,7),  new Vector2Int(5,7),
            new Vector2Int(8,7),  new Vector2Int(9,7),
            new Vector2Int(4,6),  new Vector2Int(5,6),
            new Vector2Int(4,5),  new Vector2Int(5,5),
            new Vector2Int(4,4),  new Vector2Int(5,4),
            new Vector2Int(4,3),  new Vector2Int(5,3),
        };
    }
}
