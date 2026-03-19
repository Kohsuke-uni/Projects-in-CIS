using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Tetromino : MonoBehaviour
{
    [Header("Fall Speeds")]
    public float normalFallSpeed = 1f;   // 通常落下速度（マス/秒）
    public float fastDropSpeed = 12f;    // 下加速落下速度（マス/秒）

    [Header("Grounded Action Limits")]
    public int groundedMoveAllowance = 14;      // 接地時の移動許容回数
    public int groundedRotateAllowance = 15;    // 接地時の回転許容回数

    [Header("Inactivity Lock")]
    public float inactivitySeconds = 0.9f;      // 無操作ロック秒

    [Header("Auto Shift (横長押し)")]
    [Tooltip("DAS: キー押下から連続移動が始まるまでの遅延秒数")]
    public float dasDelay = 0.15f;               // 例: 0.15
    [Tooltip("ARR: 連続移動の間隔秒数（小さいほど速い）")]
    public float arrInterval = 0.03f;            // 例: 0.03

    [Header("References")]
    public Board board;
    public Transform pivotOverride;              // 回転の中心（任意）
    public GhostPiece ghost;

    [Header("Meta")]
    // Spawnerの並び: I(0),J(1),L(2),O(3),S(4),T(5),Z(6) を想定
    public int typeIndex;                        // ミノ種類
    public bool spawnedFromHold;

    public Transform[] Cells { get; private set; } // ブロック4個

    // 内部状態
    private Transform _pivot;
    private bool locked = false;
    private bool fastDropping = false;
    private float accumulatedFall = 0f;

    private bool grounded = false;
    private bool groundedAllowanceInitialized = false;
    private int movesWhenGrounded;
    private int rotatesLeftWhenGrounded;

    private bool inactivityArmed = false;
    private float lastActionTime = -1f;

    // シーン別挙動
    private bool disableAutoFall = false;  // 自動落下を無効にするか
    private bool allowUpMove = false;      // ↑移動を許可するか
    private bool hardDropOnlyLock = false; // Normal: ハードドロップ時のみロック

    // SRS
    // 0:Up, 1:Right, 2:Down, 3:Left
    private int rotationIndex = 0;
    public bool lastMoveWasRotation { get; private set; } = false;

    // オートシフト（横長押し）
    // -1:左, 0:なし, +1:右（最後に押した方向が勝つ）
    private int horizontalDir = 0;
    private float dasTimer = 0f;
    private float arrTimer = 0f;

    public bool enablePlayerInput = true;

    public int RotationIndex => rotationIndex;

    public bool CpuTryMove(Vector3 delta) => TryMove(delta);

    public bool CpuTryRotate(int dir) => TryRotateAndRecordWithSE(dir);

    public void CpuHardDropAndLock()
    {
        while (TryMove(Vector3.down)) { }
        Lock();
    }

    public void InputRotateCW()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;
        TryRotateAndRecordWithSE(+1);
    }

    public void InputRotateCCW()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;
        TryRotateAndRecordWithSE(-1);
    }

    public void InputMoveLeft()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;

        horizontalDir = -1;
        dasTimer = 0f;
        arrTimer = 0f;
        if (TryMoveHorizontalOnce(-1))
            SoundManager.Instance?.PlaySE(SeType.Move);
    }

    public void InputMoveRight()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;

        horizontalDir = +1;
        dasTimer = 0f;
        arrTimer = 0f;
        if (TryMoveHorizontalOnce(+1))
            SoundManager.Instance?.PlaySE(SeType.Move);
    }

    public void InputStopHorizontal()
    {
        horizontalDir = 0;
        dasTimer = 0f;
        arrTimer = 0f;
    }

    public void InputSoftDropStart()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;

        fastDropping = true;
        if (TryMove(Vector3.down))
            SoundManager.Instance?.PlaySE(SeType.Move);
    }

    public void InputSoftDropEnd()
    {
        fastDropping = false;
    }

    public void InputHardDrop()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;

        while (TryMove(Vector3.down)) { }
        SoundManager.Instance?.PlaySE(SeType.HardDrop);
        Lock();
    }

    public void InputHold()
    {
        if (!enablePlayerInput || locked || GameControlUI.IsPaused) return;

        var spawner = FindObjectOfType<Spawner>();
        if (spawner != null && spawner.RequestHold(this))
            SoundManager.Instance?.PlaySE(SeType.Hold);
    }

    private void Awake()
    {
        var list = new List<Transform>(4);
        foreach (Transform c in transform)
        {
            string n = c.name.ToLower();
            if (n.Contains("pivot"))
            {
                if (pivotOverride == null) pivotOverride = c;
                continue;
            }
            if (c.GetComponent<SpriteRenderer>() == null) continue;
            if (list.Count < 4) list.Add(c);
        }
        Cells = list.ToArray();

        // Active piece should render above ghost by default.
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i] == null) continue;
            var sr = Cells[i].GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder += 1;
        }

        _pivot = pivotOverride != null ? pivotOverride : transform;

        grounded = false;
        groundedAllowanceInitialized = false;
        movesWhenGrounded = groundedMoveAllowance;
        rotatesLeftWhenGrounded = groundedRotateAllowance;
    }

    private void Start()
    {
        if (board == null) board = FindObjectOfType<Board>();

        // 初期位置をマスにスナップ
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);

        if (board == null || !board.IsValidPosition(this, Vector3.zero))
        {
            Debug.Log("Game Over (spawn invalid), piece: " + name);

            var practiceJudge = FindObjectOfType<PracticeJudge>();
            if (practiceJudge != null)
            {
                practiceJudge.OnTopOut();
            }

            var fortyJudge = FindObjectOfType<FortyLineJudge>();
            if (fortyJudge != null)
            {
                fortyJudge.OnTopOut();
            }
            enabled = false;
            return;
        }

        // スポーン角度から rotationIndex を算出（Z軸 0/90/180/270 前提）
        rotationIndex = Mathf.RoundToInt((360f - transform.eulerAngles.z) / 90f) & 3;

        // シーン名に応じた挙動
        string sceneName = SceneManager.GetActiveScene().name;
        PracticeJudge judge = FindObjectOfType<PracticeJudge>();
        RENJudge renJudge = FindObjectOfType<RENJudge>();
        bool isRenNormalMode = renJudge != null
        && renJudge.enabled
        && renJudge.renMode == RENJudge.RENMode.Normal;
        string displayKey = judge != null ? judge.GetCurrentDisplayName() : string.Empty;

        bool isNormalMode = sceneName.Contains("TSD_N") || sceneName.Contains("TST_N") 
        || sceneName.Contains("REN_N") || sceneName.Contains("40Lines")
        || displayKey.Contains("TSD_N") || displayKey.Contains("TST_N")
        || isRenNormalMode;

        if (isNormalMode)
        {
            // Normal：落下なし・↑移動あり・Spaceのみロック
            disableAutoFall = true;
            allowUpMove = true;
            hardDropOnlyLock = true;
        }
        else // if (sceneName.Contains("REN_E"))
        {
            // Easy(REN_E)：落下あり・↑移動なし
            disableAutoFall = false;
            allowUpMove = false;
            hardDropOnlyLock = false;
        }
        // Hard等はデフォルト（落下あり・↑移動なし）
    }

    private void Update()
    {
        // 🔴 一時停止中はミノの処理を完全に止める（入力・落下・ロックすべてストップ）
        if (GameControlUI.IsPaused) return;

        if (locked) return;

        if (enablePlayerInput)
        {
            HandleInput();
            HandleHorizontalAutoShift();
        }

        UpdateGroundedState();
        HandleFalling();
        TryAutoLockIfNeeded();
    
    }

    private void UpdateGroundedState()
    {
        bool touching = !board.IsValidPosition(this, Vector3.down);

        if (touching)
        {
            if (!grounded)
            {
                grounded = true;

                if (!groundedAllowanceInitialized)
                {
                    groundedAllowanceInitialized = true;
                    movesWhenGrounded = groundedMoveAllowance;
                    rotatesLeftWhenGrounded = groundedRotateAllowance;
                }

                if (!hardDropOnlyLock && !inactivityArmed)
                {
                    inactivityArmed = true;
                    lastActionTime = Time.time;
                }
            }
            else
            {
                grounded = true;
            }
        }
        else
        {
            grounded = false;
        }
    }

    private void HandleInput()
    {
        // Gamepadの入力を受ける
        var pad = Gamepad.current;

        // ↑はハードドロップ用なので、押した瞬間のみチェックする
        bool gpLeftDown = pad != null && pad.dpad.left.wasPressedThisFrame;
        bool gpRightDown = pad != null && pad.dpad.right.wasPressedThisFrame;
        bool gpUpDown = pad != null && pad.dpad.up.wasPressedThisFrame;
        bool gpDownDown = pad != null && pad.dpad.down.wasPressedThisFrame;
        
        
        bool gpLeftUp = pad != null && pad.dpad.left.wasReleasedThisFrame;
        bool gpRightUp = pad != null && pad.dpad.right.wasReleasedThisFrame;
        bool gpDownUp = pad != null && pad.dpad.down.wasReleasedThisFrame;

        bool gpLeftHeld = pad != null && pad.dpad.left.isPressed;
        bool gpRightHeld = pad != null && pad.dpad.right.isPressed;
        bool gpDownHeld = pad != null && pad.dpad.down.isPressed;
    
        // Hold (C / LeftShift)
        //コントローラーだとL1でホールド
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton4))
        {
            InputHold();
            return;
        }

        // --- 横移動：押下/離し（初回1マス + オートシフト準備） ---
        //コントローラーだとd-rightで右、d-leftで左
        // 左押下
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton16) || gpLeftDown)
        {
            InputMoveLeft();
        }
        // 右押下
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.JoystickButton17) || gpRightDown)
        {
            InputMoveRight();
        }
        // 左離し
        if (Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.JoystickButton16) || gpLeftUp)
        {
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.JoystickButton17) || gpRightHeld)
            {
                horizontalDir = +1;
                dasTimer = 0f;
                arrTimer = 0f;
            }
            else
            {
                horizontalDir = 0;
            }
        }
        // 右離し
        if (Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.JoystickButton17) || gpRightUp)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.JoystickButton16) || gpLeftHeld)
            {
                horizontalDir = -1;
                dasTimer = 0f;
                arrTimer = 0f;
            }
            else
            {
                horizontalDir = 0;
            }
        }

        // Move Up (only when allowed)
        //コントローラーはZLで上移動
        if (allowUpMove && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton6)))
        {
            if (TryMove(Vector3.up))
            {
                SoundManager.Instance?.PlaySE(SeType.Move);   // ★ 上移動も同じSE
                if (!hardDropOnlyLock) ArmInactivityTimerNow();
            }
        }

        // Rotate CW / CCW (RightArrow / LeftArrow)
        //コントローラーはAXで右回転、BYで左回転
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton3))
        {
            InputRotateCW();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton2))
        {
            InputRotateCCW();
        }

        // Hard Drop (W）
        //コントローラーだとD-up
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton14) || gpUpDown)
        {
            InputHardDrop();
        }

        // Soft Drop (↓)
        //コントローラーはD-downでソフトドロップ
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton15) || gpDownDown)
        {
            InputSoftDropStart();
        }

        if (Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.JoystickButton15))
        {
            InputSoftDropEnd();
        }
    }

    // 横長押しの自動移動（DAS/ARR）
    private void HandleHorizontalAutoShift()
    {

        if (horizontalDir == 0) return;

        var pad = Gamepad.current;
        
        // キー実際状態チェック（セーフガード）
        bool leftHeld = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.JoystickButton16) || (pad != null && pad.dpad.left.isPressed);
        bool rightHeld = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.JoystickButton17) || (pad != null && pad.dpad.right.isPressed);
        if (horizontalDir == -1 && !leftHeld)
        {
            horizontalDir = rightHeld ? +1 : 0; dasTimer = 0f; arrTimer = 0f;
        }
        if (horizontalDir == +1 && !rightHeld)
        {
            horizontalDir = leftHeld ? -1 : 0; dasTimer = 0f; arrTimer = 0f;
        }
        if (horizontalDir == 0) return;

        // DAS 経過前は待機
        if (dasTimer < dasDelay)
        {
            dasTimer += Time.deltaTime;
            return;
        }

        // ARR 間隔で移動を刻む
        arrTimer += Time.deltaTime;
        while (arrTimer >= arrInterval)
        {
            arrTimer -= arrInterval;
            if (!TryMoveHorizontalOnce(horizontalDir))
            {
                // ぶつかったら停止（キーの離し/向き変更で再開）
                break;
            }
            else
            {
                // ★ 長押し移動も成功した分だけSE
                SoundManager.Instance?.PlaySE(SeType.Move);
            }
        }
    }

    // 回転にSEを含めたラッパー
    private bool TryRotateAndRecordWithSE(int dir)
    {
        bool rotated = false;

        if (!grounded || rotatesLeftWhenGrounded > 0 || hardDropOnlyLock)
        {
            if (RotateSRS(dir))
            {
                rotated = true;
                // ★ 回転成功SE
                SoundManager.Instance?.PlaySE(SeType.Rotate);

                if (grounded && !hardDropOnlyLock)
                {
                    rotatesLeftWhenGrounded--;
                    ArmInactivityTimerNow();
                    TryAutoLockIfNeeded();
                }
            }
        }

        return rotated;
    }

    private void ArmInactivityTimerNow()
    {
        if (hardDropOnlyLock) return;
        inactivityArmed = true;
        lastActionTime = Time.time;
    }

    private void HandleFalling()
    {
        float baseSpeed = disableAutoFall ? 0f : normalFallSpeed;
        float speed = fastDropping ? fastDropSpeed : baseSpeed;

        accumulatedFall += speed * Time.deltaTime;

        while (accumulatedFall >= 1f)
        {
            accumulatedFall -= 1f;

            // ▼ ここを変更
            if (TryMove(Vector3.down))
            {
                // ↓キー長押しによるソフトドロップ中だけ、Move SE を鳴らす
                if (fastDropping)
                {
                    SoundManager.Instance?.PlaySE(SeType.Move);
                }
                continue;
            }

            grounded = true;

            if (hardDropOnlyLock)
            {
                // Normalでは自動ロックしない（SのみLock）
                break;
            }

            if (IsAllowanceDepleted())
            {
                Lock();
                break;
            }

            CheckInactivityAutoLock();
            break;
        }

        if (!hardDropOnlyLock)
        {
            CheckInactivityAutoLock();
        }
    }


    private void TryAutoLockIfNeeded()
    {
        if (hardDropOnlyLock) return;

        if (IsAllowanceDepleted() && !board.IsValidPosition(this, Vector3.down))
        {
            Lock();
            return;
        }
        CheckInactivityAutoLock();
    }

    private void CheckInactivityAutoLock()
    {
        if (hardDropOnlyLock) return;
        if (!grounded) return;
        if (!inactivityArmed) return;
        if (lastActionTime < 0f) return;
        if (board.IsValidPosition(this, Vector3.down)) return;

        if (Time.time - lastActionTime >= inactivitySeconds)
        {
            Lock();
        }
    }

    private bool IsAllowanceDepleted()
    {
        if (hardDropOnlyLock) return false;
        return (movesWhenGrounded <= 0) || (rotatesLeftWhenGrounded <= 0);
    }

    private bool TryMove(Vector3 delta)
    {
        // 上移動は可視範囲を超えないよう制限
        if (delta == Vector3.up)
        {
            foreach (var cell in Cells)
            {
                Vector2Int gridPos = board.WorldToGrid(cell.position + delta);
                if (gridPos.y >= board.visibleSize.y) return false;
            }
        }

        if (!board.IsValidPosition(this, delta)) return false;

        transform.position += delta;
        return true;
    }

    // 横方向に1マス動かす（長押し/単発どちらからも使用）
    private bool TryMoveHorizontalOnce(int dir) // dir: -1 左, +1 右
    {
        Vector3 delta = (dir < 0) ? Vector3.left : Vector3.right;

        if (!grounded || movesWhenGrounded > 0 || hardDropOnlyLock)
        {
            if (TryMove(delta))
            {
                if (grounded && !hardDropOnlyLock)
                {
                    // 接地時は許容量を消費（Normalでは消費しない）
                    movesWhenGrounded--;
                    ArmInactivityTimerNow();
                    TryAutoLockIfNeeded();
                }
                return true;
            }
        }
        return false;
    }

    // ===== SRS回転実装 =====

    // dir: +1=CW, -1=CCW
    private bool RotateSRS(int dir)
    {
        lastMoveWasRotation = false;

        int from = rotationIndex;
        int to = (rotationIndex + (dir > 0 ? 1 : 3)) & 3;

        float angle = -90f * dir;
        transform.RotateAround(_pivot.position, Vector3.forward, angle);

        Vector2Int[] kicks = GetSRSKicks(typeIndex, from, to, dir);

        foreach (var k in kicks)
        {
            Vector3 delta = new Vector3(k.x, k.y, 0f);
            if (board.IsValidPosition(this, delta))
            {
                transform.position += delta;
                rotationIndex = to;
                lastMoveWasRotation = true;
                return true;
            }
        }

        // 失敗：元に戻す
        transform.RotateAround(_pivot.position, Vector3.forward, -angle);
        return false;
    }

    private Vector2Int[] GetSRSKicks(int tIndex, int from, int to, int dir)
    {
        if (IsOPiece(tIndex))
        {
            return new[] { V(0, 0) };
        }
        if (IsIPiece(tIndex))
        {
            return GetI_Kicks(from, to, dir);
        }
        return GetJLSTZ_Kicks(from, to, dir);
    }

    private bool IsIPiece(int t) => t == 0; // I = 0（Spawnerの順に合わせる）
    private bool IsOPiece(int t) => t == 3; // O = 3

    // JLSTZ 共通（SRS正規）
    private Vector2Int[] GetJLSTZ_Kicks(int from, int to, int dir)
    {
        if (dir > 0) // CW
        {
            switch (from)
            {
                case 0: // Up -> Right (0->1)
                    return new[] {
                        V(0, 0), V(-1, 0), V(-1, +1), V(0, -2), V(-1, -2)
                    };
                case 1: // Right -> Down (1->2)
                    return new[] {
                        V(0, 0), V(+1, 0), V(+1, -1), V(0, +2), V(+1, +2)
                    };
                case 2: // Down -> Left (2->3)
                    return new[] {
                        V(0, 0), V(+1, 0), V(+1, +1), V(0, -2), V(+1, -2)
                    };
                case 3: // Left -> Up (3->0)
                    return new[] {
                        V(0, 0), V(-1, 0), V(-1, -1), V(0, +2), V(-1, +2)
                    };
            }
        }
        else // 反時計回り CCW
        {
            switch (from)
            {
                case 0: // 0 -> 3 (Up -> Left)    = 0->L
                    return new[] {
                        V(0, 0), V(+1, 0), V(+1, +1), V(0, -2), V(+1, -2)
                    };
                case 3: // 3 -> 2 (Left -> Down)  = L->2
                    return new[] {
                        V(0, 0), V(-1, 0), V(-1, -1), V(0, +2), V(-1, +2)
                    };
                case 2: // 2 -> 1 (Down -> Right) = 2->R
                    return new[] {
                        V(0, 0), V(-1, 0), V(-1, +1), V(0, -2), V(-1, -2)
                    };
                case 1: // 1 -> 0 (Right -> Up)   = R->0
                    return new[] {
                        V(0, 0), V(+1, 0), V(+1, -1), V(0, +2), V(+1, +2)
                    };
            }
        }

        return new[] { V(0, 0) };
    }

    // I専用（SRS正規）
    private Vector2Int[] GetI_Kicks(int from, int to, int dir)
    {
        if (dir > 0) // CW
        {
            switch (from)
            {
                case 0: return new[] { V(0,0), V(-2,0), V(+1,0), V(-2,-1), V(+1,+2) }; // Up->Right
                case 1: return new[] { V(0,0), V(-1,0), V(+2,0), V(-1,+2), V(+2,-1) }; // Right->Down
                case 2: return new[] { V(0,0), V(+2,0), V(-1,0), V(+2,+1), V(-1,-2) }; // Down->Left
                case 3: return new[] { V(0,0), V(+1,0), V(-2,0), V(+1,-2), V(-2,+1) }; // Left->Up
            }
        }
        else // CCW
        {
            switch (from)
            {
                case 0: return new[] { V(0,0), V(-1,0), V(+2,0), V(-1,+2), V(+2,-1) }; // Up->Left
                case 3: return new[] { V(0,0), V(-2,0), V(+1,0), V(-2,-1), V(+1,+2) }; // Left->Down
                case 2: return new[] { V(0,0), V(+1,0), V(-2,0), V(+1,-2), V(-2,+1) }; // Down->Right
                case 1: return new[] { V(0,0), V(+2,0), V(-1,0), V(+2,+1), V(-1,-2) }; // Right->Up
            }
        }
        return new[] { V(0,0) };
    }

    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    // ===== ここまで SRS =====

    public bool ContainsCell(Transform t)
    {
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] == t) return true;
        return false;
    }

    private void Lock()
    {
        if (locked) return;
        locked = true;

        // ★ 追加：ロック時のSE
        SoundManager.Instance?.PlaySE(SeType.Lock);

        // ゴースト削除
        if (ghost != null)
        {
            Destroy(ghost.gameObject);
            ghost = null;
        }

        // 子ブロックを独立させる（Board側で再アタッチ）
        foreach (var cell in Cells)
        {
            if (cell != null) cell.SetParent(null, true);
        }

        // 盤面に固定
        board.SetPiece(this);

        // ライン消去＆本数取得
        int linesCleared = board.ClearLinesAndGetCount();

        // ★ 追加：ライン消去SE
        if (linesCleared > 0)
        {
            SoundManager.Instance?.PlaySE(SeType.LineClear);
        }

        // ===== 各Judgeへ通知 =====
        var fortyJudge = FindObjectOfType<FortyLineJudge>();
        if (fortyJudge != null)
        {
            fortyJudge.OnPieceLocked(this, linesCleared);
            if (fortyJudge.IsStageCleared)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }
        }

        var practiceJudge = FindObjectOfType<PracticeJudge>();
        if (practiceJudge != null && practiceJudge.enabled)
        {
            practiceJudge.OnPieceLocked(this, linesCleared);
            if (practiceJudge.IsStageCleared)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }
        }

        var renJudge = FindObjectOfType<RENJudge>();
        if (renJudge != null && renJudge.enabled)
        {
            renJudge.OnPieceLocked(this, linesCleared);
            if (renJudge.IsStageCleared)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }
        }

        // Legacy code
        // if (practiceJudge == null)
        // {
        //     // (Optional) Legacy: T-Spin Double mode
        //     var tsdJudge = FindObjectOfType<TSpinDoubleJudge>();
        //     if (tsdJudge != null)
        //     {
        //         tsdJudge.OnPieceLocked(this, linesCleared);
        //         if (tsdJudge.IsStageCleared)
        //         {
        //             enabled = false;
        //             Destroy(gameObject);
        //             return;
        //         }
        //     }

        //     var tstJudge = FindObjectOfType<TSpinTripleJudge>();
        //     if (tstJudge != null)
        //     {
        //         tstJudge.OnPieceLocked(this, linesCleared);
        //         if (tstJudge.IsStageCleared)
        //         {
        //             enabled = false;
        //             Destroy(gameObject);
        //             return;
        //         }
        //     }
        // }

        // var renJudge = FindObjectOfType<RENJudge>();
        // if (renJudge != null)
        // {
        //     renJudge.OnPieceLocked(this, linesCleared);
        //     if (renJudge.IsStageCleared)
        //     {
        //         enabled = false;
        //         Destroy(gameObject);
        //         return;
        //     }
        // }

        // 次のミノを出す
        enabled = false;
        StartCoroutine(SpawnNextFrame());
    }

    private IEnumerator SpawnNextFrame()
    {
        yield return null;
        var spawner = FindObjectOfType<Spawner>();
        if (spawner != null) spawner.Spawn();
        Destroy(gameObject);
    }
}
