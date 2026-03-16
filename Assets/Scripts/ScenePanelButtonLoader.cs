using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePanelButtonLoader : MonoBehaviour
{
    public string sceneName = "About";

    [Header("PageTurner Pages Object")]
    public GameObject pagesObject;

    public void LoadSceneWithPagesObject()
    {
        string pagesName = pagesObject != null ? pagesObject.name : string.Empty;
        if (pagesObject != null)
        {
            GameObject pagesCopy = Instantiate(pagesObject);
            pagesCopy.name = string.IsNullOrWhiteSpace(pagesName) ? pagesObject.name : pagesName;
            DontDestroyOnLoad(pagesCopy);
            ScenePageTurnerPayload.SetPagesObject(pagesCopy);
        }
        else
        {
            ScenePageTurnerPayload.Clear();
        }

        SceneManager.LoadScene(sceneName);
    }
}
