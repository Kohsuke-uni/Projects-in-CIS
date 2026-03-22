using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class MobileGestureInput : MonoBehaviour
{
    [Header("References")]
    public GameControlUI gameControlUI;
    public RectTransform gestureArea;
    public Slider horizontalThresholdSlider;
    public TMP_Text horizontalThresholdText;
    public Slider softDropThresholdSlider;
    public TMP_Text softDropThresholdText;


    [Header("Ignored UI")]
    [Tooltip("ここに入れたUI領域ではジェスチャーを開始しない")]
    public RectTransform[] ignoredTouchZones;

    [Header("Options")]
    [Tooltip("UIボタンの上から始まったタッチは無視する")]
    public bool ignoreTouchesOverUI = true;

    [Header("Thresholds")]
    [Tooltip("左右にこの距離だけ動くたびに1マス移動する")]
    public float horizontalStepThreshold = 40f;
    [Tooltip("下方向にこの距離を超えたらソフトドロップ開始")]
    public float softDropThreshold = 50f;

    private int activeFingerId = -1;
    private Vector2 startPosition;
    private Vector2 lastStepPosition;
    private bool softDropping;

    private void Awake()
    {
        if (gameControlUI == null)
            gameControlUI = FindObjectOfType<GameControlUI>();

        LoadSavedThresholds();
        UpdateThresholdUI();
    }

    private void Update()
    {
        if (GameControlUI.IsPaused)
        {
            if (activeFingerId >= 0)
                EndGesture();
            return;
        }

        if (Input.touchCount == 0)
        {
            if (activeFingerId >= 0)
                EndGesture();
            return;
        }

        if (activeFingerId < 0)
        {
            TryBeginGesture();
            return;
        }

        Touch touch;
        if (!TryGetActiveTouch(out touch))
        {
            EndGesture();
            return;
        }

        ProcessGesture(touch);
    }

    private void TryBeginGesture()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            if (ignoreTouchesOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            if (!IsWithinGestureArea(touch.position))
                continue;

            if (IsOverIgnoredZone(touch.position))
                continue;

            activeFingerId = touch.fingerId;
            startPosition = touch.position;
            lastStepPosition = touch.position;
            softDropping = false;
            break;
        }
    }

    private void ProcessGesture(Touch touch)
    {
        if (gameControlUI == null)
            return;

        Vector2 dragFromStart = touch.position - startPosition;
        Vector2 dragFromLastStep = touch.position - lastStepPosition;

        while (dragFromLastStep.x >= horizontalStepThreshold)
        {
            gameControlUI.OnMoveRightButton();
            lastStepPosition.x += horizontalStepThreshold;
            dragFromLastStep = touch.position - lastStepPosition;
        }

        while (dragFromLastStep.x <= -horizontalStepThreshold)
        {
            gameControlUI.OnMoveLeftButton();
            lastStepPosition.x -= horizontalStepThreshold;
            dragFromLastStep = touch.position - lastStepPosition;
        }

        if (!softDropping && dragFromStart.y <= -softDropThreshold)
        {
            gameControlUI.OnSoftDropStartButton();
            softDropping = true;
        }
        else if (softDropping && dragFromStart.y > -softDropThreshold)
        {
            gameControlUI.OnSoftDropEndButton();
            softDropping = false;
        }

        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            EndGesture();
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == activeFingerId)
            {
                activeTouch = touch;
                return true;
            }
        }

        activeTouch = default;
        return false;
    }

    private bool IsWithinGestureArea(Vector2 screenPosition)
    {
        if (gestureArea == null)
            return true;

        return RectTransformUtility.RectangleContainsScreenPoint(gestureArea, screenPosition, null);
    }

    private bool IsOverIgnoredZone(Vector2 screenPosition)
    {
        if (ignoredTouchZones == null || ignoredTouchZones.Length == 0)
            return false;

        for (int i = 0; i < ignoredTouchZones.Length; i++)
        {
            RectTransform zone = ignoredTouchZones[i];
            if (zone == null)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(zone, screenPosition, null))
                return true;
        }

        return false;
    }

    private void EndGesture()
    {
        if (softDropping && gameControlUI != null)
            gameControlUI.OnSoftDropEndButton();

        if (gameControlUI != null)
            gameControlUI.OnStopHorizontalButton();

        activeFingerId = -1;
        softDropping = false;
    }

    public void SetHorizontalStepThreshold(float value)
    {
        horizontalStepThreshold = Mathf.Max(1f, value);
        SaveManager.SetHorizontalStepThreshold(horizontalStepThreshold);
        UpdateThresholdUI();
    }

    public void SetSoftDropThreshold(float value)
    {
        softDropThreshold = Mathf.Max(1f, value);
        SaveManager.SetSoftDropThreshold(softDropThreshold);
        UpdateThresholdUI();
    }

    private void LoadSavedThresholds()
    {
        horizontalStepThreshold = SaveManager.GetHorizontalStepThreshold(horizontalStepThreshold);
        softDropThreshold = SaveManager.GetSoftDropThreshold(softDropThreshold);
    }

    private void UpdateThresholdUI()
    {
        if (horizontalThresholdSlider != null)
            horizontalThresholdSlider.SetValueWithoutNotify(horizontalStepThreshold);

        if (horizontalThresholdText != null)
            horizontalThresholdText.text = horizontalStepThreshold.ToString("F0");

        if (softDropThresholdSlider != null)
            softDropThresholdSlider.SetValueWithoutNotify(softDropThreshold);

        if (softDropThresholdText != null)
            softDropThresholdText.text = softDropThreshold.ToString("F0");
    }

}
