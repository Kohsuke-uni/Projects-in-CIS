using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadButton : MonoBehaviour
{
    public string sceneName;

    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        SceneManager.LoadScene(sceneName);
    }
}
