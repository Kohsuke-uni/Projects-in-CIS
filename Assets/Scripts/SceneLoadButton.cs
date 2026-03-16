using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadButton : MonoBehaviour
{
    [Header("Scene Load")]
    public string sceneName;

    [Header("Exercise Load (Optional)")]
    public SRSExercise targetExercise;
    public bool useExerciseTargetScene = true;

    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(sceneName);
    }

    public void LoadExerciseScene()
    {
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
}
