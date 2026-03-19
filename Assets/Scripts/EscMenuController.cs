using UnityEngine;
using UnityEngine.SceneManagement;

public class EscMenuController : MonoBehaviour
{
    [Header("UI Root")]
    [Tooltip("ESCメニューのパネル（Canvas配下のPanel）")]
    public GameObject escMenuPanel;

    [Tooltip("操作方法パネル（Canvas配下のPanel）")]
    public GameObject howToPlayPanel;   // ★ 新しく追加！

    [Tooltip("PC向け操作方法パネル（任意）")]
    public GameObject desktopHowToPlayPanel;

    [Tooltip("モバイル向け操作方法パネル（任意）")]
    public GameObject mobileHowToPlayPanel;

    [Header("Scene Names")]
    [Tooltip("テクニック選択画面のシーン名")]
    public string stageSelectSceneName = "StageSelect";

    [Tooltip("タイトル画面のシーン名")]
    public string titleSceneName = "Title";

    private bool isMenuOpen = false;       // ESCメニューが開いているか
    private bool wasHowToPanelActive = false;

    private void Start()
    {
        if (escMenuPanel != null)
            escMenuPanel.SetActive(false);

        SetHowToPlayPanelsActive(false);

        wasHowToPanelActive = IsAnyHowToPlayPanelActive();
    }

    private void Update()
    {
        bool isHowToPanelActive = IsAnyHowToPlayPanelActive();
        if (isHowToPanelActive && !wasHowToPanelActive)
        {
            SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        }
        wasHowToPanelActive = isHowToPanelActive;

        // ESCキーでメニューの開閉
        //コントローラーだとL1/R1でESCメニュー開閉
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.JoystickButton5))
        {
            SoundManager.Instance?.PlaySE(SeType.ButtonClick);

            // ★ 操作説明表示中なら → 操作説明だけ閉じる
            if (IsAnyHowToPlayPanelActive())
            {
                CloseHowToPlay();
                return;
            }

            // ★ 通常のESCメニュー開閉
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (escMenuPanel != null)
            escMenuPanel.SetActive(isMenuOpen);

        Time.timeScale = isMenuOpen ? 0f : 1f;
    }

    // 操作説明パネルを閉じる
    public void CloseHowToPlay()
    {
        SetHowToPlayPanelsActive(false);

        // メニューを開いたままに戻す
        if (escMenuPanel != null)
            escMenuPanel.SetActive(true);

        isMenuOpen = true;
    }

    public void OpenHowToPlay()
    {
        SetHowToPlayPanelsActive(true);
        isMenuOpen = true;
    }

    // =========================================================
    //  既存のボタン機能
    // =========================================================
    public void OnStageSelectButton()
    {
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(stageSelectSceneName)) return;
        SceneManager.LoadScene(stageSelectSceneName);
    }

    public void OnTitleButton()
    {
        SoundManager.Instance?.PlaySE(SeType.ButtonClick);

        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(titleSceneName)) return;
        SceneManager.LoadScene(titleSceneName);
    }

    public void OnCloseButton()
    {
        if (!isMenuOpen) return;

        SoundManager.Instance?.PlaySE(SeType.ButtonClick);
        ToggleMenu();
    }

    private bool IsAnyHowToPlayPanelActive()
    {
        return (desktopHowToPlayPanel != null && desktopHowToPlayPanel.activeInHierarchy)
            || (mobileHowToPlayPanel != null && mobileHowToPlayPanel.activeInHierarchy);
    }

    private void SetHowToPlayPanelsActive(bool active)
    {
        if (desktopHowToPlayPanel == null && mobileHowToPlayPanel == null)
        {
            if (howToPlayPanel != null)
                howToPlayPanel.SetActive(active);
            return;
        }

        bool showMobile = Application.isMobilePlatform;

        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(active);

        if (desktopHowToPlayPanel != null)
            desktopHowToPlayPanel.SetActive(active && !showMobile);

        if (mobileHowToPlayPanel != null)
            mobileHowToPlayPanel.SetActive(active && showMobile);
    }
}
