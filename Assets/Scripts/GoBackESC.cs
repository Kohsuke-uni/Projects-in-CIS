using UnityEngine;
using UnityEngine.SceneManagement;

public class GoBackESC : MonoBehaviour
{
    [Header("ESC で戻るシーン名")]
    [SerializeField] private string targetSceneName = "";

    private void Update()
    {
        // ESC判定（旧 InputSystem で動作）
        //コントローラーだとL1/R1でESCメニュー開閉
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton8) || Input.GetKeyDown(KeyCode.JoystickButton9))
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                Debug.Log("[EscToScene] ESC pressed → Load: " + targetSceneName);
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("[EscToScene] targetSceneName が設定されていません！");
            }
        }
    }
}
