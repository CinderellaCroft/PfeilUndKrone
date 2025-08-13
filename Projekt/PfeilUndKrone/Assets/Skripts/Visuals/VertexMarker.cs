using UnityEngine;
using UnityEngine.EventSystems;

public class VertexMarker : MonoBehaviour, IPointerClickHandler
{
    public HexVertex vertex;
    public InteractionManager interaction;
    public void OnPointerClick(PointerEventData eventData) => interaction.OnVertexClicked(vertex);
}
