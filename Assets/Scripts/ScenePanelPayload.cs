using System.Collections.Generic;

public static class ScenePanelPayload
{
    private static readonly List<string> orderedPanelIds = new List<string>();
    private static readonly HashSet<string> panelIdSet = new HashSet<string>();
    private static bool hasPayload = false;

    public static void SetPanels(IEnumerable<string> ids)
    {
        orderedPanelIds.Clear();
        panelIdSet.Clear();
        hasPayload = false;

        if (ids == null) return;

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            string trimmed = id.Trim();
            if (panelIdSet.Contains(trimmed)) continue;

            panelIdSet.Add(trimmed);
            orderedPanelIds.Add(trimmed);
            hasPayload = true;
        }
    }

    public static bool HasPayload() => hasPayload;

    public static bool Contains(string panelId)
    {
        return hasPayload && !string.IsNullOrWhiteSpace(panelId) && panelIdSet.Contains(panelId);
    }

    public static int Count()
    {
        return hasPayload ? orderedPanelIds.Count : 0;
    }

    public static string GetAt(int index)
    {
        if (!hasPayload) return null;
        if (index < 0 || index >= orderedPanelIds.Count) return null;
        return orderedPanelIds[index];
    }

    public static void Clear()
    {
        orderedPanelIds.Clear();
        panelIdSet.Clear();
        hasPayload = false;
    }
}
