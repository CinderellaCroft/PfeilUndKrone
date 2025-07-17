using System.Collections.Generic;
using UnityEngine;

// Nun mit CornerCoords statt Vector3
[System.Serializable]
public struct AmbushEdge
{
    public CornerCoord cornerA;
    public CornerCoord cornerB;
}

[System.Serializable]
public class AmbushData
{
    public List<AmbushEdge> ambushes;
}

public class AmbushManager : MonoBehaviour
{
    public static AmbushManager Instance;

    private bool _isPlacementEnabled = false;
    private CornerNode _firstCorner = null;
    private List<AmbushEdge> _placed = new List<AmbushEdge>();
    private const int MAX_AMBUSHES = 5;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void EnableAmbushPlacement()
    {
        _isPlacementEnabled = true;
        _placed.Clear();
        _firstCorner = null;
    }

    public void DisableAmbushPlacement()
    {
        _isPlacementEnabled = false;
    }

    // Klick auf Ecke
    public void OnCornerClicked(CornerNode node)
    {
        if (!_isPlacementEnabled) return;

        if (_placed.Count >= MAX_AMBUSHES)
        {
            Debug.LogWarning("Max ambushes reached.");
            return;
        }

        if (_firstCorner == null)
        {
            _firstCorner = node;
            Debug.Log($"Ambush Start-Ecke: {node.ToCoord()}");
        }
        else
        {
            // nur benachbarte Ecken erlaubt
            if (_firstCorner.neighbors.Contains(node))
            {
                var edge = new AmbushEdge
                {
                    cornerA = _firstCorner.ToCoord(),
                    cornerB = node.ToCoord()
                };
                NetworkManager.Instance.Send("buy_ambush", edge);
            }
            else
            {
                Debug.LogWarning("Corners not adjacent for ambush.");
            }
            _firstCorner = null;
        }
    }

    // Server bestätigt einzelne Platzierung
    public void ConfirmAmbushPlacement(AmbushEdge edge)
    {
        _placed.Add(edge);
        Debug.Log($"Ambush bestätigt: {edge.cornerA} ↔ {edge.cornerB}");
    }

    // Button "Done" → alle Ambushes ans Backend
    public void FinalizeAmbushes()
    {
        if (!_isPlacementEnabled) return;
        var data = new AmbushData { ambushes = _placed };
        NetworkManager.Instance.Send("place_ambushes", data);
        DisableAmbushPlacement();
    }

    void Update()
    {
        if (_isPlacementEnabled)
        {
            // Visualisiere aktuelle Ambushes
            foreach (var e in _placed)
            {
                Vector3 a = CoordToWorld(e.cornerA);
                Vector3 b = CoordToWorld(e.cornerB);
                Debug.DrawLine(a, b, Color.red);
            }
        }
    }

    // Hilfsfunktion: CornerCoord → Weltposition
    private Vector3 CoordToWorld(CornerCoord c)
    {
        float radius = HexGridGenerator.Instance.hexRadius;
        Vector3 center = HexGridGenerator.Instance.HexToWorld(c.q, c.r, radius);
        Vector3[] corners = HexGridGenerator.Instance.GetHexCorners(center, radius);
        return corners[c.i];
    }
}
