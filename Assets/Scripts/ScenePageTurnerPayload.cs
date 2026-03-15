using UnityEngine;

public static class ScenePageTurnerPayload
{
    private static string pagesObjectName;
    private static GameObject pagesObjectRef;
    private static bool hasPayload = false;

    public static void SetPagesObjectName(string objectName)
    {
        pagesObjectName = string.IsNullOrWhiteSpace(objectName) ? string.Empty : objectName.Trim();
        pagesObjectRef = null;
        hasPayload = !string.IsNullOrEmpty(pagesObjectName);
    }

    public static void SetPagesObject(GameObject pagesObject)
    {
        pagesObjectRef = pagesObject;
        pagesObjectName = pagesObject != null ? pagesObject.name : string.Empty;
        hasPayload = pagesObjectRef != null || !string.IsNullOrEmpty(pagesObjectName);
    }

    public static bool HasPayload() => hasPayload;

    public static string GetPagesObjectName()
    {
        return hasPayload ? pagesObjectName : string.Empty;
    }

    public static bool TryGetPagesObject(out GameObject pagesObject)
    {
        pagesObject = pagesObjectRef;
        return hasPayload && pagesObjectRef != null;
    }

    public static void Clear()
    {
        pagesObjectName = string.Empty;
        pagesObjectRef = null;
        hasPayload = false;
    }
}
