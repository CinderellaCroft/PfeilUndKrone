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
        if (roleName == "King") MyRole = PlayerRole.King;
        else if (roleName == "Bandit") MyRole = PlayerRole.Bandit;

        UIManager.Instance.UpdateRoleText(MyRole);
        Debug.Log($"My role is: {MyRole}");
    }

    void OnGridReady()
    {
        Debug.Log("GAMEMANAGER: OnGridReady()");
        gridGenerator.GenerateGrid();

    }

    void OnResourceMap(List<ResourceData> mapData)
    {
        Debug.Log("GAMEMANAGER: OnResourceMap()\n!\n!");
        // Build map
        resourceMap = mapData.ToDictionary(rd => new Hex(rd.q, rd.r), rd => rd.resource);

        // Initialize visuals and interactions
        visualsManager.InitializeVisuals(resourceMap);
        interactionManager.EnableInteraction(MyRole);
    }

    public void StartKingTurn()
    {
        Debug.Log($"GAMEMANAGER: StartKingTurn() called. MyRole: {MyRole}");
        CurrentTurn = GameTurn.KingPlanning;
        UIManager.Instance.UpdateTurnStatus("King's Turn: Select Path");
        interactionManager.EnableInteraction(PlayerRole.King);
        if (MyRole == PlayerRole.King) UIManager.Instance.SetDoneButtonActive(true);
        Debug.Log($"GAMEMANAGER: King turn started. CurrentTurn: {CurrentTurn}");
    }

    public void StartBanditTurn()
    {
        Debug.Log($"GAMEMANAGER: StartBanditTurn() called. MyRole: {MyRole}");
        CurrentTurn = GameTurn.BanditPlanning;
        UIManager.Instance.UpdateTurnStatus("Bandit's Turn: Place Ambushes");
        interactionManager.EnableInteraction(PlayerRole.Bandit);
        if (MyRole == PlayerRole.Bandit) UIManager.Instance.SetDoneButtonActive(true);
        Debug.Log($"GAMEMANAGER: Bandit turn started. CurrentTurn: {CurrentTurn}");
    }

    public void StartExecutionPhase(List<PathData> kingPaths, List<AmbushEdge> banditAmbushes)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing Round...");
        UIManager.Instance.SetDoneButtonActive(false);
        interactionManager.DisableInteraction();

        Debug.Log("Executing round with King paths:");
        foreach (var pd in kingPaths)
            foreach (var c in pd.path)
                Debug.Log($"  Corner: ({c.Hex.Q},{c.Hex.R},{c.Direction})");

        Debug.Log("And Bandit ambushes:");
        foreach (var amb in banditAmbushes)
            Debug.Log($"  Ambush: ({amb.cornerA.Hex.Q},{amb.cornerA.Hex.R},{amb.cornerA.Direction}) ↔ ({amb.cornerB.Hex.Q},{amb.cornerB.Hex.R},{amb.cornerB.Direction})");

        if (kingPaths.Count > 0 && kingPaths[0].path.Count > 0)
            ExecuteWorkerPath(kingPaths[0].path);
    }

    private void ExecuteWorkerPath(List<HexVertex> path)
    {
        interactionManager.ExecuteServerPath(path);
    }
}