using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 10f;
    public Vector2 panLimitMin = new(-4f, -7f);
    public Vector2 panLimitMax = new(4f, 4f);


    [Header("Zoom Settings")]
    public float scrollSpeed = 20f;
    public float minY = 2f;
    public float maxY = 10f;

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        Vector3 pos = transform.position;

        // WASD Panning
        if (kb.wKey.isPressed) pos.z += panSpeed * Time.deltaTime;
        if (kb.sKey.isPressed) pos.z -= panSpeed * Time.deltaTime;
        if (kb.dKey.isPressed) pos.x += panSpeed * Time.deltaTime;
        if (kb.aKey.isPressed) pos.x -= panSpeed * Time.deltaTime;

        // Middle Mouse Panning
        if (mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();

            pos.x -= delta.x * (panSpeed / 2) * Time.deltaTime;
            pos.z -= delta.y * (panSpeed / 2) * Time.deltaTime;
        }

        // Zoom
        float scroll = mouse.scroll.y.ReadValue();
        pos.y -= scroll * scrollSpeed * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // PanLimit
        pos.x = Mathf.Clamp(pos.x, panLimitMin.x, panLimitMax.x);
        pos.z = Mathf.Clamp(pos.z, panLimitMin.y, panLimitMax.y);

        transform.position = pos;
    }
}
