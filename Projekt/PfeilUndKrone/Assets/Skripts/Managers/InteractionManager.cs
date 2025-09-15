using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NetworkingDTOs;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

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

    // Workers sitting on resource fields (visible when paths are created)
    private List<GameObject> resourceFieldWorkers = new();

    // Stored path data for bandit visibility (resource fields only, no routes)
    private List<Hex> submittedResourceFields = new();
    private List<bool> submittedPathIsWagonWorker = new();

    public Material ambushLineMaterial;
    public int maxAmbushes = 5;

    // Path colors for multiple paths
    public Color[] pathColors = { Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };

    [Header("Ambush via Edges")]
    public float edgeHoverMaxDistanceWorld = 0.85f;
    private bool hasHoverEdge;
    private HexEdge hoverEdge;

    [Header("Ambush collision tuning")]
    [SerializeField] private float ambushHitRadius = 0.25f;

    private InteractionMode currentMode = InteractionMode.None;

    // Multiple paths system for King
    private List<List<HexVertex>> completedPaths = new(); // All completed paths
    private List<Hex> completedPathResourceFields = new(); // Resource fields for each completed path
    private List<bool> completedPathIsWagonWorker = new();
    private Dictionary<int, Color> pathColorMap = new();
    private bool currentPathUseWagonWorker = false;
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
    private const int ambushCost = 12;
    private int purchasedAmbushes = 0; // How many ambushes bandit has purchased
    private bool isInAmbushPlacementMode = false;
    private int pendingDeletionIndex = -1; // For server-validated deletion
    private int pendingPathDeletionIndex = -1; // For server-validated path deletion

    // King resources and worker buying system
    private int currentGrain = 0; // Will be updated from server
    private int currentWood = 0; // Will be updated from server
    private const int workerGrainCost = 20;
    private const int workerWoodCost = 8;
    private const int wagonWoodCost = 25;
    private int ownedWorkers = 0;
    private bool pendingWorkerPurchase = false; // For server-validated worker purchase
    private bool pendingWagonUpgrade = false; // For server-validated wagon upgrade
    private int usedWorkers = 0;
    private int ownedWagonWorkers = 0;
    private int usedWagonWorkers = 0;

    // Vertex highlighting system
    private HashSet<HexVertex> highlightedVertices = new();
    private Color originalVertexColor = Color.red;
    private Color highlightColor = Color.magenta;

    // Path highlighting system
    private List<HexVertex> serverPathVertices = new();
    private List<List<HexVertex>> allServerPathsVertices = new();

    protected override void Awake()
    {
        base.Awake();
        workerObj = Instantiate(workerPrefab); workerObj.SetActive(false);
        //net.OnPathApproved += ExecuteServerPath;
        //net.OnAmbushConfirmed += ConfirmAmbushPlacement;
    }

    void Start()
    {
        Debug.Log($"[InteractionManager] Start() - net is assigned: {net != null}");

        // If net is not assigned, try to get NetworkManager.Instance
        if (net == null)
        {
            Debug.Log("[InteractionManager] net is null, trying to get NetworkManager.Instance");
            net = NetworkManager.Instance;
            Debug.Log($"[InteractionManager] NetworkManager.Instance assigned: {net != null}");
        }

        centralVertices = Enum.GetValues(typeof(VertexDirection))
            .Cast<VertexDirection>()
            .Select(d => new HexVertex(new Hex(0, 0), d))
            .ToList();

        // Create ambush edge colliders after a small delay to ensure grid is initialized
        StartCoroutine(CreateAmbushEdgeCollidersDelayed());
    }

    private System.Collections.IEnumerator CreateAmbushEdgeCollidersDelayed()
    {
        // Wait a frame to ensure grid generation is complete
        yield return new WaitForEndOfFrame();
        CreateAmbushEdgeColliders();
    }

    private void CreateAmbushEdgeColliders()
    {
        if (gridGen == null || gridGen.Model?.AllVertices == null)
        {
            Debug.LogWarning("Cannot create ambush edge colliders - grid not ready");
            return;
        }

        Debug.Log("Creating ambush edge colliders...");
        
        // Clear any existing colliders
        foreach (var collider in ambushEdgeColliders)
        {
            if (collider != null) Destroy(collider);
        }
        ambushEdgeColliders.Clear();

        var processedPairs = new HashSet<(HexVertex, HexVertex)>();

        // For each vertex, find its neighbors and create edge colliders
        foreach (var vertex in gridGen.Model.AllVertices)
        {
            var neighbors = GetNeighborVertices(vertex);
            foreach (var neighbor in neighbors)
            {
                // Create an ordered pair to avoid duplicates
                var pair1 = (vertex, neighbor);
                var pair2 = (neighbor, vertex);
                
                if (processedPairs.Contains(pair1) || processedPairs.Contains(pair2))
                    continue;

                processedPairs.Add(pair1);

                // Create invisible collider between these vertices
                CreateEdgeCollider(vertex, neighbor);
            }
        }

        Debug.Log($"Created {ambushEdgeColliders.Count} ambush edge colliders");
    }

    private void CreateEdgeCollider(HexVertex vertexA, HexVertex vertexB)
    {
        var aPos = vertexA.ToWorld(gridGen.hexRadius);
        var bPos = vertexB.ToWorld(gridGen.hexRadius);
        var midPoint = (aPos + bPos) * 0.5f;
        midPoint.y = 0;

        // Create invisible GameObject for the edge
        var edgeGO = new GameObject($"AmbushEdgeCollider_{vertexA.Hex.Q},{vertexA.Hex.R},{vertexA.Direction}_to_{vertexB.Hex.Q},{vertexB.Hex.R},{vertexB.Direction}");
        edgeGO.transform.position = midPoint;
        edgeGO.transform.SetParent(GridVisualsManager.Instance.hexFieldContainer);

        // Add collider - use a capsule collider oriented along the edge
        var collider = edgeGO.AddComponent<CapsuleCollider>();
        collider.direction = 2; // Z-axis
        var distance = Vector3.Distance(aPos, bPos);
        collider.height = distance;
        collider.radius = 0.1f; // Thin radius for precise detection

        // Orient the collider to align with the edge
        var direction = (bPos - aPos).normalized;
        if (direction != Vector3.zero)
        {
            edgeGO.transform.LookAt(edgeGO.transform.position + direction);
        }

        // Add AmbushEdgeMarker component
        var marker = edgeGO.AddComponent<AmbushEdgeMarker>();
        marker.Initialize(vertexA, vertexB);

        // Make the collider invisible but interactive
        edgeGO.layer = 0; // Default layer
        
        ambushEdgeColliders.Add(edgeGO);
    }

    public void EnableInteraction(PlayerRole role)
    {
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit)
        {
            currentMode = InteractionMode.AmbushPlacement;
            ShowWorkersForBandit();
            // Ensure edge colliders are created for new game
            CreateAmbushEdgeColliders();
        }
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
            if (i < workerMovingStates.Count && workerMovingStates[i] && workerObjects[i] != null)
            {
                MoveWorker(i);
            }
        }

        // Legacy single worker support
        if (isMoving && workerObj != null) MoveWorkerLegacy();

        if (currentMode == InteractionMode.AmbushPlacement)
        {
            UpdateAmbushHoverEdge();
            if (LeftClickDown() && hasHoverEdge)
            {
                var endpoints = hoverEdge.GetVertexEndpoints();
                OnAmbushEdgeLeftClick(endpoints[0], endpoints[1]);
            }
            DrawAmbushLines();
        }
    }

    public void OnHexClicked(Hex h)
    {
        if (currentMode == InteractionMode.PathSelection) HandleResourceFieldClick(h);
    }
    public void OnEdgeClicked(HexEdge e) { /* ... */ }

    public void OnVertexClicked(HexVertex v)
    {
        if (currentMode == InteractionMode.PathSelection) HandlePathClick(v);
        // Old ambush system commented out - replaced with edge-based hover system
        // else if (currentMode == InteractionMode.AmbushPlacement) HandleAmbushClick(v);
    }
    
    // Right-click to delete a completed path (only when the player is not in the creating mode)
    public void OnRightClickVertex(HexVertex v)
    {
        if (currentMode != InteractionMode.PathSelection) return;
        if (pathCreationState != PathCreationState.NotCreating) return;

        for (int idx = 0; idx < completedPaths.Count; idx++)
        {
            if (completedPaths[idx].Contains(v))
            {
                DeleteCompletedPath(idx);
                return;
            }
        }
    }
    
    public void OnRightClickEdge(HexEdge e)
    {
        if (currentMode != InteractionMode.PathSelection) return;
        if (pathCreationState != PathCreationState.NotCreating) return; // don't delete while creating

        for (int idx = 0; idx < completedPaths.Count; idx++)
        {
            var path = completedPaths[idx];
            for (int i = 1; i < path.Count; i++)
            {
                if (path[i - 1].TryGetEdgeTo(path[i], out var edge) && edge.Equals(e))
                {
                    DeleteCompletedPath(idx);
                    return;
                }
            }
        }
    }

    // Right-click to delete an ambush orb (only when not in placement mode)
    public void OnRightClickAmbushOrb(GameObject orbGameObject)
    {
        if (currentMode != InteractionMode.AmbushPlacement) return;
        if (isInAmbushPlacementMode) return; // don't delete while placing new ambushes

        // Find which ambush this orb belongs to
        int ambushIndex = ambushOrbObjects.IndexOf(orbGameObject);
        if (ambushIndex >= 0 && ambushIndex < placedAmbushes.Count)
        {
            DeleteAmbush(ambushIndex);
        }
        else
        {
            Debug.LogWarning($"⚠️ Could not find ambush for clicked orb GameObject");
        }
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

    void UpdateAmbushHoverEdge()
    {
        if (!isInAmbushPlacementMode)
        {
            if (hasHoverEdge) { GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false); hasHoverEdge = false; }
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;

        var screenPos = GetPointerPosition();
        var ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float enter))
        {
            if (hasHoverEdge) { GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false); hasHoverEdge = false; }
            return;
        }
        Vector3 hit = ray.GetPoint(enter);

        HexEdge bestEdge = default;
        float bestDist = float.MaxValue;

        foreach (var edge in gridGen.Model.AllEdges)
        {
            if (EdgeBothEndsSpecial(edge)) continue;

            var endpoints = edge.GetVertexEndpoints();
            Vector3 a = endpoints[0].ToWorld(gridGen.hexRadius);
            Vector3 b = endpoints[1].ToWorld(gridGen.hexRadius);
            float d = DistancePointSegmentXZ(hit, a, b);
            if (d < bestDist) { bestDist = d; bestEdge = edge; }
        }

        float maxD = Mathf.Max(edgeHoverMaxDistanceWorld, 0.001f);
        bool within = bestDist <= maxD;

        if (!within)
        {
            if (hasHoverEdge) { GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false); hasHoverEdge = false; }
            return;
        }

        if (!hasHoverEdge || !hoverEdge.Equals(bestEdge))
        {
            if (hasHoverEdge) GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false);
            hoverEdge = bestEdge;
            hasHoverEdge = true;
            GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, true);
        }
    }

    static float DistancePointSegmentXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        a.y = b.y = p.y = 0f;
        var ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        var closest = a + t * ab;
        return Vector3.Distance(p, closest);
    }

    private static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
    return Input.mousePosition;
#endif
    }

    private static bool LeftClickDown()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
    return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool IsSpecialField(FieldType f)
    => f == FieldType.Moat || f == FieldType.Castle;

    private bool EdgeBothEndsSpecial(HexEdge e)
    {
        var map = GameManager.Instance?.GetResourceMap();
        if (map == null) return false;

        if (!map.TryGetValue(e.Hex, out var a)) return false;
        var nb = e.GetNeighbor(); // zweites Hex der Edge
        if (!map.TryGetValue(nb, out var b)) return false;

        return IsSpecialField(a) && IsSpecialField(b);
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

        // Check if King has available workers
        if (GetAvailableWorkerCount() <= 0)
        {
            Debug.LogError("❌ Error: No workers available! Buy a worker first.");
            UIManager.Instance.UpdateInfoText("Error: No workers available! Buy a worker first.");
            return;
        }

        pathCreationState = PathCreationState.SelectingResourceField;
        currentPathIndex = completedPaths.Count;

        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        currentPathUseWagonWorker = false;

        Debug.Log($"Started creating path #{currentPathIndex + 1} - First select a resource field (Available workers: {GetAvailableWorkerCount()})");
    }

    public void StartNewWagonWorkerPath()
    {
        if (currentMode != InteractionMode.PathSelection) return;

        if (pathCreationState == PathCreationState.Creating || pathCreationState == PathCreationState.SelectingResourceField || pathCreationState == PathCreationState.SelectingStartVertex)
        {
            Debug.LogError("❌ Error: Already creating a path! Confirm current path first.");
            return;
        }

        // Check if King has available wagon workers
        if (GetAvailableWagonWorkerCount() <= 0)
        {
            Debug.LogError("❌ Error: No wagon workers available! Upgrade a worker first.");
            UIManager.Instance.UpdateInfoText("Error: No wagon workers available! Upgrade a worker first.");
            return;
        }

        pathCreationState = PathCreationState.SelectingResourceField;
        currentPathIndex = completedPaths.Count;

        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        currentPathUseWagonWorker = true;

        Debug.Log($"Started creating wagon worker path #{currentPathIndex + 1} - First select a resource field (Available wagon workers: {GetAvailableWagonWorkerCount()})");
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

        usedWorkers++;
        if (currentPathUseWagonWorker)
        {
            usedWagonWorkers++;
        }

        var pathList = selectedVertices.ToList();
        completedPaths.Add(pathList);
        completedPathResourceFields.Add(selectedResourceField);
        completedPathIsWagonWorker.Add(currentPathUseWagonWorker);

        Color pathColor = pathColors[currentPathIndex % pathColors.Length];
        pathColorMap[currentPathIndex] = pathColor;

        VisualizeCompletedPath(currentPathIndex, pathColor);
        SpawnWorkerOnResourceField(selectedResourceField, currentPathIndex);

        pathCreationState = PathCreationState.NotCreating;
        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        currentPathUseWagonWorker = false;

        var resourceField = completedPathResourceFields[currentPathIndex];
        Debug.Log($"Confirmed path #{currentPathIndex + 1} with {pathList.Count} vertices starting from resource field ({resourceField.Q},{resourceField.R}) - Workers available: {GetAvailableWorkerCount()}");
        UIManager.Instance.UpdateInfoText($"Path confirmed! Workers available: {GetAvailableWorkerCount()}");
        UIManager.Instance.UpdateWorkerText();
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

        for (int i = 1; i < pathVertices.Count; i++)
        {
            var a = pathVertices[i - 1];
            var b = pathVertices[i];
            if (a.TryGetEdgeTo(b, out var edge))
                GridVisualsManager.Instance.SetEdgeVisible(edge, true);
            else
                Debug.LogWarning($"[IM] No edge found between {a} and {b}");
        }
    }

    private void SpawnWorkerOnResourceField(Hex resourceField, int pathIndex)
    {
        if (workerPrefab == null)
        {
            Debug.LogError("WorkerPrefab is null! Cannot spawn worker on resource field.");
            return;
        }

        // Calculate the world position of the center of the resource field
        Vector3 fieldCenter = resourceField.ToWorld(gridGen.hexRadius);
        fieldCenter.y = 0.35f; // Elevate slightly above the field

        // Instantiate worker at the field center
        GameObject resourceWorker = Instantiate(workerPrefab, fieldCenter, Quaternion.identity);
        resourceWorker.SetActive(true);

        // Store the worker for later cleanup
        resourceFieldWorkers.Add(resourceWorker);

        Debug.Log($"Worker spawned on resource field ({resourceField.Q},{resourceField.R}) for path #{pathIndex + 1}");
    }

    private void ShowWorkersForBandit()
    {
        // Only show workers if there are submitted resource fields and we're the bandit
        if (submittedResourceFields.Count == 0 || GameManager.Instance?.MyRole != PlayerRole.Bandit)
        {
            return;
        }

        if (workerPrefab == null)
        {
            Debug.LogError("WorkerPrefab is null! Cannot show workers to bandit.");
            return;
        }

        // Clear any existing resource field workers
        foreach (var worker in resourceFieldWorkers)
        {
            if (worker != null) Destroy(worker);
        }
        resourceFieldWorkers.Clear();

        // Create workers on submitted resource fields for bandit to see
        for (int i = 0; i < submittedResourceFields.Count; i++)
        {
            var resourceField = submittedResourceFields[i];
            Vector3 fieldCenter = resourceField.ToWorld(gridGen.hexRadius);
            fieldCenter.y = 0.35f; // Elevate slightly above the field

            GameObject banditVisibleWorker = Instantiate(workerPrefab, fieldCenter, Quaternion.identity);
            banditVisibleWorker.SetActive(true);
            resourceFieldWorkers.Add(banditVisibleWorker);

            Debug.Log($"Bandit can see worker on resource field ({resourceField.Q},{resourceField.R})");
        }

        // Hide path routes from bandit by resetting vertex colors
        HidePathRoutesFromBandit();

        Debug.Log($"Bandit can now see {submittedResourceFields.Count} workers on resource fields (without seeing routes)");
    }

    private void HidePathRoutesFromBandit()
    {
        // Only hide routes if we're the bandit
        if (GameManager.Instance?.MyRole != PlayerRole.Bandit)
        {
            return;
        }

        // Reset any vertices that might have path colors from VisualizeCompletedPath
        // This hides the routes without affecting the worker positions
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
                            renderer.material.color = originalVertexColor; // Reset to original color
                        }
                    }
                }
            }
        }

        Debug.Log("Path routes hidden from bandit view");
    }

    public void SetWorkerLocationsForBandit(NetworkingDTOs.WorkerLocationData[] workerLocations)
    {
        if (GameManager.Instance?.MyRole != PlayerRole.Bandit)
        {
            return; // Only bandit should receive this data
        }

        // Clear existing data
        submittedResourceFields.Clear();
        submittedPathIsWagonWorker.Clear();

        // Convert server data to internal format
        foreach (var location in workerLocations)
        {
            submittedResourceFields.Add(new Hex(location.resourceFieldQ, location.resourceFieldR));
            submittedPathIsWagonWorker.Add(location.isWagonWorker);
        }

        Debug.Log($"Bandit received worker locations: {workerLocations.Length} workers");
    }

    public string GetPathCreationButtonText()
    {
        switch (pathCreationState)
        {
            case PathCreationState.NotCreating:
                if (GetAvailableWorkerCount() <= 0)
                    return "No Workers";
                return "Create Path";
            case PathCreationState.SelectingResourceField:
                return "Select Field";
            case PathCreationState.SelectingStartVertex:
                return "Select Corner";
            case PathCreationState.Creating:
                return "Creating...";
            case PathCreationState.ReadyToConfirm:
                return "Ready";
            default:
                return "Create Path";
        }
    }

    public bool CanCreateNewPath()
    {
        return pathCreationState == PathCreationState.NotCreating && GetAvailableWorkerCount() > 0;
    }

    public bool CanCreateNewPath(bool useWagonWorker)
    {
        if (pathCreationState != PathCreationState.NotCreating) return false;

        if (useWagonWorker)
        {
            return GetAvailableWagonWorkerCount() > 0;
        }
        else
        {
            return GetAvailableRegularWorkerCount() > 0;
        }
    }

    public bool CanConfirmPath()
    {
        return pathCreationState == PathCreationState.ReadyToConfirm && pathComplete;
    }

    // Hover on hex fields should only be shown while selecting a resource field for a new path
    public bool IsHexHoverEnabled()
    {
        return currentMode == InteractionMode.PathSelection && pathCreationState == PathCreationState.SelectingResourceField;
    }

    public int GetCompletedPathCount()
    {
        return completedPaths.Count;
    }

    // === RESOURCE MANAGEMENT ===
    public void UpdateGoldAmount(int newGoldAmount)
    {
        currentGold = newGoldAmount;
        Debug.Log($"💰 Gold updated: {currentGold}");
    }

    public void UpdateResources(int gold, int wood, int grain)
    {
        currentGold = gold;
        currentWood = wood;
        currentGrain = grain;
        Debug.Log($"📊 Resources updated: Gold={currentGold}, Wood={currentWood}, Grain={currentGrain}");
    }

    // === WORKER BUYING SYSTEM FOR KING ===
    public bool CanBuyWorker()
    {
        return currentGrain >= workerGrainCost && currentWood >= workerWoodCost && currentMode == InteractionMode.PathSelection && !pendingWorkerPurchase;
    }

    public string GetWorkerBuyButtonText()
    {
        if (CanBuyWorker())
        {
            return "Buy Worker";
        }
        else
        {
            return "Need Resources";
        }
    }

    public void BuyWorker()
    {
        Debug.Log($"[InteractionManager] BuyWorker() called - net is null: {net == null}");

        if (!CanBuyWorker())
        {
            Debug.LogError($"❌ Error: Cannot buy worker! Need {workerGrainCost} grain and {workerWoodCost} wood, have {currentGrain} grain and {currentWood} wood");
            UIManager.Instance.UpdateInfoText($"Error: Need {workerGrainCost} grain and {workerWoodCost} wood, have {currentGrain} grain and {currentWood} wood");
            return;
        }

        // If net is still null, try to get it again
        if (net == null)
        {
            Debug.LogWarning("[InteractionManager] net is null in BuyWorker, trying to get NetworkManager.Instance");
            net = NetworkManager.Instance;
            Debug.Log($"[InteractionManager] NetworkManager.Instance found: {net != null}");
        }

        // Set pending flag to prevent multiple purchases
        pendingWorkerPurchase = true;

        // Send buy request to server
        var payload = new { grainCost = workerGrainCost, woodCost = workerWoodCost };
        net.Send("buy_worker", payload);

        Debug.Log($"Worker purchase request sent (Cost: {workerGrainCost} grain, {workerWoodCost} wood) - Pending: {pendingWorkerPurchase}");

        // Server will respond with worker_approved or worker_denied
        // On approval, server will update our resources and we can place the worker
    }

    public void OnWorkerPurchaseApproved()
    {
        ownedWorkers++;
        pendingWorkerPurchase = false; // Clear pending flag
        Debug.Log($"Worker purchase approved! Total workers: {ownedWorkers} - Pending cleared: {!pendingWorkerPurchase}");
        UIManager.Instance.UpdateInfoText($"Worker purchased! You now have {ownedWorkers - usedWorkers} available workers.");
        UIManager.Instance.UpdateWorkerText();
    }

    public void OnWorkerPurchaseDenied(string reason)
    {
        pendingWorkerPurchase = false; // Clear pending flag
        Debug.LogError($"Worker purchase denied: {reason} - Pending cleared: {!pendingWorkerPurchase}");
        UIManager.Instance.UpdateInfoText($"Error: {reason}");
    }

    // Wagon Worker Management
    public bool CanUpgradeWorkerToWagon()
    {
        return currentWood >= wagonWoodCost && (ownedWorkers - ownedWagonWorkers) > 0 && !pendingWagonUpgrade;
    }

    public void QuitGameRequest()
    {
        if (net == null)
        {
            Debug.LogWarning("[InteractionManager] net is null in BuyWorker, trying to get NetworkManager.Instance");
            net = NetworkManager.Instance;
            Debug.Log($"[InteractionManager] NetworkManager.Instance found: {net != null}");
        }
        var dummyPayload = new { };
        if (!GameManager.Instance.quitGameCalled)
        {
            net.Send("quit_game", dummyPayload);
            Debug.Log($"Quit Game request sent");
            GameManager.Instance.quitGameCalled = true;
        }
        else
        {
            Debug.Log("InteractionManager: GameManager.Instance.quitGameCalled is already true");
        }




    }

    public void UpgradeWorkerToWagon()
    {
        if (!CanUpgradeWorkerToWagon())
        {
            Debug.LogError("Cannot upgrade worker to wagon - insufficient resources or no available workers");
            return;
        }

        // Ensure net is available
        if (net == null) net = NetworkManager.Instance;

        // Set pending flag to prevent multiple upgrades
        pendingWagonUpgrade = true;

        var payload = new { woodCost = wagonWoodCost };
        net.Send("upgrade_worker_wagon", payload);

        Debug.Log($"Wagon upgrade request sent (Cost: {wagonWoodCost} wood) - Pending: {pendingWagonUpgrade}");
        // Server will respond with wagon_upgrade_approved or wagon_upgrade_denied
    }

    public void OnWagonUpgradeApproved(int wagonWorkers, int totalWorkers)
    {
        ownedWorkers = totalWorkers;
        ownedWagonWorkers = wagonWorkers;
        pendingWagonUpgrade = false; // Clear pending flag
        Debug.Log($"Wagon upgrade approved! Wagon workers: {ownedWagonWorkers}/{ownedWorkers} - Pending cleared: {!pendingWagonUpgrade}");
        UIManager.Instance.UpdateInfoText($"Worker upgraded to wagon! You now have {ownedWagonWorkers} wagon workers.");
        UIManager.Instance.UpdateWorkerText();
    }

    public void OnWagonUpgradeDenied(string reason)
    {
        pendingWagonUpgrade = false; // Clear pending flag
        Debug.LogError($"Wagon upgrade denied: {reason} - Pending cleared: {!pendingWagonUpgrade}");
        UIManager.Instance.UpdateInfoText($"Error: {reason}");
    }

    public int GetPurchasedWorkerCount()
    {
        return ownedWorkers;
    }

    public int GetAvailableWorkerCount()
    {
        return ownedWorkers - usedWorkers;
    }

    public int GetUsedWorkerCount()
    {
        return usedWorkers;
    }

    public void OnWorkerLostToAmbush()
    {
        ownedWorkers--;
        if (ownedWorkers < 0) ownedWorkers = 0; // Safety check
        Debug.Log($"Worker lost to ambush! Remaining workers: {ownedWorkers}");
        UIManager.Instance.UpdateWorkerText();
    }

    public void SetWorkerCountFromServer(int serverWorkerCount)
    {
        ownedWorkers = serverWorkerCount;
        Debug.Log($"Worker count synced from server: {ownedWorkers}");
        UIManager.Instance.UpdateWorkerText();
    }

    public void SetWorkerCountsFromServer(int serverWorkerCount, int serverWagonWorkerCount)
    {
        ownedWorkers = serverWorkerCount;
        ownedWagonWorkers = serverWagonWorkerCount;
        Debug.Log($"Worker counts synced from server: {ownedWorkers} total ({ownedWagonWorkers} wagons)");
        UIManager.Instance.UpdateWorkerText();
        UIManager.Instance.UpdateKingWagonUpgradeButtonText();
        UIManager.Instance.UpdateKingWagonPathButtonText();
    }

    public void RestorePurchasedWorkers(int workerCount, int wagonWorkers = 0)
    {
        ownedWorkers = workerCount;
        ownedWagonWorkers = wagonWorkers;
        usedWorkers = 0;
        usedWagonWorkers = 0;
        Debug.Log($"Workers restored: {ownedWorkers} owned ({ownedWagonWorkers} wagons), {usedWorkers} used, {GetAvailableWorkerCount()} available");
        UIManager.Instance.UpdateWorkerText();
    }

    public int GetAvailableWagonWorkerCount()
    {
        return ownedWagonWorkers - usedWagonWorkers;
    }

    public int GetAvailableRegularWorkerCount()
    {
        return (ownedWorkers - ownedWagonWorkers) - (usedWorkers - usedWagonWorkers);
    }

    public int GetTotalOwnedWagonWorkers()
    {
        return ownedWagonWorkers;
    }

    public int GetTotalOwnedRegularWorkers()
    {
        return ownedWorkers - ownedWagonWorkers;
    }

    // === AMBUSH BUYING SYSTEM FOR BANDIT ===

    private int GetHighestBanditResource()
    {
        return Mathf.Max(currentWood, currentGrain);
    }

    public bool CanBuyAmbush()
    {
        return GetHighestBanditResource() >= ambushCost && currentMode == InteractionMode.AmbushPlacement;
    }

    public string GetAmbushBuyButtonText()
    {
        if (CanBuyAmbush())
        {
            return "Buy Ambush";
        }
        else
        {
            string resourceType = currentWood >= currentGrain ? "Wood" : "Grain";
            return $"Need {resourceType}";
        }
    }

    public void BuyAmbush()
    {
        Debug.Log($"[InteractionManager] BuyAmbush() called - net is null: {net == null}");

        // Determine which resource to use (declare once at top of method)
        bool useWood = currentWood >= currentGrain;
        string resourceType = useWood ? "wood" : "grain";
        int currentAmount = useWood ? currentWood : currentGrain;

        Debug.Log($"[BuyAmbush] Resources: Wood={currentWood}, Grain={currentGrain} → Using {resourceType} (amount={currentAmount})");

        if (!CanBuyAmbush())
        {
            Debug.LogError($"❌ Error: Cannot buy ambush! Need {ambushCost} {resourceType}, have {currentAmount}");
            UIManager.Instance.UpdateInfoText($"Error: Need {ambushCost} {resourceType}, have {currentAmount}");
            return;
        }

        // If net is still null, try to get it again
        if (net == null)
        {
            Debug.LogWarning("[InteractionManager] net is null in BuyAmbush, trying to get NetworkManager.Instance");
            net = NetworkManager.Instance;
            Debug.Log($"[InteractionManager] NetworkManager.Instance found: {net != null}");
        }

        // Send request to server
        var payload = new { cost = ambushCost, resourceType = resourceType };
        net.Send("buy_ambush", payload);

        Debug.Log($"Ambush purchase request sent (Cost: {ambushCost} {resourceType})");

        // Server will respond with ambush_approved or ambush_denied
        // On approval, server will update our gold and we can place the ambush
    }

    public void OnAmbushPurchaseApproved()
    {
        purchasedAmbushes++;
        isInAmbushPlacementMode = true;

        Debug.Log($"✅ Ambush purchase approved! Can now place ambush #{purchasedAmbushes}");
        UIManager.Instance.UpdateInfoText($"Ambush approved! Click an edge to place it.");
    }

    public void OnAmbushPurchaseDenied(string reason)
    {
        Debug.LogError($"❌ Ambush purchase denied: {reason}");
        UIManager.Instance.UpdateInfoText($"Error: {reason}");
    }

    public void OnAmbushDeletionApproved()
    {
        if (pendingDeletionIndex < 0 || pendingDeletionIndex >= placedAmbushes.Count)
        {
            Debug.LogWarning("⚠️ Invalid pending deletion index, cannot complete deletion");
            pendingDeletionIndex = -1;
            return;
        }

        var ambush = placedAmbushes[pendingDeletionIndex];

        // Reset vertex colors for the ambush vertices
        UpdateVertexHighlightsForAmbushVertices(ambush.cornerA);
        UpdateVertexHighlightsForAmbushVertices(ambush.cornerB);

        // Destroy the visual orb object
        if (pendingDeletionIndex < ambushOrbObjects.Count && ambushOrbObjects[pendingDeletionIndex] != null)
        {
            Destroy(ambushOrbObjects[pendingDeletionIndex]);
            ambushOrbObjects.RemoveAt(pendingDeletionIndex);
        }

        // Remove the ambush data
        placedAmbushes.RemoveAt(pendingDeletionIndex);

        // Decrease purchased ambushes counter since this ambush is deleted
        if (purchasedAmbushes > 0)
        {
            purchasedAmbushes--;
        }

        Debug.Log($"✅ Ambush deletion approved! Ambush between vertices ({ambush.cornerA.Hex.Q},{ambush.cornerA.Hex.R}) and ({ambush.cornerB.Hex.Q},{ambush.cornerB.Hex.R}) deleted");
        
        // Reset pending deletion
        pendingDeletionIndex = -1;

        // Note: Resource updates are handled by server via 'resource_update' message
        // UI will be updated when UpdateResources() is called by NetworkManager
    }

    public void OnPathDeletionApproved()
    {
        if (pendingPathDeletionIndex < 0 || pendingPathDeletionIndex >= completedPaths.Count)
        {
            Debug.LogWarning("⚠️ Invalid pending path deletion index, cannot complete deletion");
            pendingPathDeletionIndex = -1;
            return;
        }

        var index = pendingPathDeletionIndex;
        var pathVerts = completedPaths[index];
        var isWagon = completedPathIsWagonWorker[index];
        var resourceField = completedPathResourceFields[index];

        // Reset vertex colors and hide edges
        foreach (var vertex in pathVerts)
        {
            var go = GridVisualsManager.Instance.GetVertexGameObject(vertex);
            if (go != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material.color = originalVertexColor;
            }
        }
        for (int i = 1; i < pathVerts.Count; i++)
        {
            if (pathVerts[i - 1].TryGetEdgeTo(pathVerts[i], out var e))
                GridVisualsManager.Instance.SetEdgeVisible(e, false);
        }

        // Refund worker usage
        usedWorkers = Mathf.Max(0, usedWorkers - 1);
        if (isWagon) usedWagonWorkers = Mathf.Max(0, usedWagonWorkers - 1);

        // Remove any worker object on this resource field (best-effort)
        var fieldCenter = resourceField.ToWorld(gridGen.hexRadius);
        var nearest = resourceFieldWorkers
            .Where(go => go != null)
            .OrderBy(go => Vector3.SqrMagnitude(go.transform.position - new Vector3(fieldCenter.x, go.transform.position.y, fieldCenter.z)))
            .FirstOrDefault();
        if (nearest != null)
        {
            resourceFieldWorkers.Remove(nearest);
            Destroy(nearest);
        }

        // Remove bookkeeping
        completedPaths.RemoveAt(index);
        completedPathResourceFields.RemoveAt(index);
        completedPathIsWagonWorker.RemoveAt(index);
        if (pathColorMap.ContainsKey(index)) pathColorMap.Remove(index);
        // Reindex colors
        pathColorMap = completedPaths
            .Select((p, i) => new { i, color = pathColors[i % pathColors.Length] })
            .ToDictionary(x => x.i, x => x.color);

        Debug.Log($"✅ Path deletion approved! Path #{index} (wagon: {isWagon}) deleted");
        
        // Reset pending deletion
        pendingPathDeletionIndex = -1;

        // Update UI
        UIManager.Instance.UpdateWorkerText();

        // Note: Resource updates are handled by server via 'resource_update' message
        // UI will be updated when UpdateResources() is called by NetworkManager
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
        // Allow undo from ReadyToConfirm by clicking the last vertex again
        if (pathCreationState == PathCreationState.ReadyToConfirm)
        {
            if (selectedVertices.Count > 0 && v.Equals(selectedVertices.Last()))
            {
                // Remove last point, revert edge visibility
                if (selectedVertices.Count >= 2)
                {
                    var beforeLast = selectedVertices.ElementAt(selectedVertices.Count - 2);
                    if (beforeLast.TryGetEdgeTo(v, out var lastEdge))
                        GridVisualsManager.Instance.SetEdgeVisible(lastEdge, false);
                }
                ToggleVertexHighlight(v); // unhighlight last
                selectedVertices.Remove(v);

                // If no vertices left, go back to selecting start corner
                if (selectedVertices.Count == 0)
                {
                    pathComplete = false;
                    pathCreationState = PathCreationState.SelectingStartVertex;
                    validNextVertices.Clear();
                    // Re-highlight available start vertices
                    foreach (var sv in availableStartVertices)
                        EnsureVertexHighlighted(sv);
                }
                else
                {
                    pathComplete = false;
                    pathCreationState = PathCreationState.Creating;
                    var last = selectedVertices.Last();
                    validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(last).Except(selectedVertices));
                }

                // Update UI since not ready anymore
                UIManager.Instance.UpdateKingPathButtonText();
                UIManager.Instance.UpdateKingPathConfirmButtonText();
                return;
            }
            // If clicked another vertex while ReadyToConfirm that's not the last, ignore
        }

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

            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(centralVertices));
            UpdateEndVertexHighlighting();

            Debug.Log($"✅ Start vertex selected: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");
            return;
        }

        if (pathCreationState != PathCreationState.Creating)
        {
            Debug.LogError("❌ Error: Not currently creating a path! Click 'Create Path' first.");
            return;
        }

        Debug.Log($"Vertex clicked: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");

        // If clicking the current last vertex again while creating, undo the last step
        if (selectedVertices.Count > 0 && v.Equals(selectedVertices.Last()))
        {
            // Hide the last edge if there was one
            if (selectedVertices.Count >= 2)
            {
                var beforeLast = selectedVertices.ElementAt(selectedVertices.Count - 2);
                if (beforeLast.TryGetEdgeTo(v, out var lastEdge))
                    GridVisualsManager.Instance.SetEdgeVisible(lastEdge, false);
            }

            // Unhighlight and remove the last vertex
            ToggleVertexHighlight(v);
            selectedVertices.Remove(v);

            // If we removed the start corner, go back to selecting start vertex
            if (selectedVertices.Count == 0)
            {
                pathComplete = false;
                pathCreationState = PathCreationState.SelectingStartVertex;
                validNextVertices.Clear();
                foreach (var sv in availableStartVertices)
                    EnsureVertexHighlighted(sv);
            }
            else
            {
                pathComplete = false;
                pathCreationState = PathCreationState.Creating;
                var last = selectedVertices.Last();
                validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(last).Except(selectedVertices));
            }

            // Update UI (not ready anymore)
            UIManager.Instance.UpdateKingPathButtonText();
            UIManager.Instance.UpdateKingPathConfirmButtonText();
            return;
        }

        if (!validNextVertices.Contains(v))
        {
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(selectedVertices.Last()));
            return;
        }

        var from = selectedVertices.Last();
        if (from.TryGetEdgeTo(v, out var edge))
            GridVisualsManager.Instance.SetEdgeVisible(edge, true);

        selectedVertices.Add(v);
        ToggleVertexHighlight(v);
        if (centralVertices.Contains(v))
        {
            pathComplete = true;
            pathCreationState = PathCreationState.ReadyToConfirm;
            Debug.Log("✅ Path completed! Ready to confirm.");

            // Auto-confirm the path immediately when completed
            ConfirmCurrentPath();
            return;
        }
        else if (centralVertices.Contains(v))
        {
            UIManager.Instance.UpdateInfoText("Path must be longer than 1 edge.");
        }
        var candidates = GetNeighborVertices(v).Except(selectedVertices);
        if (selectedVertices.Count < 3)
            candidates = candidates.Except(centralVertices);
        validNextVertices = new HashSet<HexVertex>(candidates);
        UpdateEndVertexHighlighting();
    }

    /// <summary>
    /// Highlightet/unhighlightet die Castle-End-Vertices abhängig von der aktuellen Pfadlänge.
    /// End-Vertices erst zeigen, wenn Pfad > 1 Kante (also >= 3 Vertices) hat.
    /// </summary>
    private void UpdateEndVertexHighlighting()
    {
        bool showEnds = pathCreationState == PathCreationState.Creating && selectedVertices.Count >= 3;
        foreach (var cv in centralVertices)
        {
            bool isHighlighted = highlightedVertices.Contains(cv);
            if (showEnds && !isHighlighted) ToggleVertexHighlight(cv);
            else if (!showEnds && isHighlighted) ToggleVertexHighlight(cv);
        }
    }
    
    void DeleteCompletedPath(int index)
    {
        if (index < 0 || index >= completedPaths.Count) return;

        var isWagon = completedPathIsWagonWorker[index];

        // Store the path index for deletion after server approval
        pendingPathDeletionIndex = index;

        Debug.Log($"🗑️ Requesting deletion of path #{index} (wagon: {isWagon})");

        // Send deletion request to server
        if (net == null) net = NetworkManager.Instance;
        var payload = new { 
            grainCost = workerGrainCost, 
            woodCost = workerWoodCost, 
            wagonWoodCost = wagonWoodCost,
            isWagonWorker = isWagon
        };
        net.Send("delete_path", payload);

        var totalWoodCost = workerWoodCost + (isWagon ? wagonWoodCost : 0);
        Debug.Log($"🗑️ Path deletion request sent (Refund: {workerGrainCost} grain + {totalWoodCost} wood)");
    }

    void DeleteAmbush(int index)
    {
        if (index < 0 || index >= placedAmbushes.Count) return;

        var ambush = placedAmbushes[index];

        // Store the ambush data for deletion after server approval
        pendingDeletionIndex = index;

        // Determine which resource type should be refunded (same logic as buying)
        bool useWood = currentWood >= currentGrain;
        string resourceType = useWood ? "wood" : "grain";

        Debug.Log($"🗑️ Requesting deletion of ambush between vertices ({ambush.cornerA.Hex.Q},{ambush.cornerA.Hex.R}) and ({ambush.cornerB.Hex.Q},{ambush.cornerB.Hex.R})");

        // Send deletion request to server
        if (net == null) net = NetworkManager.Instance;
        var payload = new { cost = ambushCost, resourceType = resourceType };
        net.Send("delete_ambush", payload);

        Debug.Log($"🗑️ Ambush deletion request sent (Refund: {ambushCost} {resourceType})");
    }

    public bool SubmitPath()
    {
        if (currentMode != InteractionMode.PathSelection)
        {
            Debug.LogError("❌ Error: Not in path selection mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in path selection mode!");
            return false;
        }

        if (completedPaths.Count == 0)
        {
            Debug.LogError("❌ Error: No paths created!");
            UIManager.Instance.UpdateInfoText("Error: No paths created!");
            return false;
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
                resourceType = resourceType,
                isWagonWorker = completedPathIsWagonWorker[index]
            };
        }).ToArray();

        var pathData = new PlaceWorkersPayload { paths = serializablePaths };

        // Ensure net is available
        if (net == null) net = NetworkManager.Instance;

        Debug.Log($"Sending {completedPaths.Count} paths to server");
        net.Send("place_workers", pathData);
        DisableInteraction();

        // Store resource field data for bandit visibility (without revealing routes)
        submittedResourceFields.Clear();
        submittedPathIsWagonWorker.Clear();
        for (int i = 0; i < completedPathResourceFields.Count; i++)
        {
            submittedResourceFields.Add(completedPathResourceFields[i]);
            submittedPathIsWagonWorker.Add(completedPathIsWagonWorker[i]);
        }

        // Workers will remain visible until execution phase starts
        // They are hidden later when actual movement begins

        ResetAllVertexHighlights();
        return true;
    }

    public void ExecuteServerPath(List<HexVertex> path)
    {
        // Hide resource field workers since execution is now starting
        foreach (var worker in resourceFieldWorkers)
        {
            if (worker != null) worker.SetActive(false);
        }

        serverPathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();
        serverPathVertices = path;

        if (!serverPathWorld.Any()) return;

        for (int i = 0; i < serverPathWorld.Count; i++)
        {
            serverPathWorld[i] = new Vector3(serverPathWorld[i].x, serverPathWorld[i].y + 0.35f, serverPathWorld[i].z);
        }

        // Recreate worker if it was destroyed by ambush
        if (workerObj == null)
        {
            workerObj = Instantiate(workerPrefab);
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

        float stepDist = workerSpeed * Time.deltaTime;
        var next = Vector3.MoveTowards(curr, targ, stepDist);

        // Swept collision along segment [curr -> next]
        if (CheckAmbushCollisionAlongSegment(curr, next))
        {
            isMoving = false;
            Destroy(workerObj);
            workerObj = null;
            Debug.Log("🪦 Legacy worker destroyed by ambush (swept)!");
            return;
        }

        if (pathStep > 0 && pathStep < serverPathVertices.Count)
        {
            var prevV = serverPathVertices[pathStep - 1];
            var currV = serverPathVertices[pathStep];
            if (prevV.TryGetEdgeTo(currV, out var edge))
                GridVisualsManager.Instance.SetEdgeVisible(edge, true);
        }

        if (Vector3.Distance(next, targ) < 0.01f)
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
            workerObj.transform.position = next;
        }
    }

    void MoveWorker(int workerIndex)
    {
        if (workerIndex >= workerObjects.Count || workerIndex >= allServerPathsWorld.Count) return;
        if (workerObjects[workerIndex] == null) return; // Worker was destroyed by ambush
        if (workerPathSteps[workerIndex] >= allServerPathsWorld[workerIndex].Count)
        {
            workerMovingStates[workerIndex] = false;
            return;
        }

        var workerObj = workerObjects[workerIndex];
        var pathWorld = allServerPathsWorld[workerIndex];
        var curr = workerObj.transform.position;
        var targ = pathWorld[workerPathSteps[workerIndex]];

        float stepDist = workerSpeed * Time.deltaTime;
        var next = Vector3.MoveTowards(curr, targ, stepDist);

        // Swept collision along segment [curr -> next]
        if (CheckAmbushCollisionAlongSegment(curr, next))
        {
            workerMovingStates[workerIndex] = false;
            Destroy(workerObj);
            Debug.Log($"🪦 Worker {workerIndex} destroyed by ambush!");

            // Mark this worker slot as destroyed (keep the index but null the object)
            // This prevents index shifting issues during the Update loop
            workerObjects[workerIndex] = null;

            return;
        }

        int step = workerPathSteps[workerIndex];
        var verts = allServerPathsVertices[workerIndex];
        if (step > 0 && step < verts.Count)
        {
            var prevV = verts[step - 1];
            var currV = verts[step];
            if (prevV.TryGetEdgeTo(currV, out var edge))
                GridVisualsManager.Instance.SetEdgeVisible(edge, true);
        }

        if (Vector3.Distance(next, targ) < 0.01f)
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
            workerObj.transform.position = Vector3.MoveTowards(next, targ, workerSpeed * Time.deltaTime);
        }
    }

    public void ExecuteServerPaths(List<List<HexVertex>> paths)
    {
        // Hide resource field workers since execution is now starting
        foreach (var worker in resourceFieldWorkers)
        {
            if (worker != null) worker.SetActive(false);
        }

        // Clear existing workers
        foreach (var worker in workerObjects)
        {
            if (worker != null) Destroy(worker);
        }
        workerObjects.Clear();
        allServerPathsWorld.Clear();
        allServerPathsVertices.Clear();
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

            allServerPathsVertices.Add(path);

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
                    Debug.Log($"❌ AMBUSH COLLISION DETECTED! Worker caught in ambush! ({GameManager.Instance?.MyRole})");
                    Destroy(animationAmbushOrbObjects[i]);
                    animationAmbushOrbObjects.RemoveAt(i);

                    // Legacy worker system - destroy the worker completely
                    if (isMoving && workerObj != null)
                    {
                        isMoving = false;
                        Destroy(workerObj);
                        workerObj = null;
                        Debug.Log("🪦 Legacy worker destroyed by ambush!");
                        return;
                    }

                    // Multi-worker system collision handling is done in MoveWorker method
                    return;
                }
            }
        }
    }

    // Prüft, ob das Bewegungssegment [from -> to] eine Ambush-Kugel schneidet.
    // Nutzt die bereits vorhandene DistancePointSegmentXZ(...).
    private bool CheckAmbushCollisionAlongSegment(Vector3 from, Vector3 to)
    {
        for (int i = animationAmbushOrbObjects.Count - 1; i >= 0; i--)
        {
            var orb = animationAmbushOrbObjects[i];
            if (orb == null) continue;

            Vector3 orbPos = orb.transform.position;
            // Distanz der Kugelmitte zum Bewegungssegment im XZ
            float d = DistancePointSegmentXZ(orbPos, from, to);
            if (d <= ambushHitRadius)
            {
                // Treffer → Orb entfernen und Kollision melden
                Destroy(orb);
                animationAmbushOrbObjects.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    IEnumerable<HexVertex> GetNeighborVertices(HexVertex v)
        => v.GetAdjacentHexes()
            .SelectMany(hex => Enum.GetValues(typeof(HexDirection)).Cast<HexDirection>().Select(dir => new HexEdge(hex, dir)))
            .Where(e => e.GetVertexEndpoints().Contains(v))
            .SelectMany(e => e.GetVertexEndpoints())
            .Where(x => !x.Equals(v))
            .Distinct();

    // === AMBUSH HANDLING ===
    
    /* OLD VERTEX-BASED AMBUSH SYSTEM - COMMENTED OUT AS BACKUP
    void HandleAmbushClick(HexVertex v)
    {
        if (!isInAmbushPlacementMode)
        {
            Debug.LogError("❌ Error: No ambush purchased! Click 'Buy Ambush' first.");
            return;
        }

        var endpoints = e.GetVertexEndpoints();
        var vA = endpoints[0];
        var vB = endpoints[1];

        // If clicking the same vertex again, deselect it (toggle behavior like King's path)
        if (ambushStart.Equals(v))
        {
            UpdateVertexHighlightsForAmbushVertices(ambushStart);
            ambushStart = default;
            Debug.Log($"✅ Ambush start deselected at vertex: ({v.Hex.Q},{v.Hex.R},{v.Direction})");
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

        // Check if an ambush already exists at this location
        var existingIndex = placedAmbushes.FindIndex(a =>
            (a.cornerA.Equals(ambushStart) && a.cornerB.Equals(v)) ||
            (a.cornerA.Equals(v) && a.cornerB.Equals(ambushStart)));

        if (existingIndex >= 0)
        {
            Debug.LogError($"❌ Error: An ambush already exists between these vertices! Use right-click to delete existing ambushes.");
            UpdateVertexHighlightsForAmbushVertices(ambushStart);
            ambushStart = default;
            return;
        }

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
        
        // Add AmbushOrbMarker component for right-click detection
        if (orbGO.GetComponent<AmbushOrbMarker>() == null)
        {
            orbGO.AddComponent<AmbushOrbMarker>();
        }
        
        // Ensure the orb has a collider for mouse interaction
        if (orbGO.GetComponent<Collider>() == null)
        {
            var collider = orbGO.AddComponent<SphereCollider>();
            collider.radius = 0.5f; // Adjust size as needed
        }
        
        ambushOrbObjects.Add(orbGO);

        EnsureVertexHighlighted(v);
        Debug.Log($"✅ Ambush created between vertices ({ambushStart.Hex.Q},{ambushStart.Hex.R}) and ({v.Hex.Q},{v.Hex.R})");

        // Reset for next ambush placement
        ambushStart = default;

        // Only disable placement mode if we've used up all purchased ambushes
        if (placedAmbushes.Count >= purchasedAmbushes)
        {
            isInAmbushPlacementMode = false;
            if (hasHoverEdge) { GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false); hasHoverEdge = false; }
            GridVisualsManager.Instance.HideAllEdges();
            Debug.Log($"✅ All purchased ambushes ({purchasedAmbushes}) have been placed. Placement mode disabled.");
        }
    }
    END OF OLD VERTEX-BASED SYSTEM */

    // NEW EDGE-BASED AMBUSH SYSTEM WITH HOVER PREVIEW
    
    private GameObject currentPreviewOrb; // The transparent preview orb
    private List<GameObject> ambushEdgeColliders = new(); // Invisible edge colliders for hover detection
    
    public void OnAmbushEdgeHoverEnter(HexVertex vertexA, HexVertex vertexB)
    {
        // Only show preview if bandit is in ambush placement mode and has ambushes left to place
        if (currentMode != InteractionMode.AmbushPlacement) return;
        if (!isInAmbushPlacementMode) return;
        if (placedAmbushes.Count >= purchasedAmbushes) return; // No more ambushes to place
        
        // Check if an ambush already exists at this location
        var existingIndex = placedAmbushes.FindIndex(a =>
            (a.cornerA.Equals(vertexA) && a.cornerB.Equals(vertexB)) ||
            (a.cornerA.Equals(vertexB) && a.cornerB.Equals(vertexA)));
            
        if (existingIndex >= 0) return; // Don't show preview if ambush already exists
        
        ShowAmbushPreview(vertexA, vertexB);
    }
    
    public void OnAmbushEdgeHoverExit(HexVertex vertexA, HexVertex vertexB)
    {
        HideAmbushPreview();
    }
    
    public void OnAmbushEdgeLeftClick(HexVertex vertexA, HexVertex vertexB)
    {
        // Only place ambush if in placement mode and have ambushes left to place
        if (currentMode != InteractionMode.AmbushPlacement) return;
        if (!isInAmbushPlacementMode) return;
        if (placedAmbushes.Count >= purchasedAmbushes) return; // No more ambushes to place
        
        PlaceAmbushOnEdge(vertexA, vertexB);
    }
    
    public void OnAmbushEdgeRightClick(HexVertex vertexA, HexVertex vertexB)
    {
        // Only delete if NOT in placement mode (same logic as before)
        if (currentMode != InteractionMode.AmbushPlacement) return;
        if (isInAmbushPlacementMode) return;
        
        // Find existing ambush at this location
        var existingIndex = placedAmbushes.FindIndex(a =>
            (a.cornerA.Equals(vertexA) && a.cornerB.Equals(vertexB)) ||
            (a.cornerA.Equals(vertexB) && a.cornerB.Equals(vertexA)));
            
        if (existingIndex >= 0)
        {
            DeleteAmbush(existingIndex);
        }
    }
    
    private void ShowAmbushPreview(HexVertex vertexA, HexVertex vertexB)
    {
        HideAmbushPreview(); // Remove any existing preview
        
        if (ambushOrb == null) return;
        
        var aPos = vertexA.ToWorld(gridGen.hexRadius);
        var bPos = vertexB.ToWorld(gridGen.hexRadius);
        var midPoint = (aPos + bPos) * 0.5f;
        midPoint.y = 0;
        
        Vector3 dir = bPos - aPos;
        dir.y = 0f;
        
        Quaternion rot;
        if (dir.sqrMagnitude <= 1e-6f)
            rot = Quaternion.identity;
        else
            rot = Quaternion.LookRotation(dir);
            
        currentPreviewOrb = Instantiate(ambushOrb, midPoint, rot);
        
        // Make it transparent/ghostly
        var renderers = currentPreviewOrb.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                // Set transparency to create ghost effect
                if (material.HasProperty("_Color"))
                {
                    var color = material.color;
                    color.a = 0.5f; // 50% transparency
                    material.color = color;
                }
                
                // Enable transparency if not already
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 2); // Fade mode
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                }
            }
        }
        
        Debug.Log($"✅ Showing ambush preview between vertices ({vertexA.Hex.Q},{vertexA.Hex.R}) and ({vertexB.Hex.Q},{vertexB.Hex.R})");
    }
    
    private void HideAmbushPreview()
    {
        if (currentPreviewOrb != null)
        {
            Destroy(currentPreviewOrb);
            currentPreviewOrb = null;
        }
    }
    
    private void PlaceAmbushOnEdge(HexVertex vertexA, HexVertex vertexB)
    {
        // Check if an ambush already exists at this location
        var existingIndex = placedAmbushes.FindIndex(a =>
            (a.cornerA.Equals(vertexA) && a.cornerB.Equals(vertexB)) ||
            (a.cornerA.Equals(vertexB) && a.cornerB.Equals(vertexA)));
            
        if (existingIndex >= 0)
        {
            Debug.LogError($"❌ Error: An ambush already exists between these vertices!");
            return;
        }
        
        if (placedAmbushes.Count >= maxAmbushes)
        {
            Debug.LogError($"❌ Error: Maximum ambushes ({maxAmbushes}) already placed!");
            return;
        }
        
        if (ambushOrb == null)
        {
            Debug.LogError("❌ Error: ambushOrb prefab is null!");
            return;
        }
        
        // Hide preview since we're placing the real ambush
        HideAmbushPreview();
        
        var ambushEdge = new NetworkingDTOs.AmbushEdge { cornerA = vertexA, cornerB = vertexB };
        placedAmbushes.Add(ambushEdge);
        
        var aPos = vertexA.ToWorld(gridGen.hexRadius);
        var bPos = vertexB.ToWorld(gridGen.hexRadius);
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
        
        // Add AmbushOrbMarker component for right-click detection (for individual orb deletion)
        if (orbGO.GetComponent<AmbushOrbMarker>() == null)
        {
            orbGO.AddComponent<AmbushOrbMarker>();
        }
        
        // Ensure the orb has a collider for mouse interaction
        if (orbGO.GetComponent<Collider>() == null)
        {
            var collider = orbGO.AddComponent<SphereCollider>();
            collider.radius = 0.5f;
        }
        
        ambushOrbObjects.Add(orbGO);
        
        // Don't highlight vertices - they should stay red (original color)
        // EnsureVertexHighlighted(vertexA);
        // EnsureVertexHighlighted(vertexB);
        
        Debug.Log($"✅ Ambush placed between vertices ({vertexA.Hex.Q},{vertexA.Hex.R}) and ({vertexB.Hex.Q},{vertexB.Hex.R})");
        
        // Only disable placement mode if we've placed all purchased ambushes
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

    public bool FinalizeAmbushes()
    {
        if (currentMode != InteractionMode.AmbushPlacement)
        {
            Debug.LogError("❌ Error: Not in ambush placement mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in ambush placement mode!");
            return false;
        }

        if (placedAmbushes.Count == 0)
        {
            Debug.LogError("❌ Error: No ambushes placed!");
            UIManager.Instance.UpdateInfoText("Error: No ambushes placed!");
            return false;
        }

        var serializableAmbushes = placedAmbushes.Select(a => new SerializableAmbushEdge(a)).ToArray();
        var payload = new PlaceAmbushesPayload { ambushes = serializableAmbushes };

        // Ensure net is available
        if (net == null) net = NetworkManager.Instance;

        try
        {
            net.Send("place_ambushes", payload);
            Debug.Log($"✅ Sent {placedAmbushes.Count} ambushes to server");
            DisableInteraction();
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error: Failed to send ambushes to server: {e.Message}");
            UIManager.Instance.UpdateInfoText($"Error: Failed to send ambushes to server: {e.Message}");
            return false;
        }

        ResetVertexHighlightsKeepAmbushVertices();
        return true;
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

            var aPos = ambush.cornerA.ToWorld(gridGen.hexRadius);
            var bPos = ambush.cornerB.ToWorld(gridGen.hexRadius);

            var midPoint = (aPos + bPos) / 2f;
            midPoint.y = 0f;

            Vector3 dir = bPos - aPos;
            dir.y = 0f;

            Quaternion rot;
            if (midPoint.sqrMagnitude <= 1e-6f)
                rot = Quaternion.identity;
            else
                rot = Quaternion.LookRotation(dir);

            var orbGO = Instantiate(ambushOrb, midPoint, rot);
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
        // Hide any preview orb
        HideAmbushPreview();
        
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

        // Keep resource field workers visible during turn transitions
        // They will be cleaned up only when execution starts or game resets

        pathCreationState = PathCreationState.NotCreating;
        currentPathIndex = -1;
        isInAmbushPlacementMode = false;
        pendingWorkerPurchase = false; // Reset pending worker purchase flag
        pendingWagonUpgrade = false; // Reset pending wagon upgrade flag

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

        if (hasHoverEdge) { GridVisualsManager.Instance.SetEdgeVisible(hoverEdge, false); hasHoverEdge = false; }
        GridVisualsManager.Instance.HideAllEdges();
    }

    public void ForceCompleteReset()
    {
        selectedVertices.Clear();
        validNextVertices.Clear();
        availableStartVertices.Clear();
        selectedResourceField = default;
        pathComplete = false;
        isMoving = false;

        // Check if workerObj still exists before trying to access it
        if (workerObj != null && workerObj)
        {
            workerObj.SetActive(false);
        }

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

        GridVisualsManager.Instance.HideAllEdges();

        // Clean up resource field workers
        foreach (var worker in resourceFieldWorkers)
        {
            if (worker != null)
            {
                Destroy(worker);
            }
        }
        resourceFieldWorkers.Clear();

        // Reset vertex colors BEFORE clearing completedPaths
        ResetAllVertexHighlights();
        ResetAllVertexColorsToOriginal();

        pathCreationState = PathCreationState.NotCreating;
        currentPathIndex = -1;
        completedPaths.Clear();
        completedPathResourceFields.Clear();
        completedPathIsWagonWorker.Clear();
        pathColorMap.Clear();
        purchasedAmbushes = 0;
        isInAmbushPlacementMode = false;
        pendingDeletionIndex = -1;
        pendingPathDeletionIndex = -1;
        ownedWorkers = 0;
        usedWorkers = 0;
        ownedWagonWorkers = 0;
        usedWagonWorkers = 0;
        currentPathUseWagonWorker = false;

        // Clear bandit visibility data
        submittedResourceFields.Clear();
        submittedPathIsWagonWorker.Clear();

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

        // Clean up edge colliders for ambush system
        foreach (var edgeCollider in ambushEdgeColliders)
        {
            if (edgeCollider != null) Destroy(edgeCollider);
        }
        ambushEdgeColliders.Clear();

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

        GridVisualsManager.Instance.HideAllEdges();

        // Also clean up legacy worker
        if (workerObj != null)
        {
            workerObj.SetActive(false);
        }
        isMoving = false;

        Debug.Log($"Round cleanup completed for {GameManager.Instance?.MyRole}");
    }

    /// <summary>
    /// Complete reset for a new game session
    /// </summary>
    public void ResetForNewGame()
    {
        Debug.Log("[InteractionManager] ResetForNewGame() - Resetting interaction state");

        // Reset interaction state
        pathCreationState = PathCreationState.NotCreating;
        completedPaths.Clear();
        currentPathIndex = -1;

        // Reset mode to None - will be set again when role is assigned
        currentMode = InteractionMode.None;

        // Reset resources
        currentGold = 0;
        currentWood = 0;
        currentGrain = 0;

        // Reset workers
        ownedWorkers = 0;
        usedWorkers = 0;
        ownedWagonWorkers = 0;
        usedWagonWorkers = 0;

        // Reset ambush state
        purchasedAmbushes = 0;
        isInAmbushPlacementMode = false;
        pendingDeletionIndex = -1;
        pendingPathDeletionIndex = -1;
        pendingWorkerPurchase = false; // Reset pending worker purchase flag
        pendingWagonUpgrade = false; // Reset pending wagon upgrade flag

        // Reset path creation state
        pathComplete = false;
        currentPathUseWagonWorker = false;
        selectedResourceField = default(Hex);

        // Clear visual elements
        HideAmbushPreview(); // Clean up any preview materials
        ForceCompleteReset();

        // Clear collections
        resourceFieldWorkers.Clear();
        placedAmbushes.Clear();
        ambushOrbObjects.Clear();
        animationAmbushOrbObjects.Clear();
        ambushEdges.Clear();
        selectedVertices.Clear();
        validNextVertices.Clear();
        workerPathSteps.Clear();

        // Reset movement state
        isMoving = false;
        pathStep = 0;
        serverPathWorld?.Clear();

        // Destroy worker object
        if (workerObj != null && workerObj)
        {
            Destroy(workerObj);
            workerObj = null;
        }

        Debug.Log("[InteractionManager] ResetForNewGame() - Reset complete");
    }
}
