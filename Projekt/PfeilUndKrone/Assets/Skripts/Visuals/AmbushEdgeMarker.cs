using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Component for invisible edge colliders between neighboring vertices to handle ambush placement with hover preview
/// </summary>
public class AmbushEdgeMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Edge Data")]
    public HexVertex vertexA;
    public HexVertex vertexB;
    
    public void Initialize(HexVertex vA, HexVertex vB)
    {
        vertexA = vA;
        vertexB = vB;
        
        // Set a descriptive name for debugging
        gameObject.name = $"AmbushEdge_{vA.Hex.Q},{vA.Hex.R},{vA.Direction}_to_{vB.Hex.Q},{vB.Hex.R},{vB.Direction}";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnAmbushEdgeHoverEnter(vertexA, vertexB);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnAmbushEdgeHoverExit(vertexA, vertexB);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InteractionManager.Instance != null)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                InteractionManager.Instance.OnAmbushEdgeLeftClick(vertexA, vertexB);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                InteractionManager.Instance.OnAmbushEdgeRightClick(vertexA, vertexB);
            }
        }
    }
}
