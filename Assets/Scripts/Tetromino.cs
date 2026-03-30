using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class Tetromino : MonoBehaviour
{
    [Header("Fall Speeds")]
    public float normalFallSpeed = 1f;
    public float fastDropSpeed = 12f;

    [Header("Grounded Action Limits")]
    public int groundedMoveAllowance = 14;
    public int groundedRotateAllowance = 15;

    [Header("Inactivity Lock")]
    public float inactivitySeconds = 0.9f;

    [Header("Auto Shift (横長押し)")]
    [Tooltip("DAS: キー押下から連続移動が始まるまでの遅延秒数")]
    public float dasDelay = 0.15f;
    [Tooltip("ARR: 連続移動の間隔秒数（小さいほど速い）")]
    public float arrInterval = 0.03f;

    [Header("References")]
    public Board board;
    public Spawner spawner;
    public Transform pivotOverride;
    public GhostPiece ghost;
    public bool keepCellSpritesUpright = true;

    [Header("Meta")]
    public int typeIndex;
    public bool spawnedFromHold;

    public Transform[] Cells { get; private set; }

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

    private bool disableAutoFall = false;
    private bool allowUpMove = false;
    private bool hardDropOnlyLock = false;

    private int rotationIndex = 0;
    public bool lastMoveWasRotation { get; private set; } = false;

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

        if (spawner != null && spawner.RequestHold(this))
        {
            SoundManager.Instance?.PlaySE(SeType.Hold);
        }
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

        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);

        if (board == null || !board.IsValidPosition(this, Vector3.zero))
        {
            Debug.Log("Game Over (spawn invalid), piece: " + name);

            var versusJudge = FindObjectOfType<VersusJudge>();
            if (versusJudge != null)
            {
                versusJudge.OnTopOut(board);
            }

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

        rotationIndex = Mathf.RoundToInt((360f - transform.eulerAngles.z) / 90f) & 3;
        KeepCellSpritesUpright();

        string sceneName = SceneManager.GetActiveScene().name;
        PracticeJudge judge = FindObjectOfType<PracticeJudge>();
        RENJudge renJudge = FindObjectOfType<RENJudge>();
        bool isRenNormalMode = renJudge != null
        && renJudge.enabled
        && renJudge.renMode == RENJudge.RENMode.Normal;
        string displayKey = judge != null ? judge.GetCurrentDisplayName() : string.Empty;

        bool isNormalMode = sceneName.Contains("TSD_N") || sceneName.Contains("TST_N")
        || sceneName.Contains("REN_N") || sceneName.Contains("40Lines")
        || sceneName.Contains("MakeShape")
        || displayKey.Contains("TSD_N") || displayKey.Contains("TST_N")
        || isRenNormalMode;

        if (isNormalMode)
        {
            disableAutoFall = true;
            allowUpMove = true;
            hardDropOnlyLock = true;
        }
        else
        {
            disableAutoFall = false;
            allowUpMove = false;
            hardDropOnlyLock = false;
        }
    }

    private void Update()
    {
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
        var pad = Gamepad.current;

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

        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.JoystickButton4))
        {
            InputHold();
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.JoystickButton16) || gpLeftDown)
        {
            InputMoveLeft();
        }

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.JoystickButton17) || gpRightDown)
        {
            InputMoveRight();
        }

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

        if (allowUpMove && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton6)))
        {
            if (TryMove(Vector3.up))
            {
                SoundManager.Instance?.PlaySE(SeType.Move);
                if (!hardDropOnlyLock) ArmInactivityTimerNow();
            }
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton3))
        {
            InputRotateCW();
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton2))
        {
            InputRotateCCW();
        }

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton14) || gpUpDown)
        {
            InputHardDrop();
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton15) || gpDownDown)
        {
            InputSoftDropStart();
        }

        if (Input.GetKeyUp(KeyCode.S) || Input.GetKeyUp(KeyCode.JoystickButton15))
        {
            InputSoftDropEnd();
        }
    }

    private void HandleHorizontalAutoShift()
    {
        if (horizontalDir == 0) return;

        var pad = Gamepad.current;

        bool leftHeld = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.JoystickButton16) || (pad != null && pad.dpad.left.isPressed);
        bool rightHeld = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.JoystickButton17) || (pad != null && pad.dpad.right.isPressed);
        if (horizontalDir == -1 && !leftHeld)
        {
            horizontalDir = rightHeld ? +1 : 0;
            dasTimer = 0f;
            arrTimer = 0f;
        }
        if (horizontalDir == +1 && !rightHeld)
        {
            horizontalDir = leftHeld ? -1 : 0;
            dasTimer = 0f;
            arrTimer = 0f;
        }
        if (horizontalDir == 0) return;

        if (dasTimer < dasDelay)
        {
            dasTimer += Time.deltaTime;
            return;
        }

        arrTimer += Time.deltaTime;
        while (arrTimer >= arrInterval)
        {
            arrTimer -= arrInterval;
            if (!TryMoveHorizontalOnce(horizontalDir))
            {
                break;
            }
            else
            {
                SoundManager.Instance?.PlaySE(SeType.Move);
            }
        }
    }

    private bool TryRotateAndRecordWithSE(int dir)
    {
        bool rotated = false;

        if (!grounded || rotatesLeftWhenGrounded > 0 || hardDropOnlyLock)
        {
            if (RotateSRS(dir))
            {
                rotated = true;
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

            if (TryMove(Vector3.down))
            {
                if (fastDropping)
                {
                    SoundManager.Instance?.PlaySE(SeType.Move);
                }
                continue;
            }

            grounded = true;

            if (hardDropOnlyLock)
            {
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

    private bool TryMoveHorizontalOnce(int dir)
    {
        Vector3 delta = (dir < 0) ? Vector3.left : Vector3.right;

        if (!grounded || movesWhenGrounded > 0 || hardDropOnlyLock)
        {
            if (TryMove(delta))
            {
                if (grounded && !hardDropOnlyLock)
                {
                    movesWhenGrounded--;
                    ArmInactivityTimerNow();
                    TryAutoLockIfNeeded();
                }
                return true;
            }
        }
        return false;
    }

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
                KeepCellSpritesUpright();
                lastMoveWasRotation = true;
                return true;
            }
        }

        transform.RotateAround(_pivot.position, Vector3.forward, -angle);
        return false;
    }

    private void KeepCellSpritesUpright()
    {
        if (!keepCellSpritesUpright || Cells == null)
            return;

        for (int i = 0; i < Cells.Length; i++)
        {
            Transform cell = Cells[i];
            if (cell == null)
                continue;

            cell.rotation = Quaternion.identity;
        }
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

    private bool IsIPiece(int t) => t == 0;
    private bool IsOPiece(int t) => t == 3;

    private Vector2Int[] GetJLSTZ_Kicks(int from, int to, int dir)
    {
        if (dir > 0)
        {
            switch (from)
            {
                case 0:
                    return new[] { V(0, 0), V(-1, 0), V(-1, +1), V(0, -2), V(-1, -2) };
                case 1:
                    return new[] { V(0, 0), V(+1, 0), V(+1, -1), V(0, +2), V(+1, +2) };
                case 2:
                    return new[] { V(0, 0), V(+1, 0), V(+1, +1), V(0, -2), V(+1, -2) };
                case 3:
                    return new[] { V(0, 0), V(-1, 0), V(-1, -1), V(0, +2), V(-1, +2) };
            }
        }
        else
        {
            switch (from)
            {
                case 0:
                    return new[] { V(0, 0), V(+1, 0), V(+1, +1), V(0, -2), V(+1, -2) };
                case 3:
                    return new[] { V(0, 0), V(-1, 0), V(-1, -1), V(0, +2), V(-1, +2) };
                case 2:
                    return new[] { V(0, 0), V(-1, 0), V(-1, +1), V(0, -2), V(-1, -2) };
                case 1:
                    return new[] { V(0, 0), V(+1, 0), V(+1, -1), V(0, +2), V(+1, +2) };
            }
        }

        return new[] { V(0, 0) };
    }

    private Vector2Int[] GetI_Kicks(int from, int to, int dir)
    {
        if (dir > 0)
        {
            switch (from)
            {
                case 0: return new[] { V(0, 0), V(-2, 0), V(+1, 0), V(-2, -1), V(+1, +2) };
                case 1: return new[] { V(0, 0), V(-1, 0), V(+2, 0), V(-1, +2), V(+2, -1) };
                case 2: return new[] { V(0, 0), V(+2, 0), V(-1, 0), V(+2, +1), V(-1, -2) };
                case 3: return new[] { V(0, 0), V(+1, 0), V(-2, 0), V(+1, -2), V(-2, +1) };
            }
        }
        else
        {
            switch (from)
            {
                case 0: return new[] { V(0, 0), V(-1, 0), V(+2, 0), V(-1, +2), V(+2, -1) };
                case 3: return new[] { V(0, 0), V(-2, 0), V(+1, 0), V(-2, -1), V(+1, +2) };
                case 2: return new[] { V(0, 0), V(+1, 0), V(-2, 0), V(+1, -2), V(-2, +1) };
                case 1: return new[] { V(0, 0), V(+2, 0), V(-1, 0), V(+2, +1), V(-1, -2) };
            }
        }
        return new[] { V(0, 0) };
    }

    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

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

        SoundManager.Instance?.PlaySE(SeType.Lock);

        if (ghost != null)
        {
            Destroy(ghost.gameObject);
            ghost = null;
        }

        foreach (var cell in Cells)
        {
            if (cell != null) cell.SetParent(null, true);
        }

        // 👇 まず盤面に置く
        board.SetPiece(this);

        // 👇 ★ここを追加（超重要）
        int linesCleared = board.ClearLinesAndGetCount();

        // 👇 対戦用ゴミ処理
        var versusJudge = FindObjectOfType<VersusJudge>();
        if (versusJudge != null)
        {
            versusJudge.OnLinesCleared(this, board, linesCleared);
        }

        // 👇 SE
        if (linesCleared > 0)
        {
            SoundManager.Instance?.PlaySE(SeType.LineClear);
            PlaySpecialClearAnimation(linesCleared);
        }

        // 👇 既存Judge（そのままでOK）
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

        var makeShapeJudge = FindObjectOfType<MakeShapeJudge>();
        if (makeShapeJudge != null && makeShapeJudge.enabled)
        {
            makeShapeJudge.OnPieceLocked(this, linesCleared);
            if (makeShapeJudge.IsStageCleared)
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

    private void PlaySpecialClearAnimation(int linesCleared)
    {
        SpecialClearAnimationUI animationUI = FindObjectOfType<SpecialClearAnimationUI>();
        if (animationUI == null)
            return;

        bool isTSpin = typeIndex == 5 && lastMoveWasRotation;

        if (isTSpin)
        {
            if (linesCleared == 2)
                animationUI.PlayTSpinDouble();
            else if (linesCleared == 3)
                animationUI.PlayTSpinTriple();

            return;
        }

        if (linesCleared == 4)
            animationUI.PlayTetris();
    }

    private IEnumerator SpawnNextFrame()
    {
        yield return null;

        if (spawner != null)
        {
            spawner.Spawn();
        }
        else
        {
            Debug.LogWarning("Tetromino: spawner が未設定です。");
        }

        Destroy(gameObject);
    }
}
