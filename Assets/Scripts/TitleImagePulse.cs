using UnityEngine;

public class TitleImagePulse : MonoBehaviour
{
    [Header("Animation")]
    public float pulseAmount = 0.05f;
    public float pulseSpeed = 1.5f;
    public bool useUnscaledTime = true;

    Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float timeValue = useUnscaledTime ? Time.unscaledTime : Time.time;
        float scaleOffset = Mathf.Sin(timeValue * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        transform.localScale = baseScale * (1f + scaleOffset);
    }
}
