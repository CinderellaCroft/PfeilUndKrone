using System.Collections.Generic;
using UnityEngine;
using NetworkingDTOs;
public class NetworkSimulator : SingletonNetworkService<NetworkSimulator>
{
    protected override bool EditorOnly => true;

    [Header("Simulations-Daten")]
    [Tooltip("Wird bei MatchCreated simuliert")]
    [SerializeField]
    private PlayerRole selectedRole = PlayerRole.King;

    public string RoleName => selectedRole.ToString();

    [Tooltip("Wird bei ResourceMap simuliert (q, r und ResourceType)")]
    public List<ResourceData> testResourceMap = new List<ResourceData>
    {
        // Beispiel-Daten:
        new() { q = -3, r = 0, resource = FieldType.Desert },
        new() { q = -3, r = 1, resource = FieldType.Wood },
        new() { q = -3, r = 2, resource = FieldType.Wheat },
        new() { q = -2, r = -1, resource = FieldType.Ore },
        new() { q = 0, r = 0, resource = FieldType.Castle },
        new() { q = -1, r = 0, resource = FieldType.Moat },
        new() { q = -1, r = 1, resource = FieldType.Moat },
        new() { q = 0, r = -1, resource = FieldType.Moat },
        new() { q = 0, r = 1, resource = FieldType.Moat },
        new() { q = 1, r = -1, resource = FieldType.Moat },
        new() { q = 1, r = 0, resource = FieldType.Moat },
    };

    [Tooltip("Wird bei PathApproved simuliert")]
    public List<HexVertex> testApprovedPath = new List<HexVertex>();

    [Tooltip("Wird bei AmbushConfirmed simuliert")]
    public HexEdge testConfirmedAmbush;

    [ContextMenu("Simulate → MatchCreated")]
    void SimulateMatchCreated()
        => RaiseMatchCreated(selectedRole.ToString());

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
