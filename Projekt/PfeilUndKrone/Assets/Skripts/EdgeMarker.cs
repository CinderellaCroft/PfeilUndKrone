using UnityEngine;
using UnityEngine.EventSystems;

public class EdgeMarker : MonoBehaviour, IPointerClickHandler
{
    public HexEdge edge;
    public InteractionManager interaction;
    public void OnPointerClick(PointerEventData eventData) => interaction.OnEdgeClicked(edge);
}