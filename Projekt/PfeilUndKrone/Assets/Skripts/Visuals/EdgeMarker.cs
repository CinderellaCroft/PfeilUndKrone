using UnityEngine;
using UnityEngine.EventSystems;

public class EdgeMarker : MonoBehaviour, IPointerClickHandler
{
    public HexEdge edge;
    public InteractionManager interaction;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (interaction == null) return;
        if (eventData.button == PointerEventData.InputButton.Right)
            interaction.OnRightClickEdge(edge);
        else
            interaction.OnEdgeClicked(edge);
    }
}