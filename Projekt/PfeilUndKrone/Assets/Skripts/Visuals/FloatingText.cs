
using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    public float moveSpeed = 1f;
    public float fadeOutTime = 1f;
    public float xOffset = 0f;
    public float yOffset = 0f;
    public Vector3 moveDirection = Vector3.up;
    public Color positiveColor = Color.green;
    public Color negativeColor = Color.red;

    private TextMeshProUGUI textMesh;
    private float timer;

    void Awake()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        textMesh.enableWordWrapping = false;
        transform.localPosition = new Vector3(xOffset, yOffset, 0);
    }

    public void SetText(string text, bool isPositive)
    {
        textMesh.text = text;
        textMesh.color = isPositive ? positiveColor : negativeColor;
    }

    void Update()
    {
        transform.localPosition += moveDirection * moveSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= fadeOutTime)
        {
            Destroy(gameObject);
        }
        else
        {
            Color color = textMesh.color;
            color.a = 1f - (timer / fadeOutTime);
            textMesh.color = color;
        }
    }
}
