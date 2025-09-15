using UnityEngine;
using UnityEngine.UI;

// Dieses Skript an ein UI-Element mit einer RectTransform-Komponente anhängen.
[RequireComponent(typeof(RectTransform))]
public class HeartbeatEffect : MonoBehaviour
{
    [Header("Pulse Settings")]
    [Tooltip("Die Geschwindigkeit des Herzschlags.")]
    [SerializeField]
    private float pulseSpeed = 1.5f;

    [Tooltip("Die minimale Größe während des Pulsierens (1 = 100% Originalgröße).")]
    [SerializeField]
    private float minSize = 0.9f;

    [Tooltip("Die maximale Größe während des Pulsierens (1 = 100% Originalgröße).")]
    [SerializeField]
    private float maxSize = 1.1f;

    private RectTransform rectTransform;
    private Vector3 initialScale;

    // Start wird vor dem ersten Frame-Update aufgerufen
    void Start()
    {
        // Die RectTransform-Komponente dieses UI-Elements abrufen
        rectTransform = GetComponent<RectTransform>();
        // Die ursprüngliche Größe speichern, um darauf basierend zu skalieren
        initialScale = rectTransform.localScale;
    }

    // Update wird einmal pro Frame aufgerufen
    void Update()
    {
        // Eine Sinuswelle erzeugen, die sich mit der Zeit bewegt.
        // Mathf.Sin gibt Werte zwischen -1 und 1 zurück.
        float pulse = Mathf.Sin(Time.time * pulseSpeed);

        // Den Sinuswert von (-1 bis 1) auf einen Bereich von (0 bis 1) umwandeln.
        float normalizedPulse = (pulse + 1f) / 2f;

        // Den normalisierten Wert verwenden, um die Skalierung zwischen minSize und maxSize zu interpolieren.
        // Mathf.Lerp berechnet einen Wert zwischen zwei Punkten basierend auf einem dritten Wert (hier normalizedPulse).
        float scaleMultiplier = Mathf.Lerp(minSize, maxSize, normalizedPulse);

        // Die neue Größe auf das UI-Element anwenden, basierend auf seiner ursprünglichen Größe.
        rectTransform.localScale = initialScale * scaleMultiplier;
    }
}