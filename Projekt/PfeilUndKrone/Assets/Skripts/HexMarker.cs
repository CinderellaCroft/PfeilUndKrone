using UnityEngine;
using UnityEngine.EventSystems;

public class HexMarker : MonoBehaviour, IPointerClickHandler
{
    public Hex hex;
    public InteractionManager interaction;
    public void OnPointerClick(PointerEventData eventData) => interaction.OnHexClicked(hex);
}