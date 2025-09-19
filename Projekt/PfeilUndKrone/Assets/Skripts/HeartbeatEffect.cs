using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HeartbeatEffect : MonoBehaviour
{
    [Header("Pulse Settings")]
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float minSize = 0.9f;
    [SerializeField] private float maxSize = 1.1f;

    private RectTransform rectTransform;
    private Vector3 initialScale;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        initialScale = rectTransform.localScale;
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * pulseSpeed);
        float normalizedPulse = (pulse + 1f) / 2f;
        float scaleMultiplier = Mathf.Lerp(minSize, maxSize, normalizedPulse);
        rectTransform.localScale = initialScale * scaleMultiplier;
    }
}