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

    // Workers sitting on resource fields (visible when paths are created)
    private List<GameObject> resourceFieldWorkers = new();

    // Stored path data for bandit visibility (resource fields only, no routes)
    private List<Hex> submittedResourceFields = new();
    private List<bool> submittedPathIsWagonWorker = new();

    public Material ambushLineMaterial;
    public int maxAmbushes = 5;

    // Path colors for multiple paths
    public Color[] pathColors = { Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };

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

    // King resources and worker buying system
    private int currentGrain = 0; // Will be updated from server
    private int currentWood = 0; // Will be updated from server
    private const int workerGrainCost = 20;
    private const int workerWoodCost = 8;
    private const int wagonWoodCost = 25;
    private int ownedWorkers = 0;
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
    }

    public void EnableInteraction(PlayerRole role)
    {
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit)
        {
            currentMode = InteractionMode.AmbushPlacement;
            ShowWorkersForBandit();
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
        return currentGrain >= workerGrainCost && currentWood >= workerWoodCost && currentMode == InteractionMode.PathSelection;
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

        // Send buy request to server
        var payload = new { grainCost = workerGrainCost, woodCost = workerWoodCost };
        net.Send("buy_worker", payload);

        Debug.Log($"Worker purchase request sent (Cost: {workerGrainCost} grain, {workerWoodCost} wood)");

        // Server will respond with worker_approved or worker_denied
        // On approval, server will update our resources and we can place the worker
    }

    public void OnWorkerPurchaseApproved()
    {
        ownedWorkers++;
        Debug.Log($"Worker purchase approved! Total workers: {ownedWorkers}");
        UIManager.Instance.UpdateInfoText($"Worker purchased! You now have {ownedWorkers - usedWorkers} available workers.");
        UIManager.Instance.UpdateWorkerText();
    }

    public void OnWorkerPurchaseDenied(string reason)
    {
        Debug.LogError($"Worker purchase denied: {reason}");
        UIManager.Instance.UpdateInfoText($"Error: {reason}");
    }

    // Wagon Worker Management
    public bool CanUpgradeWorkerToWagon()
    {
        return currentWood >= wagonWoodCost && (ownedWorkers - ownedWagonWorkers) > 0;
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

        var payload = new { woodCost = wagonWoodCost };
        net.Send("upgrade_worker_wagon", payload);

        Debug.Log($"Wagon upgrade request sent (Cost: {wagonWoodCost} wood)");
        // Server will respond with wagon_upgrade_approved or wagon_upgrade_denied
    }

    public void OnWagonUpgradeApproved(int wagonWorkers, int totalWorkers)
    {
        ownedWorkers = totalWorkers;
        ownedWagonWorkers = wagonWorkers;
        Debug.Log($"Wagon upgrade approved! Wagon workers: {ownedWagonWorkers}/{ownedWorkers}");
        UIManager.Instance.UpdateInfoText($"Worker upgraded to wagon! You now have {ownedWagonWorkers} wagon workers.");
        UIManager.Instance.UpdateWorkerText();
    }

    public void OnWagonUpgradeDenied(string reason)
    {
        Debug.LogError($"Wagon upgrade denied: {reason}");
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
            Debug.LogError("❌ Error: Not currently creating a path! Click 'Create Path' first.");
            return;
        }

        Debug.Log($"Vertex clicked: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");

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

            // Update UI buttons when path is ready to confirm
            UIManager.Instance.UpdateKingPathButtonText();
            UIManager.Instance.UpdateKingPathConfirmButtonText();
        }
        validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(selectedVertices));
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

        // Kollisionserkennung - wir speichern die Anzahl der Orbs vor der Prüfung
        int orbCountBefore = animationAmbushOrbObjects.Count;
        CheckCollisionWithOrbs(curr);
        int orbCountAfter = animationAmbushOrbObjects.Count;

        // Wenn eine Kollision erkannt wurde (eine Kugel wurde entfernt), Worker komplett zerstören
        if (orbCountAfter < orbCountBefore)
        {
            isMoving = false;
            Destroy(workerObj);
            workerObj = null;
            Debug.Log("🪦 Legacy worker destroyed by ambush!");
            return;
        }

        if (pathStep > 0 && pathStep < serverPathVertices.Count)
        {
            var prevV = serverPathVertices[pathStep - 1];
            var currV = serverPathVertices[pathStep];
            if (prevV.TryGetEdgeTo(currV, out var edge))
                GridVisualsManager.Instance.SetEdgeVisible(edge, true);
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

        // Kollisionserkennung - wir speichern die Anzahl der Orbs vor der Prüfung
        int orbCountBefore = animationAmbushOrbObjects.Count;
        CheckCollisionWithOrbs(curr);
        int orbCountAfter = animationAmbushOrbObjects.Count;

        // Wenn eine Kollision erkannt wurde (eine Kugel wurde entfernt), Worker komplett entfernen
        if (orbCountAfter < orbCountBefore)
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

        // Reset path creation state
        pathComplete = false;
        currentPathUseWagonWorker = false;
        selectedResourceField = default(Hex);

        // Clear visual elements
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
