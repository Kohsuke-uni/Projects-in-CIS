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

    private static readonly List<int> runtimeRenPrefabOrder = new List<int>();
    private static int runtimeRenPrefabCursor = 0;
    private static bool runtimeRenOrderUsesClassic = false;
    private static int runtimeRenOrderPrefabCount = 0;

    public static void ResetRenProgression()
    {
        runtimeRenPrefabOrder.Clear();
        runtimeRenPrefabCursor = 0;
        runtimeRenOrderUsesClassic = false;
        runtimeRenOrderPrefabCount = 0;
        RuntimeRENMode = RENJudge.RENMode.Easy;
    }


    [Header("References")]
    public SRSExercise exercise;
    public Board board;
    public PracticeJudge practiceJudge;
    public RENJudge renJudge;
    public TMP_Text instructionTextUI;
    public TMP_Text exerciseNameTextUI;
    public TMP_Text exercisesRemainingTextUI;
    public TMP_Text bestTimeTextUI;
    public MinoAppearanceCatalog appearanceCatalog;
    [Tooltip("デフォルト表示用。color文字列と同名のPrefabをここから検索する")]
    public GameObject[] blockPrefabs;
    [Tooltip("クラシック表示用。color文字列と同名のPrefabをここから検索する")]
    public GameObject[] classicBlockPrefabs;
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
    public GameObject[] classicRenPrefabs;
    public GameObject RENWalls;
    public GameObject classicRENWalls;

    private readonly Dictionary<string, GameObject> prefabByName = new Dictionary<string, GameObject>();
    private GameObject[] ActiveBlockPrefabs
    {
        get
        {
            if (appearanceCatalog != null)
                return appearanceCatalog.GetBlockPrefabs(SaveManager.GetUseClassicMinos());

            if (SaveManager.GetUseClassicMinos() &&
                classicBlockPrefabs != null &&
                classicBlockPrefabs.Length > 0)
            {
                return classicBlockPrefabs;
            }

            return blockPrefabs;
        }
    }

    private GameObject[] ActiveRenPrefabs
    {
        get
        {
            if (SaveManager.GetUseClassicMinos() &&
                classicRenPrefabs != null &&
                classicRenPrefabs.Length > 0)
            {
                return classicRenPrefabs;
            }

            return renPrefabs;
        }
    }

    private GameObject ActiveRenWalls
    {
        get
        {
            if (SaveManager.GetUseClassicMinos() && classicRENWalls != null)
                return classicRENWalls;

            return RENWalls;
        }
    }

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
        GameObject activeRenWalls = ActiveRenWalls;
        GameObject[] activeRenPrefabs = ActiveRenPrefabs;
        bool useClassicMinos = SaveManager.GetUseClassicMinos();

        if (RENWalls != null && RENWalls != activeRenWalls)
            RENWalls.SetActive(false);
        if (classicRENWalls != null && classicRENWalls != activeRenWalls)
            classicRENWalls.SetActive(false);

        if (isREN && activeRenWalls != null && RuntimeRENMode == RENJudge.RENMode.Easy)
        {
            activeRenWalls.SetActive(true);
        }

        if (isREN && activeRenPrefabs != null && activeRenPrefabs.Length > 0 &&
            activeRenWalls != null && RuntimeRENMode == RENJudge.RENMode.Easy)
        {
            EnsureRenPrefabOrder(activeRenPrefabs.Length, useClassicMinos);
            int prefabIndex = GetCurrentRenPrefabIndex(activeRenPrefabs.Length, useClassicMinos);
            GameObject randomPrefab = Instantiate(activeRenPrefabs[prefabIndex]);
            randomPrefab.transform.SetParent(activeRenWalls.transform, false);
        }
        
        ApplyInstructionText();
        RefreshExerciseHud();

        GameObject[] activeBlockPrefabs = ActiveBlockPrefabs;
        if (exercise == null || board == null || activeBlockPrefabs == null || activeBlockPrefabs.Length == 0)
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

    public void RefreshExerciseHud()
    {
        if (exerciseNameTextUI != null)
            exerciseNameTextUI.text = isREN ? string.Empty : GetExerciseDisplayName();

        if (exercisesRemainingTextUI != null)
        {
            if (ExerciseSessionManager.HasActiveSession)
                exercisesRemainingTextUI.text = $"{ExerciseSessionManager.PendingCount}";
            else
                exercisesRemainingTextUI.text = string.Empty;
        }

        if (bestTimeTextUI != null)
            bestTimeTextUI.text = GetBestTimeLabel();
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

    public string GetExercisePerformanceKey()
    {
        if (exercise == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(exercise.displayName))
            return exercise.displayName;

        return exercise.name;
    }

    private string GetBestTimeLabel()
    {
        if (isREN)
            return string.Empty;

        ExercisePerformanceData performance = SaveManager.GetExercisePerformance(GetExercisePerformanceKey());
        float bestTimeSeconds = performance != null ? performance.bestTimeSeconds : -1f;
        return $"BEST\n{FormatElapsedTime(bestTimeSeconds)}";
    }

    private string FormatElapsedTime(float seconds)
    {
        if (seconds < 0f)
            return "--:--";

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes}:{remainingSeconds:00.00}";
    }

    private void BuildPrefabLookup()
    {
        prefabByName.Clear();

        GameObject[] activeBlockPrefabs = ActiveBlockPrefabs;
        if (activeBlockPrefabs == null)
            return;

        for (int i = 0; i < activeBlockPrefabs.Length; i++)
        {
            var prefab = activeBlockPrefabs[i];
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

        string key = colorKey.Trim();
        if (prefabByName.TryGetValue(key, out prefab))
            return true;

        foreach (var pair in prefabByName)
        {
            if (pair.Key.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                prefab = pair.Value;
                return true;
            }
        }

        Debug.LogWarning($"[ExerciseSceneLoader] No prefab match for '{colorKey}'. Available: {string.Join(", ", prefabByName.Keys)}");
        return false;
    }

    public bool TryAdvanceRenStage()
    {
        if (!isREN)
            return false;

        bool useClassicMinos = SaveManager.GetUseClassicMinos();
        GameObject[] activeRenPrefabs = ActiveRenPrefabs;

        if (RuntimeRENMode == RENJudge.RENMode.Easy)
        {
            EnsureRenPrefabOrder(activeRenPrefabs != null ? activeRenPrefabs.Length : 0, useClassicMinos);

            if (runtimeRenPrefabCursor + 1 < runtimeRenPrefabOrder.Count)
            {
                runtimeRenPrefabCursor++;
            }
            else
            {
                runtimeRenPrefabOrder.Clear();
                runtimeRenPrefabCursor = 0;
                runtimeRenOrderPrefabCount = 0;
                RuntimeRENMode = RENJudge.RENMode.Normal;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return true;
        }

        if (RuntimeRENMode == RENJudge.RENMode.Normal)
        {
            RuntimeRENMode = RENJudge.RENMode.Hard;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return true;
        }

        return false;
    }

    private static void EnsureRenPrefabOrder(int prefabCount, bool useClassicMinos)
    {
        if (prefabCount <= 0)
            return;

        bool needsRebuild =
            runtimeRenPrefabOrder.Count == 0 ||
            runtimeRenOrderPrefabCount != prefabCount ||
            runtimeRenOrderUsesClassic != useClassicMinos;

        if (!needsRebuild)
            return;

        runtimeRenPrefabOrder.Clear();
        for (int i = 0; i < prefabCount; i++)
            runtimeRenPrefabOrder.Add(i);

        for (int i = runtimeRenPrefabOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = runtimeRenPrefabOrder[i];
            runtimeRenPrefabOrder[i] = runtimeRenPrefabOrder[j];
            runtimeRenPrefabOrder[j] = temp;
        }

        runtimeRenPrefabCursor = 0;
        runtimeRenOrderUsesClassic = useClassicMinos;
        runtimeRenOrderPrefabCount = prefabCount;
    }

    private static int GetCurrentRenPrefabIndex(int prefabCount, bool useClassicMinos)
    {
        EnsureRenPrefabOrder(prefabCount, useClassicMinos);

        if (runtimeRenPrefabOrder.Count == 0)
            return 0;

        runtimeRenPrefabCursor = Mathf.Clamp(runtimeRenPrefabCursor, 0, runtimeRenPrefabOrder.Count - 1);
        return runtimeRenPrefabOrder[runtimeRenPrefabCursor];
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

    public void OnNextStageButton()
    {
        if (isREN)
        {
            if (renJudge == null)
                renJudge = FindObjectOfType<RENJudge>();

            if (renJudge != null && renJudge.enabled)
                renJudge.OnNextStageButton();

            return;
        }

        if (practiceJudge == null)
            practiceJudge = FindObjectOfType<PracticeJudge>();

        if (practiceJudge != null && practiceJudge.enabled)
            practiceJudge.OnNextStageButton();
    }

    public void OnStageSelectButton()
    {
        if (isREN)
        {
            if (renJudge == null)
                renJudge = FindObjectOfType<RENJudge>();

            if (renJudge != null && renJudge.enabled)
                renJudge.OnStageSelectButton();

            return;
        }

        if (practiceJudge == null)
            practiceJudge = FindObjectOfType<PracticeJudge>();

        if (practiceJudge != null && practiceJudge.enabled)
            practiceJudge.OnStageSelectButton();
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
