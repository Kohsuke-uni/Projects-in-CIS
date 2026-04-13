using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [System.Serializable]
    public struct BlockState
    {
        public int x;
        public int y;
        public string name;
        public string tag;
        public Sprite sprite;
        public Color color;
        public Vector3 localScale;
        public int sortingLayerID;
        public int sortingOrder;
    }

    [Header("Board Settings")]
    public Vector2Int size = new Vector2Int(10, 24);
    public Vector2Int visibleSize = new Vector2Int(10, 20);
    public Transform blockContainer;
    public Vector3 origin = Vector3.zero;

    [Header("Board Position")]
    public Vector2 boardOffset = Vector2.zero;
    public Transform gridVisual;
    public bool moveGridVisualWithBoard = true;

    [Header("Garbage Settings")]
    public GameObject garbagePrefab;

    [Header("Special Blocks")]
    public string fixedBlockTag = "FixedBlock";

    [Header("Line Clear")]
    public bool disableLineClear = false;

    [Header("Pending Garbage")]
    public PendingGarbageSystem pendingGarbageSystem;

    private Transform[,] grid;

    private void Awake()
    {
        ApplyBoardPosition();
        grid = new Transform[size.x, size.y];
        Debug.Log($"[Board:{name}] Awake size={size} visibleSize={visibleSize}");
    }

    private void OnValidate()
    {
        ApplyBoardPosition();
    }

    private void ApplyBoardPosition()
    {
        origin = new Vector3(boardOffset.x, boardOffset.y, 0f);

        if (moveGridVisualWithBoard && gridVisual != null)
        {
            float centerX = origin.x + (visibleSize.x - 1) * 0.5f;
            float centerY = origin.y + (visibleSize.y - 1) * 0.5f;
            gridVisual.position = new Vector3(centerX, centerY, gridVisual.position.z);
        }
    }

    public Vector2Int WorldToGrid(Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x - origin.x);
        int y = Mathf.RoundToInt(position.y - origin.y);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(Vector2Int cell)
    {
        float x = cell.x + origin.x;
        float y = cell.y + origin.y;
        return new Vector3(x, y, 0f);
    }

    public bool IsValidPosition(Tetromino tetromino, Vector3 move)
    {
        foreach (Transform block in tetromino.Cells)
        {
            Vector3 newPos = block.position + move;
            Vector2Int cell = WorldToGrid(newPos);

            if (!IsInside(cell))
                return false;

            if (IsOccupied(cell))
                return false;
        }

        return true;
    }

    private bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < size.x && cell.y >= 0 && cell.y < size.y;
    }

    private bool IsOccupied(Vector2Int cell)
    {
        return grid[cell.x, cell.y] != null;
    }

    public void SetPiece(Tetromino tetromino)
    {
        foreach (Transform block in tetromino.Cells)
        {
            Vector2Int cell = WorldToGrid(block.position);

            if (cell.y >= size.y)
                continue;

            if (cell.x < 0 || cell.x >= size.x || cell.y < 0)
            {
                Debug.LogWarning($"[Board:{name}] SetPiece out of bounds cell={cell}, world={block.position}");
                continue;
            }

            grid[cell.x, cell.y] = block;
            block.SetParent(blockContainer, true);
        }
    }

    public bool TryPlaceBlockAt(Transform block, Vector2Int cell)
    {
        if (block == null) return false;
        if (!IsInside(cell)) return false;
        if (IsOccupied(cell)) return false;

        grid[cell.x, cell.y] = block;
        block.position = GridToWorld(cell);
        block.SetParent(blockContainer, true);
        return true;
    }

    public void ClearLines()
    {
        if (disableLineClear) return;

        for (int y = 0; y < size.y; y++)
        {
            if (IsLineFull(y))
            {
                ClearLine(y);
                ShiftLinesDown(y + 1);
                y--;
            }
        }
    }

    private bool IsLineFull(int y)
    {
        for (int x = 0; x < size.x; x++)
        {
            if (grid[x, y] == null)
                return false;
        }

        return true;
    }

    private void ClearLine(int y)
    {
        for (int x = 0; x < size.x; x++)
        {
            if (grid[x, y] != null)
            {
                if (!string.IsNullOrEmpty(fixedBlockTag) && grid[x, y].CompareTag(fixedBlockTag))
                    continue;

                Destroy(grid[x, y].gameObject);
                grid[x, y] = null;
            }
        }
    }

    public int ClearLinesAndGetCount()
    {
        if (disableLineClear) return 0;

        int cleared = 0;

        for (int y = 0; y < size.y; y++)
        {
            if (IsLineFull(y))
            {
                ClearLine(y);
                ShiftLinesDown(y + 1);
                y--;
                cleared++;
            }
        }

        return cleared;
    }

    private void ShiftLinesDown(int startY)
    {
        for (int y = startY; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (grid[x, y] != null)
                {
                    if (!string.IsNullOrEmpty(fixedBlockTag) && grid[x, y].CompareTag(fixedBlockTag))
                        continue;

                    grid[x, y - 1] = grid[x, y];
                    grid[x, y] = null;
                    grid[x, y - 1].position += Vector3.down;
                }
            }
        }
    }

    public void ClearBoard()
    {
        if (blockContainer != null)
        {
            for (int i = blockContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = blockContainer.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (grid[x, y] != null)
                {
                    grid[x, y].gameObject.SetActive(false);
                    Destroy(grid[x, y].gameObject);
                    grid[x, y] = null;
                }
            }
        }
    }

    public void ClearBoardImmediate()
    {
        if (blockContainer != null)
        {
            for (int i = blockContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = blockContainer.GetChild(i).gameObject;
                child.SetActive(false);
                DestroyImmediate(child);
            }
        }

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (grid[x, y] == null)
                    continue;

                grid[x, y].gameObject.SetActive(false);
                DestroyImmediate(grid[x, y].gameObject);
                grid[x, y] = null;
            }
        }
    }

    public bool IsCellOccupiedOrOutOfBounds(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= size.x || cell.y < 0 || cell.y >= size.y)
            return true;

        return grid[cell.x, cell.y] != null;
    }

    public bool IsOccupiedInBounds(int x, int y)
    {
        if (x < 0 || x >= size.x || y < 0 || y >= size.y) return true;
        return grid[x, y] != null;
    }

    public List<BlockState> CaptureBlockStates()
    {
        List<BlockState> result = new List<BlockState>();

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Transform block = grid[x, y];
                if (block == null)
                    continue;

                SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
                result.Add(new BlockState
                {
                    x = x,
                    y = y,
                    name = block.name,
                    tag = block.tag,
                    sprite = sr != null ? sr.sprite : null,
                    color = sr != null ? sr.color : Color.white,
                    localScale = block.localScale,
                    sortingLayerID = sr != null ? sr.sortingLayerID : 0,
                    sortingOrder = sr != null ? sr.sortingOrder : 0
                });
            }
        }

        return result;
    }

    public void RestoreBlockStates(List<BlockState> blockStates, bool clearExisting = true)
    {
        if (clearExisting)
            ClearBoard();

        if (blockStates == null)
            return;

        for (int i = 0; i < blockStates.Count; i++)
        {
            BlockState state = blockStates[i];
            GameObject block = new GameObject(string.IsNullOrWhiteSpace(state.name) ? $"Block_{state.x}_{state.y}" : state.name);

            if (!string.IsNullOrWhiteSpace(state.tag))
                block.tag = state.tag;

            SpriteRenderer sr = block.AddComponent<SpriteRenderer>();
            sr.sprite = state.sprite;
            sr.color = state.color;
            sr.sortingLayerID = state.sortingLayerID;
            sr.sortingOrder = state.sortingOrder;

            block.transform.localScale = state.localScale;
            TryPlaceBlockAt(block.transform, new Vector2Int(state.x, state.y));
        }
    }

    public void ApplyGarbageLinesNow(int count)
    {
        Debug.Log($"[Board:{name}] ApplyGarbageLinesNow(count) called count={count}");

        if (count <= 0)
        {
            Debug.Log($"[Board:{name}] ApplyGarbageLinesNow skipped because count <= 0");
            return;
        }

        int sharedHole = Random.Range(0, size.x);
        Debug.Log($"[Board:{name}] ApplyGarbageLinesNow selected sharedHole={sharedHole}");

        ApplyGarbageLinesNow(count, sharedHole);
    }

    public void ApplyGarbageLinesNow(int count, int sharedHole)
    {
        Debug.Log($"[Board:{name}] ApplyGarbageLinesNow(count, hole) called count={count}, sharedHole={sharedHole}");

        if (count <= 0)
        {
            Debug.Log($"[Board:{name}] ApplyGarbageLinesNow(count, hole) skipped because count <= 0");
            return;
        }

        Tetromino activePiece = FindActiveTetrominoOnThisBoard();
        Debug.Log($"[Board:{name}] activePiece={(activePiece != null ? activePiece.name : "null")}");

        for (int i = 0; i < count; i++)
        {
            Debug.Log($"[Board:{name}] Adding garbage line {i + 1}/{count} with hole={sharedHole}");
            AddGarbageLine(sharedHole);

            if (activePiece != null)
            {
                bool escaped = MoveActivePieceToLowestNonOverlappingHeight(activePiece);
                Debug.Log($"[Board:{name}] MoveActivePieceToLowestNonOverlappingHeight result={escaped}");

                if (!escaped)
                {
                    Debug.Log($"[Board:{name}] active piece overlapped after garbage, top out");

                    VersusJudge versusJudge = FindObjectOfType<VersusJudge>();
                    if (versusJudge != null)
                    {
                        versusJudge.OnTopOut(this);
                    }
                    return;
                }
            }
        }
    }

    private void AddGarbageLine(int hole)
    {
        Debug.Log($"[Board:{name}] AddGarbageLine called hole={hole}");

        for (int y = size.y - 2; y >= 0; y--)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (grid[x, y] != null)
                {
                    grid[x, y + 1] = grid[x, y];
                    grid[x, y] = null;
                    grid[x, y + 1].position += Vector3.up;
                }
            }
        }

        for (int x = 0; x < size.x; x++)
        {
            if (x == hole)
                continue;

            GameObject block;

            if (garbagePrefab != null)
            {
                block = Instantiate(garbagePrefab);
                block.name = "Garbage";
            }
            else
            {
                Debug.LogWarning($"[Board:{name}] garbagePrefab is null");
                block = new GameObject("Garbage");
                block.AddComponent<SpriteRenderer>();
            }

            block.tag = fixedBlockTag;

            Vector2Int cell = new Vector2Int(x, 0);
            grid[x, 0] = block.transform;
            block.transform.position = GridToWorld(cell);

            if (blockContainer != null)
            {
                block.transform.SetParent(blockContainer, true);
            }

            Debug.Log($"[Board:{name}] Garbage block placed at x={x}, y=0");
        }
    }

    private bool MoveActivePieceToLowestNonOverlappingHeight(Tetromino activePiece)
    {
        if (activePiece == null)
            return true;

        if (IsValidPositionAtCurrentPlace(activePiece))
            return true;

        Vector3 originalPosition = activePiece.transform.position;

        for (int lift = 1; lift <= size.y; lift++)
        {
            Vector3 targetPosition = originalPosition + Vector3.up * lift;
            Vector3 move = targetPosition - activePiece.transform.position;

            if (IsValidPosition(activePiece, move))
            {
                activePiece.transform.position = targetPosition;
                return true;
            }
        }

        return false;
    }

    private bool IsValidPositionAtCurrentPlace(Tetromino activePiece)
    {
        return IsValidPosition(activePiece, Vector3.zero);
    }

    private Tetromino FindActiveTetrominoOnThisBoard()
    {
        Tetromino[] all = FindObjectsOfType<Tetromino>();

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].board == this && all[i].enabled)
                return all[i];
        }

        return null;
    }

    public bool IsBoardEmpty()
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (grid[x, y] != null)
                    return false;
            }
        }

        return true;
    }
}