using UnityEngine;
using System.Collections.Generic;

public class NextQueueUI : MonoBehaviour
{
    public enum TetrominoType
    {
        I = 0,
        J = 1,
        L = 2,
        O = 3,
        S = 4,
        T = 5,
        Z = 6
    }

    [System.Serializable]
    public struct PiecePositionOverride
    {
        public TetrominoType pieceType;
        public Vector2 localOffset;
    }

    [Header("References")]
    public Spawner spawner;
    public Transform listRoot;
    public Tetromino[] previewPrefabs;
    public Tetromino[] classicPreviewPrefabs;

    [Header("Layout")]
    public Vector3 itemOffset = new Vector3(0, -1.1f, 0);
    public float itemScale = 0.5f;
    [Tooltip("各Next枠の基準位置に対するピースごとの補正")]
    public PiecePositionOverride[] piecePositionOverrides;

    [Header("Rendering")]
    public bool overrideSortingOrder = true;
    public int previewSortingOrder = 2;

    private readonly List<GameObject> previews = new List<GameObject>();

    private Tetromino[] ActivePreviewPrefabs
    {
        get
        {
            if (spawner != null)
            {
                Tetromino[] spawnerPrefabs = spawner.GetActivePreviewPrefabs();
                if (spawnerPrefabs != null && spawnerPrefabs.Length > 0)
                    return spawnerPrefabs;
            }

            if (SaveManager.GetUseClassicMinos() &&
                classicPreviewPrefabs != null &&
                classicPreviewPrefabs.Length > 0)
            {
                return classicPreviewPrefabs;
            }

            return previewPrefabs;
        }
    }

    // 有効化時にSpawnerのイベント購読と初期描画
    private void OnEnable()
    {
        if (spawner != null)
            spawner.QueueChanged += Refresh;
        Refresh();
    }

    // 無効化時にイベント購読解除と表示クリア
    private void OnDisable()
    {
        if (spawner != null)
            spawner.QueueChanged -= Refresh;
        Clear();
    }

    // 表示中のNextミノをすべて削除
    private void Clear()
    {
        foreach (var go in previews)
            Destroy(go);
        previews.Clear();
    }

    // Nextミノの一覧を再描画
    private void Refresh()
    {
        Tetromino[] activePreviewPrefabs = ActivePreviewPrefabs;
        if (spawner == null || activePreviewPrefabs == null || activePreviewPrefabs.Length == 0)
            return;

        Clear();

        int[] next = spawner.GetUpcoming(5);
        for (int i = 0; i < next.Length; i++)
        {
            int idx = next[i];
            if (idx < 0 || idx >= activePreviewPrefabs.Length) continue;

            Tetromino prefab = activePreviewPrefabs[idx];
            var go = Instantiate(prefab.gameObject, listRoot != null ? listRoot : transform);
            go.SetActive(false);

            Vector3 pos = GetPositionForPiece(idx, i);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * itemScale;

            Tetromino tet = go.GetComponent<Tetromino>();
            if (tet != null)
            {
                tet.isPreviewOnly = true;
                tet.enabled = false;
                tet.board = null;
                tet.spawner = null;
                tet.ghost = null;
            }

            if (overrideSortingOrder)
            {
                SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                    renderers[r].sortingOrder = previewSortingOrder;
            }

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                Destroy(mb);
            foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
                Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true))
                Destroy(rb);

            go.SetActive(true);
            previews.Add(go);
        }
    }

    private Vector3 GetPositionForPiece(int pieceIndex, int queueIndex)
    {
        Vector3 pos = queueIndex * itemOffset;

        if (piecePositionOverrides != null)
        {
            for (int i = 0; i < piecePositionOverrides.Length; i++)
            {
                if ((int)piecePositionOverrides[i].pieceType != pieceIndex)
                    continue;

                Vector2 offset = piecePositionOverrides[i].localOffset;
                pos.x += offset.x;
                pos.y += offset.y;
                return pos;
            }
        }

        // Preserve the previous default centering for wide/square pieces
        if (pieceIndex == (int)TetrominoType.I || pieceIndex == (int)TetrominoType.O)
            pos.x = -0.36f;

        return pos;
    }
}
