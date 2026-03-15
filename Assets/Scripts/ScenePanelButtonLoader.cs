using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePanelButtonLoader : MonoBehaviour
{
    [Header("PageTurner Pages Object")]
    public GameObject pagesObject;

    public void LoadSceneWithPagesObject()
    {
        string pagesName = pagesObject.name;
        if (pagesObject != null)
        {
            GameObject pagesCopy = Instantiate(pagesObject);
            pagesCopy.name = string.IsNullOrWhiteSpace(pagesName) ? pagesObject.name : pagesName;
            DontDestroyOnLoad(pagesCopy);
            ScenePageTurnerPayload.SetPagesObject(pagesCopy);
        }
        else
        {
            ScenePageTurnerPayload.SetPagesObjectName(pagesName);
        }

        SceneManager.LoadScene("About");
    }
}
