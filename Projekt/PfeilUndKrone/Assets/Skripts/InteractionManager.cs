using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum InteractionMode { None, PathSelection, AmbushPlacement }

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

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

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

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
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit) currentMode = InteractionMode.AmbushPlacement;
        else currentMode = InteractionMode.None;

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
        if (currentMode == InteractionMode.PathSelection) HandlePathClick(v);
        else if (currentMode == InteractionMode.AmbushPlacement) HandleAmbushClick(v);
    }

    void HandlePathClick(HexVertex v)
    {
        if (pathComplete || isMoving) return;

        if (!selectedVertices.Any())
        {
            selectedVertices.Add(v);
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v));
            return;
        }
        if (!validNextVertices.Contains(v)) { validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(selectedVertices.Last())); return; }

        selectedVertices.Add(v);
        if (centralVertices.Contains(v)) pathComplete = true;
        validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(selectedVertices));
    }

    public void SubmitPath()
    {
        if (!pathComplete) return;
        net.Send("place_workers", selectedVertices.ToList());
        DisableInteraction();
    }

    void ExecuteServerPath(List<HexVertex> path)
    {
        serverPathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();
        if (!serverPathWorld.Any()) return;
        workerObj.transform.position = serverPathWorld[0];
        workerObj.SetActive(true);
        isMoving = true; pathStep = 0;
    }

    void MoveWorker()
    {
        var curr = workerObj.transform.position;
        var targ = serverPathWorld[++pathStep];
        workerObj.transform.position = Vector3.MoveTowards(curr, targ, workerSpeed * Time.deltaTime);
        if (Vector3.Distance(curr, targ) < 0.01f) if (pathStep >= serverPathWorld.Count - 1) { isMoving = false; workerObj.SetActive(false); ResetState(); }
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
        currentMode = InteractionMode.None;
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
