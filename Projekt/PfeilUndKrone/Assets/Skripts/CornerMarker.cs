using UnityEngine;
using UnityEngine.EventSystems;

public class CornerMarker : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public CornerNode node;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (node == null)
        {
            Debug.LogWarning("[CornerMarker] Kein CornerNode zugewiesen!");
            return;
        }

        Debug.Log($"[CornerMarker] Klick auf Ecke {node.position}");

        if (CornerPathManager.Instance != null)
            CornerPathManager.Instance.OnCornerClicked(node);
        else
            Debug.LogWarning("[CornerMarker] Kein CornerPathManager vorhanden!");
    }


    public void OnPointerEnter(PointerEventData eventData)
    {

    }

    public void OnPointerExit(PointerEventData eventData)
    {

    }
}
