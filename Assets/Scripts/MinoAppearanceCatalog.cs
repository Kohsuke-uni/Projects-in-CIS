using UnityEngine;

[CreateAssetMenu(fileName = "MinoAppearanceCatalog", menuName = "Tetris/Mino Appearance Catalog")]
public class MinoAppearanceCatalog : ScriptableObject
{
    [Header("Default")]
    public Tetromino[] defaultTetrominoPrefabs;
    public GhostPiece[] defaultGhostPrefabs;
    public GameObject[] defaultBlockPrefabs;

    [Header("Classic")]
    public Tetromino[] classicTetrominoPrefabs;
    public GhostPiece[] classicGhostPrefabs;
    public GameObject[] classicBlockPrefabs;

    public Tetromino[] GetTetrominoPrefabs(bool useClassic)
    {
        if (useClassic && classicTetrominoPrefabs != null && classicTetrominoPrefabs.Length > 0)
            return classicTetrominoPrefabs;

        return defaultTetrominoPrefabs;
    }

    public GhostPiece[] GetGhostPrefabs(bool useClassic)
    {
        if (useClassic && classicGhostPrefabs != null && classicGhostPrefabs.Length > 0)
            return classicGhostPrefabs;

        return defaultGhostPrefabs;
    }

    public GameObject[] GetBlockPrefabs(bool useClassic)
    {
        if (useClassic && classicBlockPrefabs != null && classicBlockPrefabs.Length > 0)
            return classicBlockPrefabs;

        return defaultBlockPrefabs;
    }
}
