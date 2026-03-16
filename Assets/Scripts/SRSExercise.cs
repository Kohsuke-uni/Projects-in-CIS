using UnityEngine;

[CreateAssetMenu(fileName = "SRSExercise", menuName = "SRS/Exercise")]
public class SRSExercise : ScriptableObject
{
    public enum SpawnPieceType
    {
        I,
        J,
        L,
        O,
        S,
        T,
        Z
    }

    public string exerciseId;
    public string displayName;
    public string targetSceneName = "Exercise_Scene";
    public string instructionText;
    public PracticeJudge.PracticeType practiceType = PracticeJudge.PracticeType.TSpinDouble;
    public SpawnPieceType spawnPiece = SpawnPieceType.T;

    [System.Serializable]
    public struct BlockData
    {
        public Vector2Int cell;
        [Tooltip("ExerciseSceneLoader.blockPrefabs 内の Prefab 名と一致させる (例: red)")]
        public string color;
    }

    public BlockData[] initialBlocks;
}
