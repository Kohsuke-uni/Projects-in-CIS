using UnityEngine;

public class PlatformVisibility : MonoBehaviour
{
    [Header("Targets")]
    public GameObject mobileOnlyRoot;
    public GameObject desktopOnlyRoot;

    [Header("Options")]
    public bool showMobileInEditor = false;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        bool showMobile = Application.isMobilePlatform;

#if UNITY_EDITOR
        if (showMobileInEditor)
            showMobile = true;
#endif

        if (mobileOnlyRoot != null)
            mobileOnlyRoot.SetActive(showMobile);

        if (desktopOnlyRoot != null)
            desktopOnlyRoot.SetActive(!showMobile);
    }
}
