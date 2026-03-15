using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePageTurnerPayloadApplier : MonoBehaviour
{
    [Header("Target")]
    public PageTurner pageTurner;
    public Transform fallbackPagesParent;
    public string introPanelObjectName = "Intro Panel";
    public bool clearPayloadAfterApply = true;

    private void Start()
    {
        if (pageTurner == null)
        {
            Debug.LogWarning("[ScenePageTurnerPayloadApplier] pageTurner is not assigned.");
            return;
        }

        Transform targetPagesParent = ResolvePagesParent();
        if (targetPagesParent != null)
        {
            TryParentPagesUnderIntroPanel(targetPagesParent);
            pageTurner.SetPagesParent(targetPagesParent);
        }
        else if (fallbackPagesParent != null)
        {
            pageTurner.SetPagesParent(fallbackPagesParent);
        }

        if (clearPayloadAfterApply)
        {
            ScenePageTurnerPayload.Clear();
        }
    }

    private Transform ResolvePagesParent()
    {
        if (!ScenePageTurnerPayload.HasPayload()) return null;

        if (ScenePageTurnerPayload.TryGetPagesObject(out GameObject pagesObjectRef) &&
            pagesObjectRef != null)
        {
            return pagesObjectRef.transform;
        }

        string objectName = ScenePageTurnerPayload.GetPagesObjectName();
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        GameObject pagesObject = GameObject.Find(objectName);
        if (pagesObject == null)
        {
            Debug.LogWarning($"[ScenePageTurnerPayloadApplier] Could not find pages object: {objectName}");
            return null;
        }

        return pagesObject.transform;
    }

    private void TryParentPagesUnderIntroPanel(Transform pagesTransform)
    {
        if (pagesTransform == null) return;
        if (string.IsNullOrWhiteSpace(introPanelObjectName)) return;

        GameObject introPanel = GameObject.Find(introPanelObjectName);
        if (introPanel == null)
        {
            Debug.LogWarning($"[ScenePageTurnerPayloadApplier] Could not find intro panel: {introPanelObjectName}");
            return;
        }

        if (pagesTransform.gameObject.scene != introPanel.scene)
        {
            SceneManager.MoveGameObjectToScene(pagesTransform.gameObject, introPanel.scene);
        }

        pagesTransform.SetParent(introPanel.transform, false);
        pagesTransform.SetSiblingIndex(0);
    }
}
