using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CpuAgent : MonoBehaviour
{
    public CpuDifficulty difficulty = CpuDifficulty.Normal;

    private Tetromino piece;
    private Board board;

    private float thinkDelay;// 次の手を考えるまでの待ち時間（難易度調整用）
    private int topK; // 上位K手からランダムに選ぶ（Easy/Normal用）
    private int xStride; // 横移動の試行間隔（1なら全て、2なら1つおきなど。難易度調整用）

    private float wLines, wHoles, wAggHeight, wBump, wMaxHeight; // 評価関数の重み（ライン/穴/高さの合計/凸凹/最大高さ）

    private float rotateStepDelay, moveStepDelay, hardDropDelay; // 動作実行の間隔（回転/移動/ハードドロップ）

    private void Awake()
    {
        piece = GetComponent<Tetromino>();
        board = piece.board;

        AutoSetDifficultyByScene();
        
        ApplyDifficulty();
        StartCoroutine(ThinkAndPlay());
    }

    private void ApplyDifficulty()
    {
        switch (difficulty)
        {
            case CpuDifficulty.Easy:
                thinkDelay = 0.55f;
                topK = 6;
                xStride = 2;
                wLines = 1.0f; wHoles = 2.5f; wAggHeight = 0.35f; wBump = 0.25f; wMaxHeight = 0.8f;

                rotateStepDelay = 0.24f;
                moveStepDelay = 0.12f;
                hardDropDelay = 0.20f;
                break;

            case CpuDifficulty.Normal:
                thinkDelay = 0.25f;
                topK = 3;
                xStride = 1;
                wLines = 1.2f; wHoles = 4.0f; wAggHeight = 0.45f; wBump = 0.35f; wMaxHeight = 1.0f;

                rotateStepDelay = 0.1f;
                moveStepDelay = 0.04f;
                hardDropDelay = 0.1f;
                break;

            default: // Hard
                thinkDelay = 0.08f;
                topK = 1;
                xStride = 1;
                wLines = 1.4f; wHoles = 6.0f; wAggHeight = 0.55f; wBump = 0.45f; wMaxHeight = 1.2f;

                rotateStepDelay = 0.02f;
                moveStepDelay = 0.01f;
                hardDropDelay = 0.02f;
                break;
        }
        Debug.Log($"[CPU Apply] diff={difficulty} thinkDelay={thinkDelay} topK={topK} xStride={xStride} rotateStepDelay={rotateStepDelay} moveStepDelay={moveStepDelay} hardDropDelay={hardDropDelay}");
    }

    private IEnumerator ThinkAndPlay()
    {
        yield return new WaitForSeconds(thinkDelay);

        if (piece == null || board == null) yield break;

        var move = FindBestMoveTopK();
        yield return ExecuteMove(move);
    }

    private struct MovePlan
    {
        public int rotateCwCount; // 0..3
        public int targetX;       // pivot x を合わせる
    }

    private MovePlan FindBestMoveTopK()
    {
        // 現在のピース状態を保存
        var savedPos = piece.transform.position;
        var savedRot = piece.transform.rotation;
        int savedRotIndex = piece.RotationIndex;

        List<(MovePlan plan, float score)> candidates = new List<(MovePlan, float)>();

        // 回転0..3回（CW）を試す
        for (int r = 0; r < 4; r++)
        {
            // 元に戻してから r 回回転
            Restore(savedPos, savedRot, savedRotIndex);
            for (int i = 0; i < r; i++)
            {
                if (!piece.CpuTryRotate(+1)) break;
            }

            // 盤面幅ぶん targetX を試す（strideで難易度調整）
            for (int tx = 0; tx < board.size.x; tx += xStride)
            {
                Restore(savedPos, savedRot, savedRotIndex);
                // r回回転
                bool rotOk = true;
                for (int i = 0; i < r; i++)
                {
                    if (!piece.CpuTryRotate(+1)) { rotOk = false; break; }
                }
                if (!rotOk) continue;

                // pivotX を tx に寄せる（単純に左右移動を試す）
                int safety = 40;
                while (safety-- > 0)
                {
                    int px = Mathf.RoundToInt(piece.transform.position.x - board.origin.x);
                    if (px == tx) break;

                    Vector3 dir = (tx < px) ? Vector3.left : Vector3.right;
                    if (!piece.CpuTryMove(dir)) break;
                }

                // ここで“落下後”を評価するため、仮にハードドロップして Lock せずに盤面スコアを取る
                // 方法：一旦下まで落として、Cells位置を使って仮想的に占有を作って評価（board.gridは触らない）
                float score = EvaluateAfterHardDropWithoutLock();
                candidates.Add((new MovePlan { rotateCwCount = r, targetX = tx }, score));
            }
        }

        // 元に戻す
        Restore(savedPos, savedRot, savedRotIndex);

        // スコアで降順、上位Kからランダム（Easy/Normal用）
        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        int k = Mathf.Clamp(topK, 1, candidates.Count);
        int pick = Random.Range(0, k);
        return candidates[pick].plan;
    }

    private float EvaluateAfterHardDropWithoutLock()
    {
        // 現在状態を保存
        var savedPos = piece.transform.position;

        // 下まで落とす
        while (piece.CpuTryMove(Vector3.down)) { }

        // board.grid を “読み取り”しつつ、ピースCellsを仮に追加した占有で評価する
        int w = board.size.x;
        int h = board.size.y;

        bool[,] occ = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                occ[x, y] = board.IsOccupiedInBounds(x, y);

        // ピースCellsを追加
        foreach (var cell in piece.Cells)
        {
            Vector2Int c = board.WorldToGrid(cell.position);
            if (c.x >= 0 && c.x < w && c.y >= 0 && c.y < h)
                occ[c.x, c.y] = true;
        }

        // ライン消去数（仮）
        int lines = 0;
        for (int y = 0; y < h; y++)
        {
            bool full = true;
            for (int x = 0; x < w; x++)
            {
                if (!occ[x, y]) { full = false; break; }
            }
            if (full) lines++;
        }

        // 高さ/穴/凸凹
        int[] heights = new int[w];
        int aggHeight = 0;
        int maxHeight = 0;
        int holes = 0;

        for (int x = 0; x < w; x++)
        {
            int colH = 0;
            bool seenBlock = false;
            for (int y = h - 1; y >= 0; y--)
            {
                if (occ[x, y])
                {
                    if (!seenBlock)
                    {
                        colH = y + 1;
                        seenBlock = true;
                    }
                }
                else
                {
                    if (seenBlock) holes++;
                }
            }
            heights[x] = colH;
            aggHeight += colH;
            if (colH > maxHeight) maxHeight = colH;
        }

        int bump = 0;
        for (int x = 0; x < w - 1; x++)
            bump += Mathf.Abs(heights[x] - heights[x + 1]);

        float score =
            + wLines * lines
            - wHoles * holes
            - wAggHeight * aggHeight
            - wBump * bump
            - wMaxHeight * maxHeight;

        // 元の位置へ戻す（落としっぱなし防止）
        piece.transform.position = savedPos;

        return score;
    }

    private IEnumerator ExecuteMove(MovePlan plan)
    {
        // 回転
        for (int i = 0; i < plan.rotateCwCount; i++)
        {
            piece.CpuTryRotate(+1);
            yield return new WaitForSeconds(rotateStepDelay);
        }

        // 横移動（pivot x を合わせる）
        int safety = 60;
        while (safety-- > 0)
        {
            int px = Mathf.RoundToInt(piece.transform.position.x - board.origin.x);
            if (px == plan.targetX) break;

            Vector3 dir = (plan.targetX < px) ? Vector3.left : Vector3.right;
            if (!piece.CpuTryMove(dir)) break;

            yield return new WaitForSeconds(moveStepDelay);
        }

        // ハードドロップ＆ロック
        yield return new WaitForSeconds(hardDropDelay);
        piece.CpuHardDropAndLock();
    }

    private void Restore(Vector3 pos, Quaternion rot, int rotIndex)
    {
        piece.transform.position = pos;
        piece.transform.rotation = rot;

        // rotationIndex は private なので、完全復元は難しい
        // ただし、今回の探索は「その場でTryRotate」ベースなので、回転は実質復元される（見た目揺れ対策は後で改良可）
        // ※ ここが気になる場合、Tetromino側に rotationIndex をセットできる関数を追加します。
    }

    private void AutoSetDifficultyByScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName.Contains("CPU_Easy"))
        {
            difficulty = CpuDifficulty.Easy;
        }
        else if (sceneName.Contains("CPU_Normal"))
        {
            difficulty = CpuDifficulty.Normal;
        }
        else if (sceneName.Contains("CPU_Hard"))
        {
            difficulty = CpuDifficulty.Hard;
        }

        Debug.Log($"CPU difficulty auto-set to: {difficulty} (Scene: {sceneName})");
    }
}