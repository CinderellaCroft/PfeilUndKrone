using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NetworkingDTOs;

public enum InteractionMode { None, PathSelection, AmbushPlacement }
public enum PathCreationState { NotCreating, SelectingResourceField, SelectingStartVertex, Creating, ReadyToConfirm }

public class InteractionManager : Singleton<InteractionManager>
{
    public HexGridGenerator gridGen;
    public NetworkServiceBase net;

    public GameObject workerPrefab;
    public GameObject ambushOrb;
    public float workerSpeed = 1f;

    // Multiple workers for multiple paths
    private List<GameObject> workerObjects = new();
    private List<List<Vector3>> allServerPathsWorld = new();
    private List<int> workerPathSteps = new();
    private List<bool> workerMovingStates = new();

    public Material ambushLineMaterial;
    public int maxAmbushes = 5;

    // Path colors for multiple paths
    public Color[] pathColors = { Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };

    private InteractionMode currentMode = InteractionMode.None;

    // Multiple paths system for King
    private List<List<HexVertex>> completedPaths = new(); // All completed paths
    private List<Hex> completedPathResourceFields = new(); // Resource fields for each completed path
    private Dictionary<int, Color> pathColorMap = new(); // Color for each path
    private PathCreationState pathCreationState = PathCreationState.NotCreating;
    private int currentPathIndex = -1;

    // Current path being created
    private HashSet<HexVertex> selectedVertices = new();
    private HashSet<HexVertex> validNextVertices = new();
    private List<HexVertex> centralVertices;
    private bool pathComplete;
    
    // Resource field selection for path starting
    private Hex selectedResourceField = default;
    private HashSet<HexVertex> availableStartVertices = new();
    private GameObject workerObj;
    private List<Vector3> serverPathWorld = new();
    private int pathStep;
    private bool isMoving;

    private HexVertex ambushStart;
    private List<NetworkingDTOs.AmbushEdge> placedAmbushes = new();
    private List<GameObject> ambushOrbObjects = new(); // Lokale Kugeln nur für Bandit während Platzierung
    private List<GameObject> animationAmbushOrbObjects = new(); // Animation Kugeln für beide Spieler
    private HashSet<HexEdge> ambushEdges = new();

    // Bandit resources and ambush buying system
    private int currentGold = 20; // Will be updated from server
    private const int ambushCost = 5;
    private int purchasedAmbushes = 0; // How many ambushes bandit has purchased
    private bool isInAmbushPlacementMode = false;

    // Vertex highlighting system
    private HashSet<HexVertex> highlightedVertices = new();
    private Color originalVertexColor = Color.red;
    private Color highlightColor = Color.magenta;

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
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit) currentMode = InteractionMode.AmbushPlacement;
    }

    public void DisableInteraction()
    {
        currentMode = InteractionMode.None;
        ResetState();
    }

    void Update()
    {
        // Move all active workers
        for (int i = 0; i < workerObjects.Count; i++)
        {
            if (i < workerMovingStates.Count && workerMovingStates[i])
            {
                MoveWorker(i);
            }
        }

        // Legacy single worker support
        if (isMoving) MoveWorkerLegacy();

        if (currentMode == InteractionMode.AmbushPlacement) DrawAmbushLines();
    }

    public void OnHexClicked(Hex h) 
    { 
        if (currentMode == InteractionMode.PathSelection) HandleResourceFieldClick(h);
    }
    public void OnEdgeClicked(HexEdge e) { /* ... */ }

    public void OnVertexClicked(HexVertex v)
    {
        if (currentMode == InteractionMode.PathSelection) HandlePathClick(v);
        else if (currentMode == InteractionMode.AmbushPlacement) HandleAmbushClick(v);
    }

    // === VERTEX HIGHLIGHTING SYSTEM ===
    void ToggleVertexHighlight(HexVertex vertex)
    {
        var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
        if (vertexGO == null)
        {
            Debug.LogError($"❌ Error: Could not find GameObject for vertex {vertex}");
            return;
        }

        var renderer = vertexGO.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError($"❌ Error: No Renderer found on vertex GameObject {vertex}");
            return;
        }

        if (highlightedVertices.Contains(vertex))
        {
            renderer.material.color = originalVertexColor;
            highlightedVertices.Remove(vertex);
        }
        else
        {
            renderer.material.color = highlightColor;
            highlightedVertices.Add(vertex);
        }
    }

    void ResetAllVertexHighlights()
    {
        foreach (var vertex in highlightedVertices.ToList())
        {
            var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
            if (vertexGO != null)
            {
                var renderer = vertexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = originalVertexColor;
                }
            }
        }
        highlightedVertices.Clear();
        Debug.Log("All vertex highlights reset to original color");
    }

    void ResetVertexHighlightsKeepAmbushVertices()
    {
        var ambushVertices = new HashSet<HexVertex>();

        // Sammle alle Vertices die mit Ambushes verbunden sind
        foreach (var ambush in placedAmbushes)
        {
            ambushVertices.Add(ambush.cornerA);
            ambushVertices.Add(ambush.cornerB);
        }

        foreach (var vertex in highlightedVertices.ToList())
        {
            if (!ambushVertices.Contains(vertex))
            {
                var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
                if (vertexGO != null)
                {
                    var renderer = vertexGO.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = originalVertexColor;
                    }
                }
                highlightedVertices.Remove(vertex);
            }
        }

        Debug.Log($"Vertex highlights reset, kept {ambushVertices.Count} ambush-connected vertices pink");
    }

    void EnsureVertexHighlighted(HexVertex vertex)
    {
        if (highlightedVertices.Contains(vertex)) return;

        var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
        if (vertexGO != null)
        {
            var renderer = vertexGO.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = highlightColor;
                highlightedVertices.Add(vertex);
            }
        }
    }

    void UpdateVertexHighlightsForAmbushVertices(HexVertex vertex)
    {
        bool stillConnectedToAmbush = placedAmbushes.Any(ambush => ambush.cornerA.Equals(vertex) || ambush.cornerB.Equals(vertex));

        if (!stillConnectedToAmbush && highlightedVertices.Contains(vertex))
        {
            var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
            if (vertexGO != null)
            {
                var renderer = vertexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = originalVertexColor;
                }
            }
            highlightedVertices.Remove(vertex);
        }
    }

    public void ResetVertexHighlights()
    {
        ResetAllVertexHighlights();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugHighlightedVertices()
    {
        Debug.Log($"Currently highlighted vertices: {highlightedVertices.Count}");
        foreach (var vertex in highlightedVertices)
        {
            Debug.Log($"  Highlighted: {vertex}");
        }
    }

    // === MULTIPLE PATHS SYSTEM FOR KING ===
    public void StartNewPath()
    {
        if (currentMode != InteractionMode.PathSelection) return;

        if (pathCreationState == PathCreationState.Creating || pathCreationState == PathCreationState.SelectingResourceField || pathCreationState == PathCreationState.SelectingStartVertex)
        {
            Debug.LogError("❌ Error: Already creating a path! Confirm current path first.");
            return;
        }

        pathCreationState = PathCreationState.SelectingResourceField;
        currentPathIndex = completedPaths.Count;

        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;

        Debug.Log($"✅ Started creating path #{currentPathIndex + 1} - First select a resource field");
    }

    public void ConfirmCurrentPath()
    {
        if (pathCreationState != PathCreationState.ReadyToConfirm)
        {
            Debug.LogError("❌ Error: No path ready to confirm!");
            return;
        }

        if (!pathComplete || selectedVertices.Count == 0)
        {
            Debug.LogError("❌ Error: Path is not complete!");
            return;
        }

        var pathList = selectedVertices.ToList();
        completedPaths.Add(pathList);
        completedPathResourceFields.Add(selectedResourceField);

        Color pathColor = pathColors[currentPathIndex % pathColors.Length];
        pathColorMap[currentPathIndex] = pathColor;

        VisualizeCompletedPath(currentPathIndex, pathColor);

        pathCreationState = PathCreationState.NotCreating;
        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;

        var resourceField = completedPathResourceFields[currentPathIndex];
        Debug.Log($"✅ Confirmed path #{currentPathIndex + 1} with {pathList.Count} vertices starting from resource field ({resourceField.Q},{resourceField.R})");
        currentPathIndex = -1;
    }

    private void VisualizeCompletedPath(int pathIndex, Color pathColor)
    {
        var pathVertices = completedPaths[pathIndex];
        foreach (var vertex in pathVertices)
        {
            var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
            if (vertexGO != null)
            {
                var renderer = vertexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = pathColor;
                }
            }
        }
    }

    public string GetPathCreationButtonText()
    {
        switch (pathCreationState)
        {
            case PathCreationState.NotCreating:
                return "Pfad erstellen";
            case PathCreationState.SelectingResourceField:
                return "Ressourcenfeld wählen";
            case PathCreationState.SelectingStartVertex:
                return "Startecke wählen";
            case PathCreationState.Creating:
                return pathComplete ? "Pfad bestätigen" : "Pfad erstellen";
            case PathCreationState.ReadyToConfirm:
                return "Pfad bestätigen";
            default:
                return "Pfad erstellen";
        }
    }

    public bool CanCreateNewPath()
    {
        return pathCreationState == PathCreationState.NotCreating;
    }

    public bool CanConfirmPath()
    {
        return pathCreationState == PathCreationState.ReadyToConfirm && pathComplete;
    }

    public int GetCompletedPathCount()
    {
        return completedPaths.Count;
    }

    // === AMBUSH BUYING SYSTEM FOR BANDIT ===
    public void UpdateGoldAmount(int newGoldAmount)
    {
        currentGold = newGoldAmount;
        Debug.Log($"💰 Gold updated: {currentGold}");
    }

    public bool CanBuyAmbush()
    {
        return currentGold >= ambushCost && currentMode == InteractionMode.AmbushPlacement;
    }

    public string GetAmbushBuyButtonText()
    {
        if (CanBuyAmbush())
        {
            return $"Ambush kaufen ({ambushCost} Gold)";
        }
        else
        {
            return $"Nicht genug Gold ({ambushCost} Gold benötigt)";
        }
    }

    public void BuyAmbush()
    {
        if (!CanBuyAmbush())
        {
            Debug.LogError($"❌ Error: Cannot buy ambush! Need {ambushCost} gold, have {currentGold}");
            UIManager.Instance.UpdateInfoText($"Error: Need {ambushCost} gold, have {currentGold}");
            return;
        }

        // Send buy request to server
        var payload = new { cost = ambushCost };
        net.Send("buy_ambush", payload);

        Debug.Log($"Ambush purchase request sent (Cost: {ambushCost} gold)");

        // Server will respond with ambush_approved or ambush_denied
        // On approval, server will update our gold and we can place the ambush
    }

    public void OnAmbushPurchaseApproved()
    {
        purchasedAmbushes++;
        isInAmbushPlacementMode = true;

        Debug.Log($"✅ Ambush purchase approved! Can now place ambush #{purchasedAmbushes}");
        UIManager.Instance.UpdateInfoText($"Ambush approved! Click two neighboring vertices to place it.");
    }

    public void OnAmbushPurchaseDenied(string reason)
    {
        Debug.LogError($"❌ Ambush purchase denied: {reason}");
        UIManager.Instance.UpdateInfoText($"Error: {reason}");
    }

    public int GetPurchasedAmbushCount()
    {
        return purchasedAmbushes;
    }

    public int GetPlacedAmbushCount()
    {
        return placedAmbushes.Count;
    }

    // === RESOURCE FIELD HANDLING ===
    void HandleResourceFieldClick(Hex h)
    {
        Debug.Log($"🔍 HandleResourceFieldClick called with hex ({h.Q},{h.R}), current state: {pathCreationState}");
        
        if (pathCreationState != PathCreationState.SelectingResourceField) 
        {
            Debug.Log($"❌ Not in resource field selection state. Current state: {pathCreationState}");
            return;
        }
        
        // Check if this is a valid resource field (not Castle or Moat)
        var resourceMap = GameManager.Instance?.GetResourceMap();
        if (resourceMap == null || !resourceMap.ContainsKey(h))
        {
            Debug.LogError($"❌ Error: Invalid hex field selected! ResourceMap null: {resourceMap == null}, Contains key: {resourceMap?.ContainsKey(h)}");
            return;
        }
        
        var fieldType = resourceMap[h];
        Debug.Log($"🔍 Field type at ({h.Q},{h.R}): {fieldType}");
        
        if (fieldType == FieldType.Castle || fieldType == FieldType.Moat)
        {
            Debug.LogError($"❌ Error: Cannot start path from {fieldType} field! Select a resource field (Wood, Wheat, Ore, Desert).");
            return;
        }
        
        selectedResourceField = h;
        pathCreationState = PathCreationState.SelectingStartVertex;
        
        // Get all vertices of the selected resource field
        availableStartVertices.Clear();
        for (int i = 0; i < 6; i++)
        {
            var vertex = new HexVertex(h, (VertexDirection)i);
            availableStartVertices.Add(vertex);
            ToggleVertexHighlight(vertex); // Highlight available start vertices
        }
        
        Debug.Log($"✅ Resource field {fieldType} at ({h.Q},{h.R}) selected. Now select a corner to start the path.");
    }

    // === PATH HANDLING ===
    void HandlePathClick(HexVertex v)
    {
        if (pathComplete || isMoving) return;
        
        if (pathCreationState == PathCreationState.SelectingStartVertex)
        {
            // Check if clicked vertex is one of the available start vertices
            if (!availableStartVertices.Contains(v))
            {
                Debug.LogError("❌ Error: Select a corner of the selected resource field!");
                return;
            }
            
            // Clear highlights from available vertices and only highlight the selected one
            foreach (var vertex in availableStartVertices)
            {
                if (!vertex.Equals(v))
                {
                    ToggleVertexHighlight(vertex); // Remove highlight
                }
            }
            
            selectedVertices.Add(v);
            pathCreationState = PathCreationState.Creating;
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v));
            
            Debug.Log($"✅ Start vertex selected: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");
            return;
        }

        if (pathCreationState != PathCreationState.Creating)
        {
            Debug.LogError("❌ Error: Not currently creating a path! Click 'Pfad erstellen' first.");
            return;
        }

        Debug.Log($"Vertex clicked: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");

        if (!validNextVertices.Contains(v))
        {
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(selectedVertices.Last()));
            return;
        }

        selectedVertices.Add(v);
        ToggleVertexHighlight(v);
        if (centralVertices.Contains(v))
        {
            pathComplete = true;
            pathCreationState = PathCreationState.ReadyToConfirm;
            Debug.Log("✅ Path completed! Ready to confirm.");
        }
        validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(selectedVertices));
    }

    public void SubmitPath()
    {
        if (currentMode != InteractionMode.PathSelection)
        {
            Debug.LogError("❌ Error: Not in path selection mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in path selection mode!");
            return;
        }

        if (completedPaths.Count == 0)
        {
            Debug.LogError("❌ Error: No paths created!");
            UIManager.Instance.UpdateInfoText("Error: No paths created!");
            return;
        }

        var serializablePaths = completedPaths.Select((path, index) => 
        {
            var resourceField = completedPathResourceFields[index];
            var resourceMap = GameManager.Instance?.GetResourceMap();
            var resourceType = resourceMap?.ContainsKey(resourceField) == true ? resourceMap[resourceField].ToString() : "Unknown";
            
            return new SerializablePathData 
            { 
                path = path.Select(v => new SerializableHexVertex(v)).ToArray(),
                resourceFieldQ = resourceField.Q,
                resourceFieldR = resourceField.R,
                resourceType = resourceType
            };
        }).ToArray();

        var pathData = new PlaceWorkersPayload { paths = serializablePaths };

        Debug.Log($"✅ Sending {completedPaths.Count} paths to server");
        net.Send("place_workers", pathData);
        DisableInteraction();

        ResetAllVertexHighlights();
    }

    public void ExecuteServerPath(List<HexVertex> path)
    {
        serverPathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();
        if (!serverPathWorld.Any()) return;

        for (int i = 0; i < serverPathWorld.Count; i++)
        {
            serverPathWorld[i] = new Vector3(serverPathWorld[i].x, serverPathWorld[i].y + 0.35f, serverPathWorld[i].z);
        }

        workerObj.transform.position = serverPathWorld[0];
        workerObj.SetActive(true);
        isMoving = true;
        pathStep = 0;
    }

    void MoveWorkerLegacy()
    {
        if (pathStep >= serverPathWorld.Count)
        {
            isMoving = false;
            workerObj.SetActive(false);
            return;
        }

        var curr = workerObj.transform.position;
        var targ = serverPathWorld[pathStep];

        // Kollisionserkennung - wir speichern die Anzahl der Orbs vor der Prüfung
        int orbCountBefore = animationAmbushOrbObjects.Count;
        CheckCollisionWithOrbs(curr);
        int orbCountAfter = animationAmbushOrbObjects.Count;

        // Wenn eine Kollision erkannt wurde (eine Kugel wurde entfernt), Worker deaktivieren
        if (orbCountAfter < orbCountBefore)
        {
            isMoving = false;
            workerObj.SetActive(false);
            Debug.Log("Legacy-Worker wurde durch Ambush zerstört");
            return;
        }

        if (Vector3.Distance(curr, targ) < 0.01f)
        {
            pathStep++;
            if (pathStep >= serverPathWorld.Count)
            {
                isMoving = false;
                workerObj.SetActive(false);
            }
        }
        else
        {
            workerObj.transform.position = Vector3.MoveTowards(curr, targ, workerSpeed * Time.deltaTime);
        }
    }

    void MoveWorker(int workerIndex)
    {
        if (workerIndex >= workerObjects.Count || workerIndex >= allServerPathsWorld.Count) return;
        if (workerPathSteps[workerIndex] >= allServerPathsWorld[workerIndex].Count)
        {
            workerMovingStates[workerIndex] = false;
            return;
        }

        var workerObj = workerObjects[workerIndex];
        var pathWorld = allServerPathsWorld[workerIndex];
        var curr = workerObj.transform.position;
        var targ = pathWorld[workerPathSteps[workerIndex]];

        // Kollisionserkennung - wir speichern die Anzahl der Orbs vor der Prüfung
        int orbCountBefore = animationAmbushOrbObjects.Count;
        CheckCollisionWithOrbs(curr);
        int orbCountAfter = animationAmbushOrbObjects.Count;

        // Wenn eine Kollision erkannt wurde (eine Kugel wurde entfernt), Worker deaktivieren
        if (orbCountAfter < orbCountBefore)
        {
            workerMovingStates[workerIndex] = false;
            workerObj.SetActive(false);
            Debug.Log($"Worker {workerIndex} wurde durch Ambush zerstört");
            return;
        }

        if (Vector3.Distance(curr, targ) < 0.01f)
        {
            workerPathSteps[workerIndex]++;
            if (workerPathSteps[workerIndex] >= pathWorld.Count)
            {
                workerMovingStates[workerIndex] = false;
                Debug.Log($"Worker {workerIndex} finished path");
            }
        }
        else
        {
            workerObj.transform.position = Vector3.MoveTowards(curr, targ, workerSpeed * Time.deltaTime);
        }
    }

    public void ExecuteServerPaths(List<List<HexVertex>> paths)
    {
        // Clear existing workers
        foreach (var worker in workerObjects)
        {
            if (worker != null) Destroy(worker);
        }
        workerObjects.Clear();
        allServerPathsWorld.Clear();
        workerPathSteps.Clear();
        workerMovingStates.Clear();

        Debug.Log($"ExecuteServerPaths: Creating workers for {paths.Count} paths");

        for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
        {
            var path = paths[pathIndex];
            var pathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();

            if (!pathWorld.Any()) continue;

            // Elevate positions slightly
            for (int i = 0; i < pathWorld.Count; i++)
            {
                pathWorld[i] = new Vector3(pathWorld[i].x, pathWorld[i].y + 0.35f, pathWorld[i].z);
            }

            // Create worker for this path
            var workerObj = Instantiate(workerPrefab);
            workerObj.transform.position = pathWorld[0];
            workerObj.SetActive(true);

            // Store path data
            workerObjects.Add(workerObj);
            allServerPathsWorld.Add(pathWorld);
            workerPathSteps.Add(0);
            workerMovingStates.Add(true);

            Debug.Log($"Created worker {pathIndex} for path with {path.Count} vertices");
        }
    }

    void CheckCollisionWithOrbs(Vector3 workerPosition)
    {
        for (int i = animationAmbushOrbObjects.Count - 1; i >= 0; i--)
        {
            if (animationAmbushOrbObjects[i] != null)
            {
                float distance = Vector3.Distance(workerPosition, animationAmbushOrbObjects[i].transform.position);
                if (distance < 0.1f)
                {
                    Debug.Log($"❌ AMBUSH COLLISION DETECTED! ({GameManager.Instance?.MyRole})");
                    Destroy(animationAmbushOrbObjects[i]);
                    animationAmbushOrbObjects.RemoveAt(i);

                    // Dann den Worker des Königs stoppen und deaktivieren
                    // Bei der Legacy-Version (einzelner Worker)
                    if (isMoving && workerObj != null)
                    {
                        isMoving = false;
                        workerObj.SetActive(false);
                        Debug.Log("König-Worker wurde durch Ambush zerstört (Legacy)");
                        return; // Sofort zurückkehren, da der Worker deaktiviert wurde
                    }

                    // Für Multi-Worker System - wir müssen den richtigen Worker finden und deaktivieren
                    // Das wird in MoveWorker behandelt, da wir hier den Index nicht haben
                }
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

    // === AMBUSH HANDLING ===
    void HandleAmbushClick(HexVertex v)
    {
        if (!isInAmbushPlacementMode)
        {
            Debug.LogError("❌ Error: No ambush purchased! Click 'Buy Ambush' first.");
            return;
        }

        if (ambushStart.Equals(default))
        {
            ambushStart = v;
            EnsureVertexHighlighted(v);
            Debug.Log($"✅ Ambush start set at vertex: ({v.Hex.Q},{v.Hex.R},{v.Direction})");
            return;
        }

        // Check if the clicked vertex is a neighbor of the ambush start
        var neighborsOfStart = GetNeighborVertices(ambushStart).ToList();

        Debug.Log($"Checking neighbors: Start vertex ({ambushStart.Hex.Q},{ambushStart.Hex.R}) has {neighborsOfStart.Count} neighbors");
        Debug.Log($"Clicked vertex: ({v.Hex.Q},{v.Hex.R}) - Is neighbor: {neighborsOfStart.Contains(v)}");

        if (!neighborsOfStart.Contains(v))
        {
            Debug.LogError($"❌ Error: Vertices are not neighbors! Start: ({ambushStart.Hex.Q},{ambushStart.Hex.R}) End: ({v.Hex.Q},{v.Hex.R}) Resetting ambush start.");
            UpdateVertexHighlightsForAmbushVertices(ambushStart);
            ambushStart = default;
            return;
        }

        var existingIndex = placedAmbushes.FindIndex(a =>
            (a.cornerA.Equals(ambushStart) && a.cornerB.Equals(v)) ||
            (a.cornerA.Equals(v) && a.cornerB.Equals(ambushStart)));

        if (existingIndex >= 0)
        {
            if (existingIndex < ambushOrbObjects.Count)
            {
                Destroy(ambushOrbObjects[existingIndex]);
                ambushOrbObjects.RemoveAt(existingIndex);
            }
            placedAmbushes.RemoveAt(existingIndex);
            UpdateVertexHighlightsForAmbushVertices(ambushStart);
            UpdateVertexHighlightsForAmbushVertices(v);
            Debug.Log($"Ambush removed between vertices ({ambushStart.Hex.Q},{ambushStart.Hex.R}) and ({v.Hex.Q},{v.Hex.R})");
        }
        else
        {
            if (placedAmbushes.Count >= maxAmbushes)
            {
                Debug.LogError($"❌ Error: Maximum ambushes ({maxAmbushes}) already placed!");
                ambushStart = default;
                return;
            }

            if (ambushOrb == null)
            {
                Debug.LogError("❌ Error: ambushOrb prefab is null!");
                return;
            }

            var ambushEdge = new NetworkingDTOs.AmbushEdge { cornerA = ambushStart, cornerB = v };
            placedAmbushes.Add(ambushEdge);


            var aPos = ambushStart.ToWorld(gridGen.hexRadius);
            var bPos = v.ToWorld(gridGen.hexRadius);

            var midPoint = (aPos + bPos) * 0.5f;
            midPoint.y = 0;

            Vector3 dir = bPos - aPos;
            dir.y = 0f;

            Quaternion rot;
            if (dir.sqrMagnitude <= 1e-6f)
                rot = Quaternion.identity;
            else
                rot = Quaternion.LookRotation(dir);

            var orbGO = Instantiate(ambushOrb, midPoint, rot);
            ambushOrbObjects.Add(orbGO);

            EnsureVertexHighlighted(v);
            Debug.Log($"✅ Ambush created between vertices ({ambushStart.Hex.Q},{ambushStart.Hex.R}) and ({v.Hex.Q},{v.Hex.R})");
        }

        // Reset for next ambush placement (don't disable placement mode immediately)
        ambushStart = default;

        // Only disable placement mode if we've used up all purchased ambushes
        if (placedAmbushes.Count >= purchasedAmbushes)
        {
            isInAmbushPlacementMode = false;
            Debug.Log($"✅ All purchased ambushes ({purchasedAmbushes}) have been placed. Placement mode disabled.");
        }
        else
        {
            Debug.Log($"✅ Ambush placed. {purchasedAmbushes - placedAmbushes.Count} more ambushes available to place.");
        }
    }

    public void FinalizeAmbushes()
    {
        if (currentMode != InteractionMode.AmbushPlacement)
        {
            Debug.LogError("❌ Error: Not in ambush placement mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in ambush placement mode!");
            return;
        }

        if (placedAmbushes.Count == 0)
        {
            Debug.LogError("❌ Error: No ambushes placed!");
            UIManager.Instance.UpdateInfoText("Error: No ambushes placed!");
            return;
        }

        var serializableAmbushes = placedAmbushes.Select(a => new SerializableAmbushEdge(a)).ToArray();
        var payload = new PlaceAmbushesPayload { ambushes = serializableAmbushes };

        try
        {
            net.Send("place_ambushes", payload);
            Debug.Log($"✅ Sent {placedAmbushes.Count} ambushes to server");
            DisableInteraction();
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error: Failed to send ambushes to server: {e.Message}");
        }

        ResetVertexHighlightsKeepAmbushVertices();
    }

    void DrawAmbushLines()
    {
        if (!ambushStart.Equals(default))
        {
            // Draw preview line logic here if needed
        }
    }

    // === ANIMATION AND DISPLAY ===
    public void DisplayAnimationOrbs(List<NetworkingDTOs.AmbushEdge> banditAmbushes)
    {
        // Remove this condition - ambushes should be visible to BOTH players during animation
        // if (GameManager.Instance?.MyRole == PlayerRole.Bandit)
        // {
        //     return;
        // }

        if (ambushOrb == null)
        {
            Debug.LogError("❌ Fehler: ambushOrb Prefab is not assigned in the Inspector!");
            return;
        }

        Debug.Log($"DisplayAnimationOrbs: Showing {banditAmbushes.Count} ambushes for {GameManager.Instance?.MyRole}");

        foreach (var ambush in banditAmbushes)
        {
            if (!IsValidAmbushForAnimation(ambush))
            {
                Debug.LogWarning($"⚠️ [IM] Skipping invalid ambush during orb creation");
                continue;
            }

            Debug.Log($"[IM] Valid ambush [{banditAmbushes.IndexOf(ambush)}]: cornerA({ambush.cornerA.Hex.Q},{ambush.cornerA.Hex.R},{ambush.cornerA.Direction}) <-> cornerB({ambush.cornerB.Hex.Q},{ambush.cornerB.Hex.R},{ambush.cornerB.Direction})");

            var midPoint = (ambush.cornerA.ToWorld(gridGen.hexRadius) + ambush.cornerB.ToWorld(gridGen.hexRadius)) / 2f;
            midPoint.y = 0.35f;

            var orbGO = Instantiate(ambushOrb, midPoint, Quaternion.identity);
            animationAmbushOrbObjects.Add(orbGO);

            Debug.Log($"✅ Animation orb created at position {midPoint} for ambush {ambush.cornerA} <-> {ambush.cornerB}");
        }
    }

    bool IsValidAmbushForAnimation(NetworkingDTOs.AmbushEdge ambush)
    {
        if (ambush.cornerA.Equals(default(HexVertex)) || ambush.cornerB.Equals(default(HexVertex)))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has default/invalid corners, skipping");
            return false;
        }

        if (ambush.cornerA.Equals(ambush.cornerB))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has identical corners ({ambush.cornerA}), skipping");
            return false;
        }

        bool isCornerACenter = ambush.cornerA.Hex.Q == 0 && ambush.cornerA.Hex.R == 0;
        bool isCornerBCenter = ambush.cornerB.Hex.Q == 0 && ambush.cornerB.Hex.R == 0;

        if (isCornerACenter && isCornerBCenter)
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has both corners at center hex (0,0), skipping");
            return false;
        }

        var neighborsOfA = GetNeighborVertices(ambush.cornerA).ToList();
        if (!neighborsOfA.Contains(ambush.cornerB))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush vertices are not neighbors: {ambush.cornerA} <-> {ambush.cornerB}");
            return false;
        }

        return true;
    }

    public void StartSynchronizedAnimation(List<HexVertex> kingPath, List<NetworkingDTOs.AmbushEdge> banditAmbushes)
    {
        DisplayAnimationOrbs(banditAmbushes);
        StartCoroutine(DelayedWorkerExecution(kingPath, 2f));
    }

    public void StartSynchronizedAnimationMultiplePaths(List<List<HexVertex>> kingPaths, List<NetworkingDTOs.AmbushEdge> banditAmbushes)
    {
        DisplayAnimationOrbs(banditAmbushes);
        StartCoroutine(DelayedWorkerExecutionMultiple(kingPaths, 2f));
    }

    System.Collections.IEnumerator DelayedWorkerExecution(List<HexVertex> path, float delay)
    {
        yield return new WaitForSeconds(delay);
        ExecuteServerPath(path);
    }

    System.Collections.IEnumerator DelayedWorkerExecutionMultiple(List<List<HexVertex>> paths, float delay)
    {
        yield return new WaitForSeconds(delay);
        ExecuteServerPaths(paths);
    }

    public void ClearAnimationOrbs()
    {
        // This method is now replaced by CleanupAfterRoundAnimation()
        // Keep for backward compatibility but delegate to new method
        Debug.Log($"ClearAnimationOrbs (legacy): Delegating to CleanupAfterRoundAnimation for {GameManager.Instance?.MyRole}");
        CleanupAfterRoundAnimation();
    }

    // === STATE MANAGEMENT ===
    void ResetState()
    {
        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        isMoving = false;
        workerObj?.SetActive(false);
        ambushStart = default;

        // Clean up multiple workers
        foreach (var worker in workerObjects)
        {
            if (worker != null) worker.SetActive(false);
        }

        pathCreationState = PathCreationState.NotCreating;
        currentPathIndex = -1;
        isInAmbushPlacementMode = false;

        if (!isMoving)
        {
            foreach (var orb in ambushOrbObjects)
            {
                if (orb != null) Destroy(orb);
            }
            ambushOrbObjects.Clear();
            placedAmbushes.Clear();
        }

        ambushEdges.Clear();
    }

    public void ForceCompleteReset()
    {
        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        isMoving = false;
        workerObj?.SetActive(false);
        ambushStart = default;

        // Clean up all worker objects
        foreach (var worker in workerObjects)
        {
            if (worker != null)
            {
                worker.SetActive(false);
                Destroy(worker);
            }
        }
        workerObjects.Clear();
        allServerPathsWorld.Clear();
        workerPathSteps.Clear();
        workerMovingStates.Clear();

        // Reset vertex colors BEFORE clearing completedPaths
        ResetAllVertexHighlights();
        ResetAllVertexColorsToOriginal();

        pathCreationState = PathCreationState.NotCreating;
        currentPathIndex = -1;
        completedPaths.Clear();
        completedPathResourceFields.Clear();
        pathColorMap.Clear();
        purchasedAmbushes = 0;
        isInAmbushPlacementMode = false;

        foreach (var orb in ambushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        ambushOrbObjects.Clear();

        foreach (var orb in animationAmbushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        animationAmbushOrbObjects.Clear();

        placedAmbushes.Clear();
        ambushEdges.Clear();

        Debug.Log("🔄 Complete reset performed - all colors, workers, and ambushes cleared");
    }

    // New method to reset ALL vertex colors, not just highlighted ones
    void ResetAllVertexColorsToOriginal()
    {
        // Reset any vertices that might have path colors from VisualizeCompletedPath
        for (int pathIndex = 0; pathIndex < pathColorMap.Count; pathIndex++)
        {
            if (pathIndex < completedPaths.Count)
            {
                var pathVertices = completedPaths[pathIndex];
                foreach (var vertex in pathVertices)
                {
                    var vertexGO = GridVisualsManager.Instance.GetVertexGameObject(vertex);
                    if (vertexGO != null)
                    {
                        var renderer = vertexGO.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material.color = originalVertexColor;
                        }
                    }
                }
            }
        }

        Debug.Log("All vertex colors reset to original");
    }

    // Public method to clean up after animation ends (called after round execution)
    public void CleanupAfterRoundAnimation()
    {
        Debug.Log($"CleanupAfterRoundAnimation called for {GameManager.Instance?.MyRole}");

        // Clear all animation orbs for BOTH players
        foreach (var orb in animationAmbushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        animationAmbushOrbObjects.Clear();

        // Clear all worker objects for BOTH players  
        foreach (var worker in workerObjects)
        {
            if (worker != null)
            {
                worker.SetActive(false);
                Destroy(worker);
            }
        }
        workerObjects.Clear();
        allServerPathsWorld.Clear();
        workerPathSteps.Clear();
        workerMovingStates.Clear();

        // Also clean up legacy worker
        if (workerObj != null)
        {
            workerObj.SetActive(false);
        }
        isMoving = false;

        Debug.Log($"Round cleanup completed for {GameManager.Instance?.MyRole}");
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
