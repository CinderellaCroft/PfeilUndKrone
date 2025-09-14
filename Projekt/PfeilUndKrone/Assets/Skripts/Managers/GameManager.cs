using System.Collections.Generic;
using System.Linq;
using NetworkingDTOs;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public enum PlayerRole { None, King, Bandit }
public enum GameTurn { Setup, KingPlanning, BanditPlanning, Executing }

public class GameManager : Singleton<GameManager>
{

    [SerializeField] NetworkServiceBase networkService;

    public HexGridGenerator gridGenerator;
    public GridVisualsManager visualsManager;
    public InteractionManager interactionManager;

    private Dictionary<Hex, FieldType> resourceMap;

    private int currentRoundNumber = 0; // Track current round

    public bool IsGameOver { get; private set; } = false;

    public PlayerRole MyRole { get; private set; } = PlayerRole.None;
    public GameTurn CurrentTurn { get; private set; } = GameTurn.Setup;

    private bool subscribed = false;

    public bool quitGameCalled = false;

    protected override void Awake()
    {
        Debug.Log("[GameManager] Awake() START");
        try
        {
            base.Awake(); // This sets up the Singleton Instance
            Debug.Log($"[GameManager] base.Awake() completed - Instance is now available: {Instance != null}");
            Debug.Log($"[GameManager] Current scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            Debug.Log("[GameManager] Awake() COMPLETED SUCCESSFULLY");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] Awake() FAILED with exception: {e.Message}");
            Debug.LogError($"[GameManager] Stack trace: {e.StackTrace}");
        }
    }

    void OnEnable()
    {
        Debug.Log("[GameManager] OnEnable() called");
        if (!subscribed)
        {
            // Use existing NetworkManager if available, otherwise use assigned networkService
            if (NetworkManager.Instance != null)
            {
                Debug.Log("GameManager: Using existing NetworkManager instance");
                networkService = NetworkManager.Instance;
            }

            networkService.OnRoleAssigned += SetRole;
            networkService.OnGridDataReady += OnGridReady;
            networkService.OnResourceMapReceived += OnResourceMap;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Check if role was already assigned before we subscribed
            // This happens when transitioning from title to main scene
            Debug.Log($"[GameManager] Checking if role already assigned. Current role: {MyRole}");
            if (MyRole == PlayerRole.None)
            {
                Debug.Log("[GameManager] Role is None - getting role from TitleSceneManager");
                string storedRole = TitleSceneManager.GetAssignedRole();
                if (!string.IsNullOrEmpty(storedRole))
                {
                    Debug.Log($"[GameManager] Found stored role: {storedRole} - calling SetRole");
                    SetRole(storedRole);
                }
                else
                {
                    Debug.LogWarning("[GameManager] No stored role found - role will remain None");
                    Debug.LogWarning("[GameManager] This means the role will need to come from OnRoleAssigned event later");
                }
            }
            else
            {
                Debug.Log($"[GameManager] Role already set to: {MyRole} - not changing");
            }

            // Only connect if not already connected
            if (!networkService.IsConnected)
            {
                Debug.Log("GameManager: NetworkService not connected, attempting to connect");
                this.networkService.Connect();
            }
            else
            {
                Debug.Log("GameManager: NetworkService already connected");
            }

            subscribed = true;
        }
    }

    void Start()
    {
        Debug.Log("[GameManager] Start() called");
        Debug.Log($"[GameManager] Instance in Start: {Instance != null}");
    }


    void OnDisable()
    {
        networkService.OnRoleAssigned -= SetRole;
        networkService.OnGridDataReady -= OnGridReady;
        networkService.OnResourceMapReceived -= OnResourceMap;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode x)
    {
        if (s.name == "Main")
        {
            Debug.Log("GameManager: Main scene loaded");
            // Don't connect again if already connected
            if (!networkService.IsConnected)
            {
                Debug.Log("GameManager: Main scene - connecting to network");
                _ = networkService.Connect();
            }
            else
            {
                Debug.Log("GameManager: Main scene - already connected to network");
            }
        }
    }

    public void EndGame()
    {
        Debug.Log($"[GameManager] EndGame() CALLED - Current Role: {MyRole}");
        Debug.LogWarning("[GameManager] STACK TRACE for EndGame call:");
        Debug.LogWarning(System.Environment.StackTrace);

        _ = this.networkService.Disconnect();
        IsGameOver = true;

        // Perform comprehensive cleanup
        ResetGameState();

        Debug.Log("[GameManager] EndGame() - Complete game state reset for next game");
    }

    /// <summary>
    /// Comprehensive reset of all game state for a fresh new game
    /// </summary>
    public void ResetGameState()
    {
        Debug.Log($"[GameManager] ResetGameState() CALLED - Current Role: {MyRole}");
        Debug.LogWarning("[GameManager] STACK TRACE for ResetGameState call:");
        Debug.LogWarning(System.Environment.StackTrace);

        // Reset core game state
        MyRole = PlayerRole.None;
        CurrentTurn = GameTurn.Setup;
        currentRoundNumber = 0;
        IsGameOver = false;
        resourceMap?.Clear();
        quitGameCalled = false;

        // Reset managers
        if (visualsManager != null) visualsManager.ResetForNewGame();
        if (interactionManager != null) interactionManager.ResetForNewGame();
        if (UIManager.Instance != null) UIManager.Instance.ResetForNewGame();

        Debug.Log("[GameManager] ResetGameState() - All systems reset for new game");
    }

    /// <summary>
    /// Reset only game state without touching UI (for title scene cleanup)
    /// </summary>
    public void ResetGameStateOnly()
    {
        Debug.Log($"[GameManager] ResetGameStateOnly() CALLED - Current Role: {MyRole}");
        Debug.LogWarning("[GameManager] STACK TRACE for ResetGameStateOnly call:");
        Debug.LogWarning(System.Environment.StackTrace);

        // Reset core game state
        MyRole = PlayerRole.None;
        CurrentTurn = GameTurn.Setup;
        currentRoundNumber = 0;
        IsGameOver = false;
        resourceMap?.Clear();

        // Reset non-UI managers only
        if (visualsManager != null) visualsManager.ResetForNewGame();
        if (interactionManager != null) interactionManager.ResetForNewGame();

        Debug.Log("[GameManager] ResetGameStateOnly() - Core systems reset complete");
    }

    void SetRole(string roleName)
    {
        Debug.Log($"[GameManager] SetRole called with: '{roleName}', current role: {MyRole}");
        Debug.Log($"[GameManager] SetRole - this.GetHashCode(): {this.GetHashCode()}");
        Debug.Log($"[GameManager] SetRole - Instance.GetHashCode(): {Instance.GetHashCode()}");
        Debug.Log($"[GameManager] SetRole - Are they the same instance? {this == Instance}");

        if (roleName == PlayerRole.King.ToString()) MyRole = PlayerRole.King;
        else if (roleName == PlayerRole.Bandit.ToString()) MyRole = PlayerRole.Bandit;
        else Debug.Log($"Unknown role: '{roleName}', role remains: {MyRole}");

        Debug.Log($"[GameManager] Role set to: {MyRole} - Initializing fresh game session");

        // Perform fresh initialization when role is assigned (start of new game)
        InitializeFreshGame();

        UIManager.Instance.UpdateRoleText(MyRole);
    }

    /// <summary>
    /// Initialize a completely fresh game session
    /// </summary>
    private void InitializeFreshGame()
    {
        Debug.Log("[GameManager] InitializeFreshGame() - Setting up fresh game state");

        // Ensure we start with clean state
        IsGameOver = false;
        CurrentTurn = GameTurn.Setup;
        currentRoundNumber = 0;

        // Clear any existing resource map
        resourceMap?.Clear();

        // Reset subscriptions flag to allow re-subscription
        subscribed = false;

        Debug.Log("[GameManager] InitializeFreshGame() - Fresh game state ready");
    }

    void OnGridReady()
    {
        gridGenerator.GenerateGrid();
    }


    void OnResourceMap(List<ResourceData> mapData)
    {
        var counts = new Dictionary<FieldType, int>();
        var dupes = new List<string>();
        resourceMap = new Dictionary<Hex, FieldType>();

        // Debug.Log("##########################\nONRESOURCEMAP\n####################");

        foreach (var rd in mapData)
        {
            var hex = new Hex(rd.q, rd.r);

            // count by resource type
            counts[rd.resource] = counts.TryGetValue(rd.resource, out var c) ? c + 1 : 1;

            // detect duplicates before insert
            if (resourceMap.TryGetValue(hex, out var existing))
            {
                dupes.Add($"({rd.q},{rd.r}) was {existing} → now {rd.resource}");
            }
            else
            {
                resourceMap[hex] = rd.resource;
            }

        }

        // Wait a frame to ensure MainBindings has set up properly
        StartCoroutine(InitializeVisualsAfterDelay(resourceMap));
    }

    private System.Collections.IEnumerator InitializeVisualsAfterDelay(Dictionary<Hex, FieldType> resourceMap)
    {
        // Wait a frame for MainBindings to complete setup
        yield return null;

        // Wait for MainBindings to exist
        int attempts = 0;
        while (FindFirstObjectByType<MainBindings>() == null && attempts < 10)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
            Debug.Log($"[GameManager] Waiting for MainBindings... Attempt {attempts}");
        }

        Debug.Log("[GameManager] Proceeding with visuals initialization");

        // Ensure managers have proper references for new game session
        if (visualsManager == null) visualsManager = GridVisualsManager.Instance;
        if (interactionManager == null) interactionManager = InteractionManager.Instance;

        visualsManager.InitializeVisuals(resourceMap);
        interactionManager.EnableInteraction(MyRole);
    }

    public void StartKingTurn()
    {
        if (IsGameOver) return;
        // Only increment round number at the start of king's turn (beginning of new round)
        if (CurrentTurn == GameTurn.Setup || CurrentTurn == GameTurn.Executing)
        {
            currentRoundNumber++;
            Debug.Log($"Round {currentRoundNumber} started!");
            UIManager.Instance.UpdateRoundNumber(currentRoundNumber);

            // Reset vertex highlights when new round starts
            interactionManager.ForceCompleteReset();
        }

        CurrentTurn = GameTurn.KingPlanning;

        Debug.Log("GameManager.StartKingTurn is now setting UI visibility.");

        UIManager.Instance.UpdateTurnStatus("Your Turn");
        interactionManager.EnableInteraction(PlayerRole.King);

        // Update button visibility based on turn and role
        UIManager.Instance.UpdateButtonVisibilityForTurn(CurrentTurn, MyRole);
    }

    public void StartBanditTurn()
    {
        if (IsGameOver) return;
        CurrentTurn = GameTurn.BanditPlanning;
        UIManager.Instance.UpdateTurnStatus("Your Turn");
        interactionManager.EnableInteraction(PlayerRole.Bandit);

        // Update button visibility based on turn and role
        UIManager.Instance.UpdateButtonVisibilityForTurn(CurrentTurn, MyRole);
    }

    public void StartExecutionPhase(List<PathData> kingPaths, List<AmbushEdge> banditAmbushes)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing...");

        // Hide all buttons during execution
        UIManager.Instance.UpdateButtonVisibilityForTurn(CurrentTurn, MyRole);

        interactionManager.DisableInteraction();

        // Execute all paths, not just the first one
        if (kingPaths.Count > 0)
        {
            var allPaths = kingPaths.Select(pathData => pathData.path).ToList();
            ExecuteWorkerPaths(allPaths);
        }
    }

    private void ExecuteWorkerPath(List<HexVertex> path)
    {
        interactionManager.ExecuteServerPath(path);
    }

    private void ExecuteWorkerPaths(List<List<HexVertex>> paths)
    {
        interactionManager.ExecuteServerPaths(paths);
    }

    public Dictionary<Hex, FieldType> GetResourceMap()
    {
        return resourceMap;
    }
}