using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class ExerciseSceneLoader : MonoBehaviour
{
    public static SRSExercise RuntimeSelectedExercise { get; private set; }

    public static void SetRuntimeSelectedExercise(SRSExercise exercise)
    {
        RuntimeSelectedExercise = exercise;
    }

    public static void ClearRuntimeSelectedExercise()
    {
        RuntimeSelectedExercise = null;
    }

    public static bool RENMode { get; private set; }

    public static void SetRENMode(bool ren)
    {
        RENMode = ren;
    }

    public static RENJudge.RENMode RuntimeRENMode { get; private set; }

    public static void SetRuntimeRENMode(RENJudge.RENMode mode)
    {
        RuntimeRENMode = mode;
    }


    [Header("References")]
    public SRSExercise exercise;
    public Board board;
    public PracticeJudge practiceJudge;
    public RENJudge renJudge;
    public TMP_Text instructionTextUI;
    public TMP_Text exerciseNameTextUI;
    public TMP_Text exercisesRemainingTextUI;
    [Tooltip("color文字列と同名のPrefabをここから検索する")]
    public GameObject[] blockPrefabs;
    [Tooltip("Next Stage 用の候補一覧。exerciseId 末尾の番号で次を解決する")]
    public SRSExercise[] availableExercises;

    [Header("Options")]
    public bool clearBoardBeforePopulate = true;
    [Tooltip("ON ならランタイムで選択された exercise を優先")]
    public bool useRuntimeSelectedExercise = true;
    [Tooltip("ON なら最終的に使った exercise をランタイム選択として保存")]
    public bool syncResolvedExerciseToRuntimeSelection = true;

    [Header("REN Settings")]
    public bool isREN = false;
    public GameObject[] renPrefabs;
    public GameObject RENWalls;

    private readonly Dictionary<string, GameObject> prefabByName = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (ExerciseSessionManager.HasActiveSession && ExerciseSessionManager.GetCurrentExercise() != null)
            exercise = ExerciseSessionManager.GetCurrentExercise();

        if (useRuntimeSelectedExercise && RuntimeSelectedExercise != null)
            exercise = RuntimeSelectedExercise;

        if (syncResolvedExerciseToRuntimeSelection && exercise != null)
            RuntimeSelectedExercise = exercise;

        isREN = RENMode;

        if (renJudge != null)
            renJudge.renMode = RuntimeRENMode;

        ApplyJudgeMode();
        ApplyPracticeTypeToJudge();
    }

    private void Start()
    {
        if (isREN && RENWalls != null && RuntimeRENMode == RENJudge.RENMode.Easy)
        {
            RENWalls.SetActive(true);
        }

        if (isREN && renPrefabs != null && renPrefabs.Length > 0 && RuntimeRENMode == RENJudge.RENMode.Easy)
        {
            GameObject randomPrefab = Instantiate(renPrefabs[Random.Range(0, renPrefabs.Length)]);
            randomPrefab.transform.SetParent(RENWalls.transform, false);
        }
        
        ApplyInstructionText();
        ApplyExerciseSessionTexts();

        if (exercise == null || board == null || blockPrefabs == null || blockPrefabs.Length == 0)
        {
            Debug.LogWarning("[ExerciseSceneLoader] Missing reference(s).");
            return;
        }

        BuildPrefabLookup();

        if (clearBoardBeforePopulate)
            board.ClearBoard();

        for (int i = 0; i < exercise.initialBlocks.Length; i++)
        {
            var data = exercise.initialBlocks[i];

            if (!TryGetPrefabByColorKey(data.color, out GameObject prefab))
            {
                continue;
            }

            GameObject block = Instantiate(prefab);
            block.name = $"Block_{data.cell.x}_{data.cell.y}";

            bool placed = board.TryPlaceBlockAt(block.transform, data.cell);
            if (!placed)
            {
                Destroy(block);
                Debug.LogWarning($"[ExerciseSceneLoader] Could not place block at {data.cell}.");
            }
        }
    }

    private void ApplyPracticeTypeToJudge()
    {
        if (isREN) return;
        if (exercise == null) return;

        if (practiceJudge == null)
            practiceJudge = FindObjectOfType<PracticeJudge>();

        if (practiceJudge == null) return;
        practiceJudge.practiceType = exercise.practiceType;
    }

    private void ApplyJudgeMode()
    {
        if (practiceJudge != null)
            practiceJudge.enabled = !isREN;

        if (renJudge != null)
            renJudge.enabled = isREN;
    }


    private void ApplyInstructionText()
    {
        if (instructionTextUI == null) return;
        instructionTextUI.text = exercise != null ? exercise.instructionText : string.Empty;
        if (isREN)
        {
            switch (RuntimeRENMode)
            {
                case RENJudge.RENMode.Easy:
                    instructionTextUI.text = "Achieve as many REN as you can!";
                    break;
                case RENJudge.RENMode.Normal:
                    instructionTextUI.text = $"Achieve {renJudge.normalRequiredRen}+ REN to clear the stage. (No gravity)";
                    break;
                case RENJudge.RENMode.Hard:
                    instructionTextUI.text = $"Achieve {renJudge.hardRequiredRen}+ REN to clear the stage.";
                    break;
            }
        }
    }

    private void ApplyExerciseSessionTexts()
    {
        if (exerciseNameTextUI != null)
            exerciseNameTextUI.text = GetExerciseDisplayName();

        if (exercisesRemainingTextUI != null)
        {
            if (ExerciseSessionManager.HasActiveSession)
                exercisesRemainingTextUI.text = $"{ExerciseSessionManager.PendingCount}";
            else
                exercisesRemainingTextUI.text = string.Empty;
        }
    }

    private string GetExerciseDisplayName()
    {
        if (exercise == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(exercise.displayName))
            return exercise.displayName;

        if (!string.IsNullOrWhiteSpace(exercise.exerciseId))
            return exercise.exerciseId;

        return exercise.name;
    }

    private void BuildPrefabLookup()
    {
        prefabByName.Clear();

        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab == null) continue;

            string key = prefab.name;
            if (!prefabByName.ContainsKey(key))
                prefabByName.Add(key, prefab);
        }
    }

    private bool TryGetPrefabByColorKey(string colorKey, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrWhiteSpace(colorKey)) return false;

        string key = colorKey;
        bool found = prefabByName.TryGetValue(key, out prefab);
        if (!found)
        {
            Debug.LogWarning($"[ExerciseSceneLoader] No prefab match for '{colorKey}'. Available: {string.Join(", ", prefabByName.Keys)}");
        }
        return found;
    }

    public bool TryLoadNextExerciseById()
    {
        if (!TryResolveNextExercise(out SRSExercise nextExercise))
            return false;

        RuntimeSelectedExercise = nextExercise;

        string sceneName = string.IsNullOrWhiteSpace(nextExercise.targetSceneName)
            ? SceneManager.GetActiveScene().name
            : nextExercise.targetSceneName;

        SceneManager.LoadScene(sceneName);
        return true;
    }

    private bool TryResolveNextExercise(out SRSExercise nextExercise)
    {
        nextExercise = null;
        if (exercise == null || availableExercises == null || availableExercises.Length == 0)
            return false;

        string currentKey = !string.IsNullOrWhiteSpace(exercise.exerciseId) ? exercise.exerciseId : exercise.name;
        if (!TrySplitTrailingNumber(currentKey, out string prefix, out int number))
            return false;

        string targetKey = prefix + (number + 1);

        for (int i = 0; i < availableExercises.Length; i++)
        {
            var candidate = availableExercises[i];
            if (candidate == null) continue;

            string candidateKey = !string.IsNullOrWhiteSpace(candidate.exerciseId) ? candidate.exerciseId : candidate.name;
            if (candidateKey == targetKey)
            {
                nextExercise = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TrySplitTrailingNumber(string key, out string prefix, out int number)
    {
        prefix = string.Empty;
        number = 0;
        if (string.IsNullOrEmpty(key)) return false;

        int end = key.Length - 1;
        int start = end;
        while (start >= 0 && char.IsDigit(key[start]))
            start--;

        start++;
        if (start > end) return false;

        string numberPart = key.Substring(start, end - start + 1);
        if (!int.TryParse(numberPart, out number)) return false;

        prefix = key.Substring(0, start);
        return true;
    }
}
