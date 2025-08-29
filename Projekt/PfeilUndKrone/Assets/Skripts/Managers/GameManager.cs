using System.Collections.Generic;
using System.Linq;
using NetworkingDTOs;
using UnityEngine;

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

    public PlayerRole MyRole { get; private set; } = PlayerRole.None;
    public GameTurn CurrentTurn { get; private set; } = GameTurn.Setup;

    void OnEnable()
    {
        networkService.OnRoleAssigned += SetRole;
        networkService.OnGridDataReady += OnGridReady;
        networkService.OnResourceMapReceived += OnResourceMap;
    }
    void OnDisable()
    {
        networkService.OnRoleAssigned -= SetRole;
        networkService.OnGridDataReady -= OnGridReady;
        networkService.OnResourceMapReceived -= OnResourceMap;
    }

    void SetRole(string roleName)
    {
    if (roleName == PlayerRole.King.ToString()) MyRole = PlayerRole.King;
    else if (roleName == PlayerRole.Bandit.ToString()) MyRole = PlayerRole.Bandit;
        else Debug.Log($"Unknown role: '{roleName}', role remains: {MyRole}");
        

        UIManager.Instance.UpdateRoleText(MyRole);
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
        visualsManager.InitializeVisuals(resourceMap);
        interactionManager.EnableInteraction(MyRole);
    }

    public void StartKingTurn()
    {
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
        UIManager.Instance.UpdateTurnStatus("King's Turn: Select Path");
        interactionManager.EnableInteraction(PlayerRole.King);
        
        // Update button visibility based on turn and role
        UIManager.Instance.UpdateButtonVisibilityForTurn(CurrentTurn, MyRole);
    }

    public void StartBanditTurn()
    {
        CurrentTurn = GameTurn.BanditPlanning;
        UIManager.Instance.UpdateTurnStatus("Bandit's Turn: Place Ambushes");
    interactionManager.EnableInteraction(PlayerRole.Bandit);
        
        // Update button visibility based on turn and role
        UIManager.Instance.UpdateButtonVisibilityForTurn(CurrentTurn, MyRole);
    }

    public void StartExecutionPhase(List<PathData> kingPaths, List<AmbushEdge> banditAmbushes)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing Round...");
        
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