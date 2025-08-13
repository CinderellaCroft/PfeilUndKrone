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
        gridGenerator.GenerateGrid();
    }

    void OnResourceMap(List<ResourceData> mapData)
    {
        // Build map
        resourceMap = mapData.ToDictionary(rd => new Hex(rd.q, rd.r), rd => rd.resource);
        foreach (var rd in mapData)
        {
            resourceMap[new Hex(rd.q, rd.r)] = rd.resource;
        }

        // Initialize visuals and interactions
        visualsManager.InitializeVisuals(resourceMap);
        interactionManager.EnableInteraction(MyRole);
    }

    /* public void StartKingTurn()
    {
        CurrentTurn = GameTurn.KingPlanning;
        UIManager.Instance.UpdateTurnStatus("King's Turn: Place Workers");
        CornerPathManager.Instance.EnablePathSelection();
        AmbushManager.Instance.DisableAmbushPlacement();
        if (MyRole == PlayerRole.King) UIManager.Instance.SetDoneButtonActive(true);
    }

    public void StartBanditTurn()
    {
        CurrentTurn = GameTurn.BanditPlanning;
        UIManager.Instance.UpdateTurnStatus("Bandit's Turn: Place Ambushes");
        CornerPathManager.Instance.DisablePathSelection();
        AmbushManager.Instance.EnableAmbushPlacement();
        if (MyRole == PlayerRole.Bandit) UIManager.Instance.SetDoneButtonActive(true);
    }

    public void StartExecutionPhase(List<PathData> kingPaths, List<AmbushEdge> banditAmbushes)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing Round...");
        UIManager.Instance.SetDoneButtonActive(false);
        CornerPathManager.Instance.DisablePathSelection();
        AmbushManager.Instance.DisableAmbushPlacement();

        Debug.Log("Executing round with King paths:");
        foreach (var pd in kingPaths)
            foreach (var c in pd.path)
                Debug.Log($"  Corner: ({c.q},{c.r},{c.i})");

        Debug.Log("And Bandit ambushes:");
        foreach (var amb in banditAmbushes)
            Debug.Log($"  Ambush: ({amb.cornerA.q},{amb.cornerA.r},{amb.cornerA.i}) ↔ ({amb.cornerB.q},{amb.cornerB.r},{amb.cornerB.i})");

        // Hier kannst du dann starten, welche Pfade ausgeführt werden sollen
        // z.B. Worker loslaufen lassen:
        if (kingPaths.Count > 0)
            CornerPathManager.Instance.ExecuteServerPath(kingPaths[0].path);
    } */
}