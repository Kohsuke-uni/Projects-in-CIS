using UnityEngine;
using System.Collections.Generic;

public class NextQueueUI : MonoBehaviour
{
    [Header("References")]
    public Spawner spawner;
    public Transform listRoot;
    public Tetromino[] previewPrefabs;
    public Tetromino[] classicPreviewPrefabs;

    [Header("Layout")]
    public Vector3 itemOffset = new Vector3(0, -1.1f, 0);
    public float itemScale = 0.5f;

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
            Transform parent = listRoot != null ? listRoot : transform;

            GameObject go = Instantiate(prefab.gameObject, parent);
            go.SetActive(false);

            Vector3 pos = i * itemOffset;
            if (idx == 0 || idx == 3)
                pos.x = -0.36f;

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
            {
                if (mb is Tetromino)
                    continue;
                mb.enabled = false;
            }

            foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;

            foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true))
                Destroy(rb);

            var ghost = go.GetComponentInChildren<GhostPiece>(true);
            if (ghost != null)
                Destroy(ghost.gameObject);

            go.SetActive(true);
            previews.Add(go);
        }
    }
}
