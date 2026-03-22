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

    public static void RegisterExerciseClear(string exerciseId, float clearTimeSeconds)
    {
        if (string.IsNullOrWhiteSpace(exerciseId))
            return;

        ExercisePerformanceData performance = GetOrCreateExercisePerformance(exerciseId);
        performance.completed = true;
        performance.clearCount++;

        if (clearTimeSeconds > 0f &&
            (performance.bestTimeSeconds < 0f || clearTimeSeconds < performance.bestTimeSeconds))
        {
            performance.bestTimeSeconds = clearTimeSeconds;
        }

        Save();
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

    private static void EnsureLoaded()
    {
        if (data != null)
            return;

        Reload();
    }
}
