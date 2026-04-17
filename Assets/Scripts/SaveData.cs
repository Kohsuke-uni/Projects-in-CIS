using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public bool useClassicMinos;
    public float horizontalStepThreshold = 40f;
    public float softDropThreshold = 50f;
    public int totalLinesCleared;
    public float totalRecordedTimeSeconds;
    public float bestFortyLineTimeSeconds = -1f;
    public float bestCpuEasyTimeSeconds = -1f;
    public float bestCpuNormalTimeSeconds = -1f;
    public float bestCpuHardTimeSeconds = -1f;
    public float bestMakeShapeHeartTimeSeconds = -1f;
    public float bestMakeShapeHardTimeSeconds = -1f;
    public float bestPerfectClearTimeSeconds = -1f;
    public int bestRen = -1;
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
