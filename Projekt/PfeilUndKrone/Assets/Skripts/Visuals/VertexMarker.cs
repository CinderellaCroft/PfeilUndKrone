using UnityEngine;
using UnityEngine.EventSystems;

public class VertexMarker : MonoBehaviour, IPointerClickHandler
{
    public HexVertex vertex;
    public InteractionManager interaction;
    public void OnPointerClick(PointerEventData eventData)
    {
        if (interaction == null) return;
        if (eventData.button == PointerEventData.InputButton.Right)
            interaction.OnRightClickVertex(vertex);
        else
            interaction.OnVertexClicked(vertex);
    }
}
