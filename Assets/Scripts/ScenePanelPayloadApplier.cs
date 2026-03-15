using UnityEngine;

public class ScenePanelPayloadApplier : MonoBehaviour
{
    [System.Serializable]
    public struct PanelBinding
    {
        public string panelId;
        public GameObject panelObject;
    }

    [Header("Scene Panels")]
    public PanelBinding[] panels;
    public bool disableAllWhenNoPayload = false;
    public bool reorderByPayloadOrder = true;
    public bool clearPayloadAfterApply = true;

    private void Start()
    {
        bool hasPayload = ScenePanelPayload.HasPayload();

        for (int i = 0; i < panels.Length; i++)
        {
            var go = panels[i].panelObject;
            if (go == null) continue;

            if (!hasPayload && !disableAllWhenNoPayload)
                continue;

            bool active = hasPayload && ScenePanelPayload.Contains(panels[i].panelId);
            go.SetActive(active);
        }

        if (hasPayload && reorderByPayloadOrder)
        {
            ApplyHierarchyOrder();
        }

        if (clearPayloadAfterApply)
        {
            ScenePanelPayload.Clear();
        }
    }

    private void ApplyHierarchyOrder()
    {
        int payloadCount = ScenePanelPayload.Count();
        for (int i = 0; i < payloadCount; i++)
        {
            string id = ScenePanelPayload.GetAt(i);
            if (string.IsNullOrWhiteSpace(id)) continue;

            for (int j = 0; j < panels.Length; j++)
            {
                if (panels[j].panelObject == null) continue;
                if (panels[j].panelId != id) continue;

                // Move matched panel to the end in payload order.
                // Final sibling order among matched panels becomes: payload[0], payload[1], ...
                panels[j].panelObject.transform.SetAsLastSibling();
                break;
            }
        }
    }
}
