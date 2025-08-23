using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NetworkingDTOs;

public enum InteractionMode { None, PathSelection, AmbushPlacement }

public class InteractionManager : Singleton<InteractionManager>
{
    public HexGridGenerator gridGen;
    public NetworkServiceBase net;

    public GameObject workerPrefab;
    public float workerSpeed = 1f;

    public Material ambushLineMaterial;
    public int maxAmbushes = 5;

    private InteractionMode currentMode = InteractionMode.None;

    private HashSet<HexVertex> selectedVertices = new();
    private HashSet<HexVertex> validNextVertices = new();
    private List<HexVertex> centralVertices;
    private bool pathComplete;
    private GameObject workerObj;
    private List<Vector3> serverPathWorld = new();
    private int pathStep;
    private bool isMoving;

    private HexVertex ambushStart;
    private HashSet<HexEdge> ambushEdges = new();

    protected override void Awake()
    {
        base.Awake();
        workerObj = Instantiate(workerPrefab); workerObj.SetActive(false);
        //net.OnPathApproved += ExecuteServerPath;
        //net.OnAmbushConfirmed += ConfirmAmbushPlacement;
    }

    void Start()
    {
        centralVertices = Enum.GetValues(typeof(VertexDirection))
            .Cast<VertexDirection>()
            .Select(d => new HexVertex(new Hex(0, 0), d))
            .ToList();
    }

    public void EnableInteraction(PlayerRole role)
    {
        Debug.Log($"INTERACTIONMANAGER: EnableInteraction called with role: {role}");
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit) currentMode = InteractionMode.AmbushPlacement;
        else currentMode = InteractionMode.None;

        Debug.Log($"INTERACTIONMANAGER: Interaction mode set to: {currentMode}");
        ResetState();
        workerObj.SetActive(false);
    }
    public void DisableInteraction() { currentMode = InteractionMode.None; }

    void Update()
    {
        if (isMoving) MoveWorker();
        if (currentMode == InteractionMode.AmbushPlacement) DrawAmbushLines();
    }

    public void OnHexClicked(Hex h)
    {
    }

    public void OnEdgeClicked(HexEdge e)
    {
    }

    public void OnVertexClicked(HexVertex v)
    {
        Debug.Log($"INTERACTIONMANAGER: Vertex clicked: {v}, CurrentMode: {currentMode}");
        if (currentMode == InteractionMode.PathSelection) HandlePathClick(v);
        else if (currentMode == InteractionMode.AmbushPlacement) HandleAmbushClick(v);
    }

    void HandlePathClick(HexVertex v)
    {
        Debug.Log($"INTERACTIONMANAGER: HandlePathClick called for vertex: {v}");
        Debug.Log($"INTERACTIONMANAGER: pathComplete: {pathComplete}, isMoving: {isMoving}, selectedVertices count: {selectedVertices.Count}");
        
        if (pathComplete || isMoving) return;

        if (!selectedVertices.Any())
        {
            Debug.Log($"INTERACTIONMANAGER: Starting new path with vertex: {v}");
            selectedVertices.Add(v);
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v));
            Debug.Log($"INTERACTIONMANAGER: Found {validNextVertices.Count} neighbor vertices");
            return;
        }
        if (!validNextVertices.Contains(v)) { 
            Debug.Log($"INTERACTIONMANAGER: Invalid vertex selection. Resetting to neighbors of last selected vertex.");
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(selectedVertices.Last())); 
            return; 
        }

        Debug.Log($"INTERACTIONMANAGER: Adding vertex to path: {v}");
        selectedVertices.Add(v);
        if (centralVertices.Contains(v)) {
            Debug.Log($"INTERACTIONMANAGER: Path completed! Connected to center.");
            pathComplete = true;
        }
        validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(selectedVertices));
        Debug.Log($"INTERACTIONMANAGER: Path now has {selectedVertices.Count} vertices, {validNextVertices.Count} valid next vertices");
    }

    public void SubmitPath()
    {
        if (!pathComplete) return;
        Debug.Log($"INTERACTIONMANAGER: Submitting path with {selectedVertices.Count} vertices");
        foreach (var vertex in selectedVertices)
        {
            Debug.Log($"INTERACTIONMANAGER: Path vertex: {vertex}");
        }
        var serializableVertices = selectedVertices.Select(v => new SerializableHexVertex(v)).ToArray();
        
        // Test individual vertex serialization
        if (serializableVertices.Length > 0)
        {
            Debug.Log($"INTERACTIONMANAGER: Testing single vertex: {JsonUtility.ToJson(serializableVertices[0])}");
        }
        
        var pathData = new PlaceWorkersPayload 
        { 
            paths = new SerializablePathData[] 
            { 
                new SerializablePathData { path = serializableVertices }
            }
        };
        Debug.Log($"INTERACTIONMANAGER: Serialized payload: {JsonUtility.ToJson(pathData)}");
        
        // Alternative: Create simple manual JSON as backup
        var manualJson = "{\"paths\":[[";
        for (int i = 0; i < serializableVertices.Length; i++)
        {
            var v = serializableVertices[i];
            manualJson += $"{{\"q\":{v.q},\"r\":{v.r},\"direction\":{v.direction}}}";
            if (i < serializableVertices.Length - 1) manualJson += ",";
        }
        manualJson += "]]}";
        Debug.Log($"INTERACTIONMANAGER: Manual JSON: {manualJson}");
        
        net.Send("place_workers", pathData);
        DisableInteraction();
    }

    public void ExecuteServerPath(List<HexVertex> path)
    {
        Debug.Log($"INTERACTIONMANAGER: ExecuteServerPath called with {path.Count} vertices");
        serverPathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();
        if (!serverPathWorld.Any()) return;
        
        Debug.Log($"INTERACTIONMANAGER: Starting position: {serverPathWorld[0]}");
        Debug.Log($"INTERACTIONMANAGER: Worker object: {workerObj?.name}");
        
        workerObj.transform.position = serverPathWorld[0];
        workerObj.SetActive(true);
        isMoving = true; pathStep = 0;
        
        Debug.Log($"INTERACTIONMANAGER: Worker activated at position: {workerObj.transform.position}");
        Debug.Log($"INTERACTIONMANAGER: Worker active state: {workerObj.activeInHierarchy}");
    }

    void MoveWorker()
    {
        if (pathStep >= serverPathWorld.Count) 
        {
            Debug.Log("INTERACTIONMANAGER: Worker movement completed");
            isMoving = false; 
            workerObj.SetActive(false); 
            ResetState(); 
            return;
        }
        
        var curr = workerObj.transform.position;
        var targ = serverPathWorld[pathStep];
        
        // Debug every few frames to avoid spam
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"INTERACTIONMANAGER: Moving worker - Step: {pathStep}, Current: {curr}, Target: {targ}");
        }
        
        // Visual debug - draw a line to show worker path
        Debug.DrawLine(curr, targ, Color.red, 0.1f);
        
        workerObj.transform.position = Vector3.MoveTowards(curr, targ, workerSpeed * Time.deltaTime);
        
        if (Vector3.Distance(curr, targ) < 0.01f) 
        {
            pathStep++;
            Debug.Log($"INTERACTIONMANAGER: Reached target {pathStep-1}, moving to step {pathStep}");
            if (pathStep >= serverPathWorld.Count) 
            { 
                Debug.Log("INTERACTIONMANAGER: Worker movement completed");
                isMoving = false; 
                workerObj.SetActive(false); 
                ResetState(); 
            }
        }
    }

    IEnumerable<HexVertex> GetNeighborVertices(HexVertex v)
        => v.GetAdjacentHexes()
            .SelectMany(hex => Enum.GetValues(typeof(HexDirection)).Cast<HexDirection>().Select(dir => new HexEdge(hex, dir)))
            .Where(e => e.GetVertexEndpoints().Contains(v))
            .SelectMany(e => e.GetVertexEndpoints())
            .Where(x => !x.Equals(v))
            .Distinct();

    void HandleAmbushClick(HexVertex v)
    {
        if (ambushEdges.Count >= maxAmbushes) return;
        if (ambushStart.Equals(default)) { ambushStart = v; return; }
        if (GetNeighborVertices(ambushStart).Contains(v))
        {
            var dir = GetDirectionBetween(ambushStart, v);
            var edge = new HexEdge(ambushStart.Hex, dir);
            net.Send("buy_ambush", edge);
        }
        ambushStart = default;
    }

    public void FinalizeAmbushes()
    {
        net.Send("place_ambushes", ambushEdges.ToList());
        DisableInteraction();
    }

    void ConfirmAmbushPlacement(HexEdge edge)
    {
        ambushEdges.Add(edge);
    }

    void DrawAmbushLines()
    {
        foreach (var e in ambushEdges)
        {
            Debug.DrawLine(e.ToWorld(gridGen.hexRadius), e.GetNeighbor().ToWorld(gridGen.hexRadius), Color.red);
        }
    }

    void ResetState()
    {
        selectedVertices.Clear(); validNextVertices.Clear(); pathComplete = false;
        isMoving = false; workerObj?.SetActive(false);
        ambushStart = default; ambushEdges.Clear();
    }

    HexDirection GetDirectionBetween(HexVertex a, HexVertex b)
    {
        foreach (var hex in a.GetAdjacentHexes())
            if (b.GetAdjacentHexes().Contains(hex))
                foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
                    if (Vector3.Distance(new HexEdge(hex, dir).ToWorld(gridGen.hexRadius),
                        (a.ToWorld(gridGen.hexRadius) + b.ToWorld(gridGen.hexRadius)) / 2f) < 0.01f)
                        return dir;
        return HexDirection.Right;
    }
}
