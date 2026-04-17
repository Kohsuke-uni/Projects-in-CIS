using System.IO;
using UnityEngine;

public static class SaveManager
{
    private const string SaveFileName = "save_data.json";

    private static SaveData data;

    public static SaveData Data
    {
        get
        {
            EnsureLoaded();
            return data;
        }
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static void Save()
    {
        EnsureLoaded();

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }

    public static void Reload()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<SaveData>(json);
        }

        if (data == null)
            data = new SaveData();
    }

    public static void ResetAll()
    {
        data = new SaveData();
        Save();
    }

    public static void SetUseClassicMinos(bool useClassicMinos)
    {
        Data.useClassicMinos = useClassicMinos;
        Save();
    }

    public static bool GetUseClassicMinos()
    {
        return Data.useClassicMinos;
    }

    public static void SetHorizontalStepThreshold(float value)
    {
        Data.horizontalStepThreshold = Mathf.Max(1f, value);
        Save();
    }

    public static float GetHorizontalStepThreshold(float fallback = 40f)
    {
        return Data.horizontalStepThreshold > 0f ? Data.horizontalStepThreshold : fallback;
    }

    public static void SetSoftDropThreshold(float value)
    {
        Data.softDropThreshold = Mathf.Max(1f, value);
        Save();
    }

    public static float GetSoftDropThreshold(float fallback = 50f)
    {
        return Data.softDropThreshold > 0f ? Data.softDropThreshold : fallback;
    }

    public static void AddLinesCleared(int lines)
    {
        if (lines == 0) return;

        Data.totalLinesCleared = Mathf.Max(0, Data.totalLinesCleared + lines);
        Save();
    }

    public static int GetTotalLinesCleared()
    {
        return Data.totalLinesCleared;
    }

    public static void AddRecordedTime(float seconds)
    {
        if (seconds <= 0f) return;

        Data.totalRecordedTimeSeconds += seconds;
        Save();
    }

    public static float GetTotalRecordedTimeSeconds()
    {
        return Data.totalRecordedTimeSeconds;
    }

    public static bool RegisterFortyLineTime(float clearTimeSeconds)
    {
        if (clearTimeSeconds <= 0f) return false;

        bool isBest = Data.bestFortyLineTimeSeconds < 0f || clearTimeSeconds < Data.bestFortyLineTimeSeconds;
        if (isBest)
        {
            Data.bestFortyLineTimeSeconds = clearTimeSeconds;
            Save();
        }

        return isBest;
    }

    public static float GetBestFortyLineTimeSeconds()
    {
        return Data.bestFortyLineTimeSeconds;
    }

    public static bool RegisterCpuBestTime(CpuDifficulty difficulty, float clearTimeSeconds)
    {
        if (clearTimeSeconds <= 0f)
            return false;

        ref float bestTime = ref GetCpuBestTimeField(difficulty);
        bool isBest = bestTime < 0f || clearTimeSeconds < bestTime;
        if (isBest)
        {
            bestTime = clearTimeSeconds;
            Save();
        }

        return isBest;
    }

    public static float GetBestCpuTimeSeconds(CpuDifficulty difficulty)
    {
        return difficulty switch
        {
            CpuDifficulty.Easy => Data.bestCpuEasyTimeSeconds,
            CpuDifficulty.Normal => Data.bestCpuNormalTimeSeconds,
            CpuDifficulty.Hard => Data.bestCpuHardTimeSeconds,
            _ => -1f,
        };
    }

    public static bool RegisterBestRen(int ren)
    {
        if (ren < 0) return false;

        bool isBest = ren > Data.bestRen;
        if (isBest)
        {
            Data.bestRen = ren;
            Save();
        }

        return isBest;
    }

    public static int GetBestRen()
    {
        return Data.bestRen;
    }

    public static bool RegisterMakeShapeHeartTime(float clearTimeSeconds)
    {
        return RegisterBestTime(ref Data.bestMakeShapeHeartTimeSeconds, clearTimeSeconds);
    }

    public static float GetBestMakeShapeHeartTimeSeconds()
    {
        return Data.bestMakeShapeHeartTimeSeconds;
    }

    public static bool RegisterMakeShapeHardTime(float clearTimeSeconds)
    {
        return RegisterBestTime(ref Data.bestMakeShapeHardTimeSeconds, clearTimeSeconds);
    }

    public static float GetBestMakeShapeHardTimeSeconds()
    {
        return Data.bestMakeShapeHardTimeSeconds;
    }

    public static bool RegisterPerfectClearTime(float clearTimeSeconds)
    {
        return RegisterBestTime(ref Data.bestPerfectClearTimeSeconds, clearTimeSeconds);
    }

    public static float GetBestPerfectClearTimeSeconds()
    {
        return Data.bestPerfectClearTimeSeconds;
    }

    public static bool RegisterExerciseClear(string exerciseId, float clearTimeSeconds)
    {
        if (string.IsNullOrWhiteSpace(exerciseId))
            return false;

        ExercisePerformanceData performance = GetOrCreateExercisePerformance(exerciseId);
        performance.completed = true;
        performance.clearCount++;
        bool isBest = false;

        if (clearTimeSeconds > 0f &&
            (performance.bestTimeSeconds < 0f || clearTimeSeconds < performance.bestTimeSeconds))
        {
            performance.bestTimeSeconds = clearTimeSeconds;
            isBest = true;
        }

        Save();
        return isBest;
    }

    public static ExercisePerformanceData GetExercisePerformance(string exerciseId)
    {
        if (string.IsNullOrWhiteSpace(exerciseId))
            return null;

        for (int i = 0; i < Data.exercisePerformance.Count; i++)
        {
            ExercisePerformanceData performance = Data.exercisePerformance[i];
            if (performance != null && performance.exerciseId == exerciseId)
                return performance;
        }

        return null;
    }

    private static ExercisePerformanceData GetOrCreateExercisePerformance(string exerciseId)
    {
        ExercisePerformanceData performance = GetExercisePerformance(exerciseId);
        if (performance != null)
            return performance;

        performance = new ExercisePerformanceData
        {
            exerciseId = exerciseId
        };
        Data.exercisePerformance.Add(performance);
        return performance;
    }

    private static ref float GetCpuBestTimeField(CpuDifficulty difficulty)
    {
        switch (difficulty)
        {
            case CpuDifficulty.Easy:
                return ref Data.bestCpuEasyTimeSeconds;
            case CpuDifficulty.Normal:
                return ref Data.bestCpuNormalTimeSeconds;
            default:
                return ref Data.bestCpuHardTimeSeconds;
        }
    }

    private static bool RegisterBestTime(ref float bestTimeSeconds, float clearTimeSeconds)
    {
        if (clearTimeSeconds <= 0f)
            return false;

        bool isBest = bestTimeSeconds < 0f || clearTimeSeconds < bestTimeSeconds;
        if (isBest)
        {
            bestTimeSeconds = clearTimeSeconds;
            Save();
        }

        return isBest;
    }

    private static void EnsureLoaded()
    {
        if (data != null)
            return;

        Reload();
    }
}
