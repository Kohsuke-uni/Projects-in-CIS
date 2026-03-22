using UnityEngine;

public class SettingsSensitivityPreview : MonoBehaviour
{
    [Header("References")]
    public RectTransform pieceRoot;
    public RectTransform defaultPieceRoot;
    public RectTransform classicPieceRoot;
    public RectTransform gestureArea;

    [Header("Motion")]
    public bool usePieceWidthForHorizontalStep = true;
    [Tooltip("1なら画像幅そのまま、0.25なら画像幅の1/4だけ移動")]
    public float horizontalStepWidthFactor = 0.25f;
    public float horizontalStepSize = 30f;
    public bool usePieceHeightForSoftDropStep = true;
    [Tooltip("1なら画像高さそのまま、0.25なら画像高さの1/4だけ移動")]
    public float softDropHeightFactor = 0.25f;
    public float softDropStepSize = 30f;
    public float returnSpeed = 12f;

    private Vector2 homePosition;
    private Vector2 dragStartPosition;
    private float lastStepX;
    private float lastStepY;
    private bool dragging;
    private int activeFingerId = -1;
    private bool lastUseClassicMinos;

    private void Awake()
    {
        ApplyAppearanceSelection(force: true);
    }

    private void Update()
    {
        ApplyAppearanceSelection(force: false);
        UpdateInput();

        if (!dragging && pieceRoot != null)
        {
            pieceRoot.anchoredPosition = Vector2.Lerp(
                pieceRoot.anchoredPosition,
                homePosition,
                1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));
        }
    }

    private void UpdateInput()
    {
        if (pieceRoot == null)
            return;

        if (Input.touchCount > 0)
        {
            UpdateTouchInput();
            return;
        }

        activeFingerId = -1;

        if (Input.GetMouseButtonDown(0) && IsInsideGestureArea(Input.mousePosition))
            BeginGesture(Input.mousePosition);

        if (dragging && Input.GetMouseButton(0))
            ProcessDrag(Input.mousePosition);

        if (dragging && Input.GetMouseButtonUp(0))
            dragging = false;
    }

    private void UpdateTouchInput()
    {
        if (activeFingerId < 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                if (!IsInsideGestureArea(touch.position))
                    continue;

                activeFingerId = touch.fingerId;
                BeginGesture(touch.position);
                return;
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activeFingerId)
                continue;

            ProcessDrag(touch.position);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                dragging = false;
                activeFingerId = -1;
            }
            return;
        }

        dragging = false;
        activeFingerId = -1;
    }

    private void BeginGesture(Vector2 screenPosition)
    {
        dragging = true;
        dragStartPosition = screenPosition;
        lastStepX = 0f;
        lastStepY = 0f;
    }

    private void ApplyAppearanceSelection(bool force)
    {
        bool useClassicMinos = SaveManager.GetUseClassicMinos();
        if (!force && useClassicMinos == lastUseClassicMinos)
            return;

        lastUseClassicMinos = useClassicMinos;

        if (defaultPieceRoot != null)
            defaultPieceRoot.gameObject.SetActive(!useClassicMinos);

        if (classicPieceRoot != null)
            classicPieceRoot.gameObject.SetActive(useClassicMinos);

        RectTransform selectedPiece = useClassicMinos && classicPieceRoot != null
            ? classicPieceRoot
            : defaultPieceRoot;

        if (selectedPiece == null)
            selectedPiece = pieceRoot;

        pieceRoot = selectedPiece;

        if (pieceRoot != null)
        {
            homePosition = pieceRoot.anchoredPosition;

            if (!dragging)
                pieceRoot.anchoredPosition = homePosition;
        }
    }

    private void ProcessDrag(Vector2 screenPosition)
    {
        if (!dragging || pieceRoot == null)
            return;

        float horizontalThreshold = SaveManager.GetHorizontalStepThreshold(40f);
        float softDropThreshold = SaveManager.GetSoftDropThreshold(50f);

        Vector2 drag = screenPosition - dragStartPosition;

        while (drag.x - lastStepX >= horizontalThreshold)
        {
            lastStepX += horizontalThreshold;
            pieceRoot.anchoredPosition += Vector2.right * GetHorizontalStepSize();
            SoundManager.Instance?.PlaySE(SeType.Move);
        }

        while (drag.x - lastStepX <= -horizontalThreshold)
        {
            lastStepX -= horizontalThreshold;
            pieceRoot.anchoredPosition += Vector2.left * GetHorizontalStepSize();
            SoundManager.Instance?.PlaySE(SeType.Move);
        }

        while (-drag.y - lastStepY >= softDropThreshold)
        {
            lastStepY += softDropThreshold;
            pieceRoot.anchoredPosition += Vector2.down * GetSoftDropStepSize();
            SoundManager.Instance?.PlaySE(SeType.Move);
        }

        while (-drag.y - lastStepY <= -softDropThreshold && lastStepY > 0f)
        {
            lastStepY -= softDropThreshold;
            pieceRoot.anchoredPosition += Vector2.up * GetSoftDropStepSize();
            SoundManager.Instance?.PlaySE(SeType.Move);
        }

        Vector2 targetPosition = pieceRoot.anchoredPosition;
        targetPosition.y = homePosition.y - (GetSoftDropStepSize() * GetSoftDropStepCount());
        pieceRoot.anchoredPosition = targetPosition;
    }

    private bool IsInsideGestureArea(Vector2 screenPosition)
    {
        if (gestureArea == null)
            return true;

        Canvas canvas = gestureArea.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(gestureArea, screenPosition, eventCamera);
    }

    private float GetHorizontalStepSize()
    {
        if (!usePieceWidthForHorizontalStep || pieceRoot == null)
            return horizontalStepSize;

        return pieceRoot.rect.width * pieceRoot.localScale.x * horizontalStepWidthFactor;
    }

    private float GetSoftDropStepSize()
    {
        if (!usePieceHeightForSoftDropStep || pieceRoot == null)
            return softDropStepSize;

        return pieceRoot.rect.height * pieceRoot.localScale.y * softDropHeightFactor;
    }

    private int GetSoftDropStepCount()
    {
        if (lastStepY <= 0f)
            return 0;

        float softDropThreshold = SaveManager.GetSoftDropThreshold(50f);
        return Mathf.RoundToInt(lastStepY / softDropThreshold);
    }
}
