using System.Collections.Generic;
using UnityEngine;

// A simple struct to represent an edge between two corners
[System.Serializable]
public struct AmbushEdge
{
    public Vector3 posA;
    public Vector3 posB;
}

// A wrapper class for sending a list of ambushes
[System.Serializable]
public class AmbushData
{
    public List<AmbushEdge> ambushes;
}

public class AmbushManager : MonoBehaviour
{
    private static AmbushManager _instance;
    public static AmbushManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<AmbushManager>();
            return _instance;
        }
    }

    private bool _isPlacementEnabled = false;
    private CornerNode _firstCornerSelected = null;
    private List<AmbushEdge> _placedAmbushes = new List<AmbushEdge>();
    private const int MAX_AMBUSHES = 5;
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void EnableAmbushPlacement()
    {
        _isPlacementEnabled = true;
        _placedAmbushes.Clear();
        _firstCornerSelected = null;
        Debug.Log("Ambush placement enabled.");
    }

    public void DisableAmbushPlacement()
    {
        _isPlacementEnabled = false;
    }

    public void OnCornerClicked(CornerNode node)
    {
        if (!_isPlacementEnabled) return;

        if (_placedAmbushes.Count >= MAX_AMBUSHES)
        {
            Debug.LogWarning("Maximum number of ambushes placed.");
            UIManager.Instance.UpdateInfoText("Max ambushes placed. Click 'Done'.");
            return;
        }

        if (_firstCornerSelected == null)
        {
            _firstCornerSelected = node;
            Debug.Log($"Selected first corner for ambush at {node.position}");
            UIManager.Instance.UpdateInfoText("Select an adjacent corner to place ambush.");
        }
        else
        {
            if (_firstCornerSelected.neighbors.Contains(node))
            {
                AmbushEdge newAmbush = new AmbushEdge { posA = _firstCornerSelected.position, posB = node.position };
                NetworkManager.Instance.Send("buy_ambush", newAmbush);
            }
            else
            {
                Debug.LogWarning("Invalid ambush placement: Corners are not adjacent.");
                UIManager.Instance.UpdateInfoText("Error: Corners must be adjacent.");
            }
            _firstCornerSelected = null;
        }
    }

    public void ConfirmAmbushPlacement(AmbushEdge edge)
    {
        _placedAmbushes.Add(edge);
        Debug.Log($"Ambush confirmed between {edge.posA} and {edge.posB}. Total: {_placedAmbushes.Count}");
        UIManager.Instance.UpdateInfoText($"Ambush placed! ({_placedAmbushes.Count}/{MAX_AMBUSHES})");
    }

    public void FinalizeAmbushes()
    {
        if (!_isPlacementEnabled) return;

        AmbushData dataToSend = new AmbushData { ambushes = _placedAmbushes };
        NetworkManager.Instance.Send("place_ambushes", dataToSend);
        Debug.Log("Final ambush positions submitted to server.");
        DisableAmbushPlacement();
        UIManager.Instance.SetDoneButtonActive(false);
    }

    void Update()
    {
        if (_isPlacementEnabled)
        {
            foreach (var ambush in _placedAmbushes)
            {
                Debug.DrawLine(ambush.posA, ambush.posB, Color.red);
            }
        }
    }
}