using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Component for ambush orb GameObjects to detect right-clicks and forward them to InteractionManager
/// </summary>
public class AmbushOrbMarker : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Forward right-click to InteractionManager
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.OnRightClickAmbushOrb(this.gameObject);
            }
        }
        // Left-clicks are ignored - ambushes are only placed via vertex clicks
    }
}
