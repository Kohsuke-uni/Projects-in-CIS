using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public bool useClassicMinos;
    public int totalLinesCleared;
    public float totalRecordedTimeSeconds;
    public float bestFortyLineTimeSeconds = -1f;
    public List<ExercisePerformanceData> exercisePerformance = new List<ExercisePerformanceData>();
}

[Serializable]
public class ExercisePerformanceData
{
    public string exerciseId;
    public bool completed;
    public float bestTimeSeconds = -1f;
    public int clearCount;
}
