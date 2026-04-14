using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadButton : MonoBehaviour
{
    [Header("Scene Load")]
    public string sceneName;

    [Header("CPU Load (Optional)")]
    public CpuDifficulty cpuDifficulty = CpuDifficulty.Normal;

    [Header("Exercise Load (Optional)")]
    public SRSExercise targetExercise;
    public SRSExercise[] sessionExercises;
    public bool useExerciseTargetScene = true;
    public bool shuffleExerciseSession = true;
    public bool isREN = false;
    public RENJudge.RENMode renMode = RENJudge.RENMode.Easy;


    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        CpuAgent.ClearRuntimeDifficultyOverride();
        ExerciseSessionManager.ClearSession();
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(sceneName);
    }

    public void LoadCpuScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;

        CpuAgent.SetRuntimeDifficultyOverride(cpuDifficulty);
        ExerciseSessionManager.ClearSession();
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(sceneName);
    }

    public void LoadExerciseScene()
    {
        CpuAgent.ClearRuntimeDifficultyOverride();
        ExerciseSessionManager.ClearSession();
        ExerciseSceneLoader.SetRENMode(isREN);
        if (isREN && renMode == RENJudge.RENMode.Easy)
            ExerciseSceneLoader.ResetRenProgression();
        else
            ExerciseSceneLoader.SetRuntimeRENMode(renMode);
        if (targetExercise == null)
        {
            LoadScene();
            return;
        }

        ExerciseSceneLoader.SetRuntimeSelectedExercise(targetExercise);

        string targetSceneName = useExerciseTargetScene
            ? targetExercise.targetSceneName
            : sceneName;

        if (string.IsNullOrWhiteSpace(targetSceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(targetSceneName);
    }

    public void LoadExerciseSession()
    {
        CpuAgent.ClearRuntimeDifficultyOverride();
        if (sessionExercises == null || sessionExercises.Length == 0)
        {
            Debug.LogWarning("SceneLoadButton: sessionExercises is empty.");
            return;
        }

        ExerciseSessionManager.StartSession(sessionExercises, shuffleExerciseSession);
        ExerciseSceneLoader.SetRENMode(false);
        ExerciseSceneLoader.SetRuntimeRENMode(RENJudge.RENMode.Easy);

        SRSExercise currentExercise = ExerciseSessionManager.GetCurrentExercise();
        if (currentExercise == null)
            return;

        ExerciseSceneLoader.SetRuntimeSelectedExercise(currentExercise);

        string targetSceneName = useExerciseTargetScene
            ? currentExercise.targetSceneName
            : sceneName;

        if (string.IsNullOrWhiteSpace(targetSceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(targetSceneName);
    }
}
