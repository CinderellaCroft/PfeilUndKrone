using UnityEngine;

public enum PlayerRole { None, King, Bandit }
public enum GameTurn { Setup, KingPlanning, BanditPlanning, Executing }

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<GameManager>();
            if (_instance == null) Debug.LogError("FATAL ERROR: A GameManager is needed in the scene, but there is none.");
            return _instance;
        }
    }

    public PlayerRole MyRole { get; private set; } = PlayerRole.None;
    public GameTurn CurrentTurn { get; private set; } = GameTurn.Setup;


    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
    }

    public void SetRole(string roleName)
    {
        if (roleName == "King") MyRole = PlayerRole.King;
        else if (roleName == "Bandit") MyRole = PlayerRole.Bandit;

        UIManager.Instance.UpdateRoleText(MyRole);
        Debug.Log($"My role is: {MyRole}");
    }

    public void StartKingTurn()
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

    public void StartExecutionPhase(string kingPathsJson, string banditAmbushesJson)
    {
        CurrentTurn = GameTurn.Executing;
        UIManager.Instance.UpdateTurnStatus("Executing Round...");
        UIManager.Instance.SetDoneButtonActive(false);
        CornerPathManager.Instance.DisablePathSelection();
        AmbushManager.Instance.DisableAmbushPlacement();

        Debug.Log("Executing round with King paths: " + kingPathsJson);
        Debug.Log("And Bandit ambushes: " + banditAmbushesJson);
    }

    public void OnCornerClicked(CornerNode node)
    {
        if (CurrentTurn == GameTurn.KingPlanning && MyRole == PlayerRole.King)
        {
            CornerPathManager.Instance.OnCornerClicked(node);
        }
        else if (CurrentTurn == GameTurn.BanditPlanning && MyRole == PlayerRole.Bandit)
        {
            AmbushManager.Instance.OnCornerClicked(node);
        }
    }
}