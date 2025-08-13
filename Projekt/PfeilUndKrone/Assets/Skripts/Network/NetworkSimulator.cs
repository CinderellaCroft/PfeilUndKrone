using System.Collections.Generic;
using UnityEngine;
using NetworkingDTOs;
public class NetworkSimulator : SingletonNetworkService<NetworkSimulator>
{
    protected override bool EditorOnly => true;

    [Header("Simulations-Daten")]
    [Tooltip("Wird bei ResourceMap simuliert (q, r und ResourceType)")]
    public List<ResourceData> testResourceMap = new List<ResourceData>
    {
        // Beispiel-Daten:
        new ResourceData { q = -3, r = 0, resource = ResourceType.Desert },
        new ResourceData { q = -3, r = 1, resource = ResourceType.Wood },
        new ResourceData { q = -3, r = 2, resource = ResourceType.Wheat },
    };

    [Tooltip("Wird bei PathApproved simuliert")]
    public List<HexVertex> testApprovedPath = new List<HexVertex>();

    [Tooltip("Wird bei AmbushConfirmed simuliert")]
    public HexEdge testConfirmedAmbush;

    [ContextMenu("Simulate → GridReady")]
    void SimulateGridReady()
        => RaiseGridDataReady();

    [ContextMenu("Simulate → ResourceMap")]
    void SimulateResourceMap()
        => RaiseResourceMapReceived(testResourceMap);

    [ContextMenu("Simulate → AmbushConfirmed")]
    void SimulateAmbushConfirmed()
        => RaiseAmbushConfirmed(testConfirmedAmbush);

    public override void Send(string type, object payload)
        => Debug.Log($"[Simulator Send] {type} → {JsonUtility.ToJson(payload)}");
}
