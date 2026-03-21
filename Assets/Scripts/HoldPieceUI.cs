using UnityEngine;

public class HoldPieceUI : MonoBehaviour
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
        public Vector2 localPosition;
    }

    [Header("References")]
    public Spawner spawner;                 // Spawnerを参照
    public Transform displayRoot;           // 表示位置の親Transform
    public Tetromino[] previewPrefabs;      // 表示に使うテトロミノのプレハブ群
    public Tetromino[] classicPreviewPrefabs;

    [Header("Layout")]
    public float itemScale = 0.5f;          // ミノの表示スケール
    public Vector3 itemOffset = Vector3.zero; // 表示位置の微調整
    [Tooltip("特定ミノだけ個別の表示位置を指定（x,yローカル座標）")]
    public PiecePositionOverride[] piecePositionOverrides;

    private GameObject currentPreview;

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
        {
            spawner.QueueChanged += Refresh;
            spawner.OnHoldPieceReleased += Clear;
        }
        Refresh();
    }

    // 無効化時にイベント購読解除とUIのクリア
    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.QueueChanged -= Refresh;
            spawner.OnHoldPieceReleased -= Clear;
        }
        Clear();
    }

    // 現在のホールドUIを削除
    private void Clear()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    // ホールドしているミノの表示を更新
    private void Refresh()
    {
        Tetromino[] activePreviewPrefabs = ActivePreviewPrefabs;
        if (spawner == null || activePreviewPrefabs == null || activePreviewPrefabs.Length == 0)
            return;

        Clear();

        int? held = spawner.GetHeldIndex();
        if (held == null) return;

        int idx = held.Value;
        if (idx < 0 || idx >= activePreviewPrefabs.Length) return;

        Tetromino prefab = activePreviewPrefabs[idx];
        currentPreview = Instantiate(prefab.gameObject, displayRoot != null ? displayRoot : transform);
        currentPreview.transform.localPosition = GetOffsetForPiece(idx);
        currentPreview.transform.localRotation = Quaternion.identity;
        currentPreview.transform.localScale = Vector3.one * itemScale;

        foreach (var mb in currentPreview.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
        foreach (var col in currentPreview.GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (var rb in currentPreview.GetComponentsInChildren<Rigidbody2D>())
            Destroy(rb);

        var ghost = currentPreview.GetComponentInChildren<GhostPiece>();
        if (ghost != null) Destroy(ghost.gameObject);
    }

    private Vector3 GetOffsetForPiece(int pieceIndex)
    {
        if (piecePositionOverrides != null)
        {
            for (int i = 0; i < piecePositionOverrides.Length; i++)
            {
                if ((int)piecePositionOverrides[i].pieceType == pieceIndex)
                {
                    Vector2 p = piecePositionOverrides[i].localPosition;
                    return new Vector3(p.x, p.y, itemOffset.z);
                }
            }
        }

        return itemOffset;
    }
}
