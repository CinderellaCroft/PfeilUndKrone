using UnityEngine;
using UnityEngine.EventSystems;

public class HexMarker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Hex hex;
    public InteractionManager interaction;

    // Cache original Y to reliably restore after hover/click
    private float? baseY;
    private const float hoverLift = 0.3f;

    void EnsureBaseY()
    {
        if (baseY.HasValue) return;
        baseY = transform.position.y;
    }

    void Lift()
    {
        EnsureBaseY();
        var pos = transform.position;
        transform.position = new Vector3(pos.x, baseY.Value + hoverLift, pos.z);
    }

    void Lower()
    {
        if (!baseY.HasValue) return;
        var pos = transform.position;
        transform.position = new Vector3(pos.x, baseY.Value, pos.z);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (interaction == null) return;
        // Only react if hover is currently enabled (path resource field selection)
        if (interaction.IsHexHoverEnabled())
        {
            // Lower immediately on click (selection happened)
            Lower();
        }
        interaction.OnHexClicked(hex);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (interaction != null && interaction.IsHexHoverEnabled())
        {
            Lift();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (interaction != null && interaction.IsHexHoverEnabled())
        {
            Lower();
        }
    }
}