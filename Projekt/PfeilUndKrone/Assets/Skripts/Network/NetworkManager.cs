using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System;
using System.Linq;
using PimDeWitte.UnityMainThreadDispatcher;
using NetworkingDTOs;
using System.Threading.Tasks;

public class NetworkManager : SingletonNetworkService<NetworkManager>
{
    public string AssignedRole { get; private set; }
    private int PORT = 8080;
    private String IP = "localhost";//   "localhost"     "172.104.147.34"
    private WebSocket websocket;
    private bool isGameOver = false; // Flag to track if the game has ended.

    private bool socketSetup = false;
    public override bool IsConnected
    {
        get
        {
            return websocket != null && websocket.State == WebSocketState.Open;
        }
    }


    public override async Task Connect()
    {
        Debug.Log("NM Connect() called");
        if (this.websocket == null)
        {
            Debug.Log("NM Connect(): websocket is null!");
            this.websocket = new WebSocket($"ws://{IP}:{PORT}");
        }
        if (!this.socketSetup)
        {
            Debug.Log("NM Connect(): SetupWebsocket()");
            SetupWebsocket();
        }
        if (this.websocket.State != WebSocketState.Open && this.websocket.State != WebSocketState.Connecting)
        {
            Debug.Log("NM Connect() -> await Connect()");
            await this.websocket.Connect();
        }
    }



    public override async Task Disconnect()
    {

        Debug.Log("NM Disconnect() called");
        if (this.websocket != null && this.websocket.State == WebSocketState.Open)
        {
            Debug.Log("NM Disconnect() await");
            this.socketSetup = false;
            await this.websocket.Close();
        }
    }



    void Start()
    {
        Debug.Log("NEW ROUND HERE WE GO!!! (Client NetworkManager.cs)");
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    void Update()
    {
        // The NativeWebSocket library requires this to be called each frame
        // to process message queues on the main thread.
#if !UNITY_WEBGL || UNITY_EDITOR
        if (this.websocket != null)
        {
            this.websocket.DispatchMessageQueue();
        }
#endif
    }

    public void SetupWebsocket()
    {
        this.socketSetup = true;
        Debug.Log("SetupWebsocket called()!!!!");
        if (this.websocket == null)
        {
            this.websocket = new WebSocket($"ws://{IP}:{PORT}");
        }

        this.websocket.OnError += (e) =>
        {
            Debug.LogError("❌ Connection Error: " + e);
        };

        this.websocket.OnClose += (e) =>
        {
            Debug.Log("❌ Connection closed: " + e);
        };

        // This is where we handle messages from the server
        this.websocket.OnMessage += (bytes) =>
        {
            var messageString = System.Text.Encoding.UTF8.GetString(bytes);

            // First, deserialize only to find the message type
            var typeFinder = JsonUtility.FromJson<ServerMessageType>(messageString);

            // We must use the Main Thread Dispatcher for ALL Unity API calls
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // Now, use the type to deserialize to the correct, fully-structured class
                switch (typeFinder.type)
                {
                    case "lobby_randomly_joined":
                        Debug.Log("cl: join_random -> sv: lobby_randomly_joined");
                        var lj = JsonUtility.FromJson<ServerMessageLobbyJoinedRandomly>(messageString);
                        Debug.Log($"Joined lobby {lj.payload.lobby_id} (queued={lj.payload.queued})");
                        MainMenu.Instance.UpdateStatusText(
                            lj.payload.queued
                                ? $"Waiting for opponent… (Lobby ID: {lj.payload.lobby_id})"
                                : $"Opponent found! Starting…"
                        );
                        break;

                    case "lobby_created":
                        var msg = JsonUtility.FromJson<ServerMessageLobbyCreated>(messageString);
                        if (MainMenu.Instance != null) MainMenu.Instance.ShowCreatedLobbyPanel(msg.payload.lobby_id);
                        break;

                    case "lobby_joinedById":
                        var joinedMsg = JsonUtility.FromJson<ServerMessageLobbyJoinedById>(messageString);
                        string joinedByIdMessage = $"Joined Lobby! Waiting for opponent...";
                        if (MainMenu.Instance != null) MainMenu.Instance.UpdateStatusText(joinedByIdMessage);
                        break;

                    case "match_created":
                        var matchMessage = JsonUtility.FromJson<ServerMessageMatchCreated>(messageString);
                        Debug.Log($"Match created! Assigning role: {matchMessage.payload.role}. Loading Main scene...");
                        
                        // 1. Store the role in our new property
                        AssignedRole = matchMessage.payload.role;
                        
                        // 2. Load the main game scene
                        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
                            
                        RaiseMatchCreated(matchMessage.payload.role);
                        break;

                    case "grid_data":
                        Debug.Log("NM RaiseGridDataReady()");
                        RaiseGridDataReady();
                        break;

                    case "resource_map":
                        {
                            var resMsg = JsonUtility.FromJson<ServerMessageResourceMap>(messageString);

                            if (resMsg?.payload?.map == null)
                            {
                                Debug.LogError("❌ Error: resource_map deserialization failed or payload.map is null");
                                break;
                            }

                            var list = new List<ResourceData>();
                            foreach (var rd in resMsg.payload.map)
                            {
                                if (!Enum.TryParse<FieldType>(rd.resource, true, out var parsed))
                                {
                                    Debug.LogError($"❌ Error: Unknown resource type '{rd.resource}' at ({rd.q},{rd.r})");
                                    parsed = FieldType.Desert; // fallback
                                }
                                list.Add(new ResourceData { q = rd.q, r = rd.r, resource = parsed });
                            }

                            RaiseGridDataReady();
                            RaiseResourceMapReceived(list);
                            break;
                        }

                    case "king_turn_start":
                        GameManager.Instance.StartKingTurn();
                        break;

                    case "bandit_turn_start":
                        var banditTurnMsg = JsonUtility.FromJson<ServerMessageBanditTurnStart>(messageString);
                        if (banditTurnMsg.payload.workerLocations != null && banditTurnMsg.payload.workerLocations.Length > 0)
                        {
                            InteractionManager.Instance.SetWorkerLocationsForBandit(banditTurnMsg.payload.workerLocations);
                        }
                        GameManager.Instance.StartBanditTurn();
                        break;

                    case "ambush_approved":
                        var ambushMessage = JsonUtility.FromJson<ServerMessageAmbushApproved>(messageString);
                        RaiseAmbushConfirmed(ambushMessage.payload);

                        // Notify InteractionManager that ambush purchase was approved
                        InteractionManager.Instance.OnAmbushPurchaseApproved();
                        break;

                    case "ambush_denied":
                        Debug.LogError("❌ Server denied ambush purchase - Not enough resources!");
                        UIManager.Instance.UpdateInfoText("Ambush denied: Not enough resources!");

                        // Notify InteractionManager that ambush purchase was denied
                        InteractionManager.Instance.OnAmbushPurchaseDenied("Not enough resources!");
                        break;

                    case "worker_approved":
                        Debug.Log("Worker purchase approved by server!");
                        UIManager.Instance.UpdateInfoText("Worker purchased successfully!");
                        
                        InteractionManager.Instance.OnWorkerPurchaseApproved();
                        
                        if (GameManager.Instance.MyRole == PlayerRole.King)
                        {
                            UIManager.Instance.UpdateKingWorkerBuyButtonText();
                            UIManager.Instance.UpdateKingPathButtonText();
                            UIManager.Instance.UpdateKingPathConfirmButtonText();
                            UIManager.Instance.UpdateKingWagonUpgradeButtonText();
                        }
                        break;

                    case "worker_denied":
                        Debug.LogError("Server denied worker purchase - Not enough resources!");
                        UIManager.Instance.UpdateInfoText("Worker denied: Not enough resources!");

                        InteractionManager.Instance.OnWorkerPurchaseDenied("Not enough resources!");
                        break;

                    case "wagon_upgrade_approved":
                        Debug.Log("Wagon upgrade approved by server!");
                        UIManager.Instance.UpdateInfoText("Worker upgraded to wagon successfully!");
                        
                        var wagonApprovedMsg = JsonUtility.FromJson<ServerMessageWagonUpgradeApproved>(messageString);
                        InteractionManager.Instance.OnWagonUpgradeApproved(wagonApprovedMsg.payload.wagonWorkers, wagonApprovedMsg.payload.workerCount);
                        
                        if (GameManager.Instance.MyRole == PlayerRole.King)
                        {
                            UIManager.Instance.UpdateKingWagonUpgradeButtonText();
                            UIManager.Instance.UpdateKingWagonPathButtonText();
                        }
                        break;

                    case "wagon_upgrade_denied":
                        Debug.LogError("Server denied wagon upgrade!");
                        var wagonDeniedMsg = JsonUtility.FromJson<ServerMessageWagonUpgradeDenied>(messageString);
                        UIManager.Instance.UpdateInfoText($"Wagon upgrade denied: {wagonDeniedMsg.payload.reason}");
                        InteractionManager.Instance.OnWagonUpgradeDenied(wagonDeniedMsg.payload.reason);
                        break;

                    case "resource_update":
                        var resourceMessage = JsonUtility.FromJson<ServerMessageResourceUpdate>(messageString);
                        UIManager.Instance.UpdateResourcesText(resourceMessage.payload.gold, resourceMessage.payload.wood, resourceMessage.payload.grain);

                        // Update InteractionManager with new gold amount for ambush buying
                        InteractionManager.Instance.UpdateGoldAmount(resourceMessage.payload.gold);

                        // Update bandit ambush button text to reflect new gold amount
                        if (GameManager.Instance.MyRole == PlayerRole.Bandit)
                        {
                            UIManager.Instance.UpdateBanditAmbushButtonText();
                        }
                        break;

                    case "execute_round":
                        var execMsg = JsonUtility.FromJson<ServerMessageExecuteRound>(messageString).payload;

                        if (execMsg.workersLost > 0 && GameManager.Instance.MyRole == PlayerRole.King)
                        {
                            Debug.Log($"Server reports {execMsg.workersLost} workers lost to ambushes");
                            if (InteractionManager.Instance != null)
                            {
                                InteractionManager.Instance.SetWorkerCountsFromServer(execMsg.kingWorkerCount, execMsg.kingWagonWorkerCount);
                                string message = $"{execMsg.workersLost} worker(s) lost to ambush! {execMsg.kingWorkerCount} workers remaining ({execMsg.kingWagonWorkerCount} wagons).";
                                UIManager.Instance.UpdateInfoText(message);
                            }
                        }

                        var r = (GameManager.Instance.MyRole.ToString() == "King")
                                ? execMsg.kingBonus
                                : execMsg.banditBonus;

                        UIManager.Instance.UpdateResourcesText(r.gold, r.wood, r.grain);
                        Debug.Log($"Updated resources: Gold {r.gold}, Wood {r.wood}, Grain {r.grain}");




                        // 3) Start synchronized animation for both players
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            List<NetworkingDTOs.AmbushEdge> convertedAmbushes = new List<NetworkingDTOs.AmbushEdge>();

                            // Process ambushes
                            if (execMsg.banditAmbushes != null && execMsg.banditAmbushes.Length > 0)
                            {
                                Debug.Log($"[NM] My role: {GameManager.Instance?.MyRole}, Processing {execMsg.banditAmbushes.Length} serialized ambushes for animation");

                                // Convert SerializableAmbushEdge[] to List<AmbushEdge>
                                for (int i = 0; i < execMsg.banditAmbushes.Length; i++)
                                {
                                    var serializedAmbush = execMsg.banditAmbushes[i];
                                    try
                                    {
                                        var convertedAmbush = serializedAmbush.ToAmbushEdge();
                                        Debug.Log($"[NM] Converted ambush [{i}]: {convertedAmbush.cornerA} <-> {convertedAmbush.cornerB}");
                                        convertedAmbushes.Add(convertedAmbush);
                                    }
                                    catch (System.Exception e)
                                    {
                                        Debug.LogError($"❌ [NM] Failed to convert ambush [{i}]: {e.Message}");
                                        Debug.LogError($"❌ [NM] Raw serialized ambush: cornerA({serializedAmbush.cornerA?.q},{serializedAmbush.cornerA?.r},{serializedAmbush.cornerA?.direction}) cornerB({serializedAmbush.cornerB?.q},{serializedAmbush.cornerB?.r},{serializedAmbush.cornerB?.direction})");
                                    }
                                }
                                Debug.Log($"[NM] Successfully converted {convertedAmbushes.Count} ambushes out of {execMsg.banditAmbushes.Length}");
                            }
                            else
                            {
                                Debug.Log($"[NM] My role: {GameManager.Instance?.MyRole}, No banditAmbushes to display");
                            }

                            // Process king paths (multiple paths now)
                            List<List<HexVertex>> allKingPaths = new List<List<HexVertex>>();
                            if (execMsg.kingPaths != null && execMsg.kingPaths.Length > 0)
                            {
                                Debug.Log($"[NM] My role: {GameManager.Instance?.MyRole}, Processing {execMsg.kingPaths.Length} King paths.");

                                // Convert all paths from SerializableHexVertex[] to List<HexVertex>
                                foreach (var pathData in execMsg.kingPaths)
                                {
                                    var hexVertexPath = pathData.path.Select(v => v.ToHexVertex()).ToList();
                                    allKingPaths.Add(hexVertexPath);
                                    Debug.Log($"[NM] Converted path with {hexVertexPath.Count} vertices");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"❌ Fehler: [NM] My role: {GameManager.Instance?.MyRole}, No kingPaths in execute_round payload.");
                            }

                            // Start synchronized animation with multiple paths
                            if (allKingPaths.Count > 0)
                            {
                                InteractionManager.Instance.StartSynchronizedAnimationMultiplePaths(allKingPaths, convertedAmbushes);
                            }
                            else
                            {
                                // If no paths, just display ambushes
                                InteractionManager.Instance.DisplayAnimationOrbs(convertedAmbushes);
                            }

                            // Schedule cleanup after animation completes (10 seconds as per server)
                            StartCoroutine(DelayedAnimationCleanup(10.5f));
                        });
                        break;


                    case "game_over":
                        isGameOver = true; // Keep this for the coroutine
                        var gameOverMsg = JsonUtility.FromJson<ServerMessageGameOver>(messageString);
                        string winner = gameOverMsg.payload.winner;
                        string reason = gameOverMsg.payload.reason;

                        Debug.LogWarning("--- RECEIVED 'game_over'. Displaying end panel. ---");

                        GameManager.Instance.EndGame();

                        bool amIWinner = GameManager.Instance.MyRole.ToString() == winner;
                        UIManager.Instance.ShowEndGamePanel(amIWinner);
                        break;

                    case "new_round":
                        var newRoundMsg = JsonUtility.FromJson<ServerMessageNewRound>(messageString);
                        var roundPayload = newRoundMsg.payload;

                        Debug.Log($"Round {roundPayload.roundNumber} started! Resources: 💰{roundPayload.resources.gold}, 🪵{roundPayload.resources.wood}, 🌾{roundPayload.resources.grain}");

                        // IMPORTANT: Clear all workers, paths, and visual elements at start of new round
                        InteractionManager.Instance.ForceCompleteReset();

                        UIManager.Instance.UpdateRoundNumber(roundPayload.roundNumber);
                        UIManager.Instance.UpdateResourcesText(roundPayload.resources.gold, roundPayload.resources.wood, roundPayload.resources.grain);

                        // Update InteractionManager with new gold amount
                        InteractionManager.Instance.UpdateGoldAmount(roundPayload.resources.gold);

                        // Restore purchased workers for King from server data
                        if (GameManager.Instance.MyRole == PlayerRole.King && roundPayload.workers > 0)
                        {
                            InteractionManager.Instance.RestorePurchasedWorkers(roundPayload.workers, roundPayload.wagonWorkers);
                            Debug.Log($"King's workers restored: {roundPayload.workers} total ({roundPayload.wagonWorkers} wagons)");
                        }

                        // Update bandit button text if bandit player
                        if (GameManager.Instance.MyRole == PlayerRole.Bandit)
                        {
                            UIManager.Instance.UpdateBanditAmbushButtonText();
                        }
                        
                        // Update king button texts if king player
                        if (GameManager.Instance.MyRole == PlayerRole.King)
                        {
                            UIManager.Instance.UpdateKingWorkerBuyButtonText();
                            UIManager.Instance.UpdateKingPathButtonText();
                            UIManager.Instance.UpdateKingPathConfirmButtonText();
                        }
                        break;

                    case "turn_status":
                        var turnStatusMsg = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        UIManager.Instance.UpdateTurnStatus(turnStatusMsg.payload);
                        break;

                    case "info":
                        var info = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        MainMenu.Instance.UpdateStatusText(info.payload);
                        Debug.Log($"Info-Message from Server: {info.payload}");
                        break;

                    case "error":
                        // This handles cases where the payload is just a simple string
                        var stringMessage = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        if (typeFinder.type == "info")
                        {
                            UIManager.Instance.UpdateInfoText(stringMessage.payload);
                        }
                        else
                        {
                            Debug.LogError("Server Error: " + stringMessage.payload);
                            UIManager.Instance.UpdateInfoText("SERVER ERROR: " + stringMessage.payload);
                        }
                        break;

                    default:
                        Debug.LogWarning("Received message of unknown type: " + typeFinder.type);
                        break;
                }
            });
        };

        this.websocket.OnOpen += () =>
        {
            Debug.Log("✅ Connection to server opened!");
        };
    }


    /// <summary>
    /// Sends a structured message to the server.
    /// </summary>
    /// <param name="type">The "event name" for the server to route.</param>
    /// <param name="payloadObject">The data object to send (e.g., PathData).</param>
    public override async void Send(string type, object payloadObject)
    {
        if (websocket.State != WebSocketState.Open)
        {
            Debug.LogError("Error: Cannot send message, websocket is not open.");
            return;
        }

        // 1. Serialize the payload object into its JSON string representation.
        string payloadJson = JsonUtility.ToJson(payloadObject);

        // 2. Construct the final JSON string manually to ensure the payload is a nested object, not a string.
        // This avoids the double-serialization issue.
        string finalJson = $"{{\"type\":\"{type}\",\"payload\":{payloadJson}}}";
        
        // This is how it looked before the fix:
        // ClientMessage message = new ClientMessage { type = type, payload = payloadJson };
        // string finalJson = JsonUtility.ToJson(message);
        
        Debug.Log($"Sending message: {finalJson}"); // Added for debugging
        await websocket.SendText(finalJson);
    }

    /// <summary>
    /// Called by UI to join a random game.
    /// </summary>
    public void JoinRandomLobby()
    {
        if (IsConnected)
        {
            Debug.Log("NM -> Sending 'join_random'");
            Send("join_random", new object());
        }
        else
        {
            Debug.LogError("Cannot join random lobby, not connected!");
        }
    }

    /// <summary>
    /// Called by UI to create a new private lobby.
    /// </summary>
    public void CreatePrivateLobby()
    {
        if (IsConnected)
        {
            Debug.Log("NM -> Sending 'create_lobby'");
            Send("create_lobby", new object());
        }
        else
        {
            Debug.LogError("Cannot create private lobby, not connected!");
        }
    }

    /// <summary>
    /// Called by UI to join an existing lobby by its ID.
    /// </summary>
    public void JoinLobbyById(string lobbyId)
    {
        if (IsConnected)
        {
            Debug.Log($"NM -> Sending 'join_lobbyById' for ID: {lobbyId}");
            var payload = new ClientPayloadJoinLobby { lobby_id = lobbyId };
            Send("join_lobbyById", payload);
        }
        else
        {
            Debug.LogError("Cannot join lobby by ID, not connected!");
        }
    }

    // Coroutine to clean up animation after delay
    private System.Collections.IEnumerator DelayedAnimationCleanup(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.LogWarning($"--- Coroutine 'DelayedAnimationCleanup' has finished waiting. isGameOver = {isGameOver} ---");

        // Only run cleanup logic if the game is NOT over.
        if (isGameOver) yield break;

        Debug.Log($"[NM] DelayedAnimationCleanup: Cleaning up animation for {GameManager.Instance?.MyRole}");
        InteractionManager.Instance.CleanupAfterRoundAnimation();
    }
}