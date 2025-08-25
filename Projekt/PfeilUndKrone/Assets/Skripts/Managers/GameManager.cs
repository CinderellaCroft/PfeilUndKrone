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

    private Dictionary<Hex, ResourceType> resourceMap;

    private int currentRoundNumber = 0; // Track current round

    public PlayerRole MyRole { get; private set; } = PlayerRole.None;
    public GameTurn CurrentTurn { get; private set; } = GameTurn.Setup;

    void OnEnable()
    {
        networkService.OnGridDataReady += OnGridReady;
        networkService.OnResourceMapReceived += OnResourceMap;
    }
    void OnDisable()
    {
        networkService.OnGridDataReady -= OnGridReady;
        networkService.OnResourceMapReceived -= OnResourceMap;
    }

    public void SetRole(string roleName)
    {
        if (roleName == "King") 
        {
            MyRole = PlayerRole.King;
        }
        else if (roleName == "Bandit") 
        {
            MyRole = PlayerRole.Bandit;
        }
        else
        {
            Debug.Log($"Unknown role: '{roleName}', role remains: {MyRole}");
        }

        UIManager.Instance.UpdateRoleText(MyRole);
    }

    void OnGridReady()
    {
        gridGenerator.GenerateGrid();
    }


    void OnResourceMap(List<ResourceData> mapData)
    {
        var counts = new Dictionary<ResourceType, int>();
        var dupes = new List<string>();
        resourceMap = new Dictionary<Hex, ResourceType>();

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
            Debug.Log($"🎯 Round {currentRoundNumber} started!");
            UIManager.Instance.UpdateRoundNumber(currentRoundNumber);
            
            // Reset vertex highlights when new round starts
            interactionManager.ForceCompleteReset();
        }
        
        CurrentTurn = GameTurn.KingPlanning;
        UIManager.Instance.UpdateTurnStatus("King's Turn: Select Path");
        interactionManager.EnableInteraction(PlayerRole.King);
        
        // Show Done button only for King, hide for Bandit
        if (MyRole == PlayerRole.King) 
        {
            UIManager.Instance.SetDoneButtonActive(true);
        }
        else
        {
            UIManager.Instance.SetDoneButtonActive(false);
        }
    }

    public void StartBanditTurn()
    {
        CurrentTurn = GameTurn.BanditPlanning;
        UIManager.Instance.UpdateTurnStatus("Bandit's Turn: Place Ambushes");
        interactionManager.EnableInteraction(PlayerRole.Bandit);
        
        // Show Done button only for Bandit, hide for King
        if (MyRole == PlayerRole.Bandit) 
        {
            UIManager.Instance.SetDoneButtonActive(true);
        }
        else
        {
            UIManager.Instance.SetDoneButtonActive(false);
        }
    }

    public void StartExecutionPhase(List<PathData> kingPaths, List<AmbushEdge> banditAmbushes)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing Round...");
        UIManager.Instance.SetDoneButtonActive(false);
        interactionManager.DisableInteraction();

        if (kingPaths.Count > 0 && kingPaths[0].path.Count > 0)
            ExecuteWorkerPath(kingPaths[0].path);
    }

    private void ExecuteWorkerPath(List<HexVertex> path)
    {
        interactionManager.ExecuteServerPath(path);
    }
}