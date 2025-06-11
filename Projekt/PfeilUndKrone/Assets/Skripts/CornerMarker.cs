using UnityEngine;
using UnityEngine.EventSystems;

public class CornerMarker : MonoBehaviour, IPointerClickHandler
{
    public CornerNode node;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (node == null)
        {
            Debug.LogWarning("[CornerMarker] No CornerNode assigned!");
            return;
        }

        // Delegate the click to the central game manager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnCornerClicked(node);
        }
        else
        {
            Debug.LogWarning("[CornerMarker] No GameManager instance found!");
        }
    }
}