using System.Collections.Generic;
using UnityEngine;

public static class ExerciseSessionManager
{
    private static readonly List<SRSExercise> pendingExercises = new List<SRSExercise>();

    public static bool HasActiveSession => pendingExercises.Count > 0;
    public static int PendingCount => pendingExercises.Count;

    public static void StartSession(IEnumerable<SRSExercise> exercises, bool shuffle)
    {
        pendingExercises.Clear();
        if (exercises == null)
            return;

        foreach (SRSExercise exercise in exercises)
        {
            if (exercise != null)
                pendingExercises.Add(exercise);
        }

        if (shuffle && pendingExercises.Count > 1)
        {
            for (int i = pendingExercises.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                SRSExercise temp = pendingExercises[i];
                pendingExercises[i] = pendingExercises[j];
                pendingExercises[j] = temp;
            }
        }
    }

    public static void ClearSession()
    {
        pendingExercises.Clear();
    }

    public static SRSExercise GetCurrentExercise()
    {
        return HasActiveSession ? pendingExercises[0] : null;
    }

    public static bool MarkCurrentCorrect()
    {
        if (!HasActiveSession)
            return false;

        pendingExercises.RemoveAt(0);
        return HasActiveSession;
    }

    public static bool MoveCurrentToEnd()
    {
        if (!HasActiveSession)
            return false;

        if (pendingExercises.Count == 1)
            return true;

        SRSExercise current = pendingExercises[0];
        pendingExercises.RemoveAt(0);
        pendingExercises.Add(current);
        return true;
    }
}
