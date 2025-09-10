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

    private int PORT = 8080;
    private String IP = "localhost";//   "localhost"     "172.104.235.41"
    private WebSocket websocket;
    private bool isGameOver = false; // Flag to track if the game has ended.

    private bool socketSetup = false;

    public event Action<string> OnLobbyCreated;
    public event Action<LobbyJoinedPayload> OnLobbyJoined;
    public event Action<string> OnLobbyInfo;

    private TaskCompletionSource<bool> connectionTcs;

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
        if (this.websocket.State == WebSocketState.Open)
        {
            Debug.Log("NM Connect(): Already connected.");
            return;
        }
        if (this.websocket.State == WebSocketState.Connecting)
        {
            Debug.Log("NM Connect(): Connection already in progress, awaiting result...");
            await connectionTcs.Task;
            return;
        }
        
        // Use a TaskCompletionSource for a reliable async connection
        connectionTcs = new TaskCompletionSource<bool>();
        
        // Start the connection attempt (fire and forget)
        // The result will be handled by our OnOpen/OnError events
        _ = this.websocket.Connect();
        
        Debug.Log("NM Connect() -> Awaiting connection result...");
        
        // Await the result from our TaskCompletionSource
        bool success = await connectionTcs.Task;
        
        Debug.Log($"NM Connect() completed. Success: {success}, State: {this.websocket.State}");
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
        _ = Disconnect();
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

    /// <summary>
    /// Safe way to update UI that won't crash during scene transitions
    /// </summary>
    private void SafeUpdateInfoText(string message)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateInfoText(message);
        }
        else
        {
            Debug.LogWarning($"UIManager not available - Info text: {message}");
        }
    }

    public void SetupWebsocket()
    {
        this.socketSetup = true;
        Debug.Log("SetupWebsocket called()!!!!");
        if (this.websocket == null)
        {
            this.websocket = new WebSocket($"ws://{IP}:{PORT}");
        }

        this.websocket.OnOpen += () =>
        {
            Debug.Log("✅ Connection to server opened!");
            // Signal that the connection task was successful
            if (connectionTcs != null && !connectionTcs.Task.IsCompleted)
            {
                connectionTcs.TrySetResult(true);
            }
        };

        this.websocket.OnError += (e) =>
        {
            Debug.LogError("❌ Connection Error: " + e);
            // Signal that the connection task failed
            if (connectionTcs != null && !connectionTcs.Task.IsCompleted)
            {
                connectionTcs.TrySetResult(false);
            }
        };

        this.websocket.OnClose += (e) =>
        {
            Debug.Log("❌ Connection closed: " + e);
            // Signal that the connection task failed
            if (connectionTcs != null && !connectionTcs.Task.IsCompleted)
            {
                connectionTcs.TrySetResult(false);
            }
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
                        // Raise the generic OnLobbyJoined event
                        OnLobbyJoined?.Invoke(lj.payload);
                        // Also raise the info event for any UI to display
                        string randomInfo = lj.payload.queued ? $"Waiting for opponent… (Lobby ID: {lj.payload.lobby_id})" : "Opponent found! Starting…";
                        OnLobbyInfo?.Invoke(randomInfo);
                        break;

                    case "lobby_created":
                        var msg = JsonUtility.FromJson<ServerMessageLobbyCreated>(messageString);

                        string lobbyId = msg.payload.lobby_id; 

                        Debug.Log($"Created lobby: {lobbyId}");
                        // Raise our new event instead of calling UIManager directly
                        OnLobbyCreated?.Invoke(lobbyId);
                        break;

                    case "lobby_joinedById":
                        var joinedMsg = JsonUtility.FromJson<ServerMessageLobbyJoinedById>(messageString);
                        Debug.Log($"Joined lobby {joinedMsg.payload.lobby_id} by ID.");
                        // Raise the generic OnLobbyJoined event
                        OnLobbyJoined?.Invoke(joinedMsg.payload);
                        // Also raise the info event
                        OnLobbyInfo?.Invoke($"Joined Lobby! Waiting for opponent...");
                        break;

                    case "match_created":
                        var matchMessage = JsonUtility.FromJson<ServerMessageMatchCreated>(messageString);
                        Debug.Log($"Player joined as: {matchMessage.payload.role}");
                        Debug.Log(GameManager.Instance == null ? "GameManager is not set up!" : "GameManager is ready.");

                        // Always raise the event - TitleSceneManager will handle scene transition
                        // GameManager will be available after scene loads
                        RaiseMatchCreated(matchMessage.payload.role);
                        break;

                    case "grid_data":
                        Debug.Log("NM RaiseGridDataReady()");
                        // Wait for GameManager to be ready before processing grid data
                        if (GameManager.Instance != null)
                        {
                            RaiseGridDataReady();
                        }
                        else
                        {
                            Debug.LogWarning("Received grid_data but GameManager not ready - deferring...");
                            StartCoroutine(DeferGridDataUntilGameManagerReady());
                        }
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

                            // Wait for GameManager before processing resource map
                            if (GameManager.Instance != null)
                            {
                                RaiseGridDataReady();
                                RaiseResourceMapReceived(list);
                            }
                            else
                            {
                                Debug.LogWarning("Received resource_map but GameManager not ready - deferring...");
                                StartCoroutine(DeferResourceMapUntilGameManagerReady(list));
                            }
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
                        Debug.Log($"[NetworkManager] Game Over Details: Winner={winner}, Reason={reason}");

                        // CRITICAL: Store role immediately to prevent any interference
                        Debug.Log($"[NetworkManager] GameManager.Instance: {GameManager.Instance}");
                        Debug.Log($"[NetworkManager] GameManager.Instance.GetHashCode(): {GameManager.Instance.GetHashCode()}");

                        PlayerRole myCurrentRole = GameManager.Instance.MyRole;
                        string myCurrentRoleString = myCurrentRole.ToString();

                        Debug.Log($"[NetworkManager] My Role: {myCurrentRole}, My Role String: '{myCurrentRoleString}'");
                        Debug.Log($"[NetworkManager] Winner String: '{winner}'");

                        // IMPORTANT: Determine winner using stored values, not live GameManager state
                        bool amIWinner = myCurrentRoleString == winner;
                        Debug.Log($"[NetworkManager] Winner determination (using stored role): Am I Winner? {amIWinner}");

                        GameManager.Instance.EndGame();

                        // Double-check what role is after EndGame (for debugging)
                        Debug.Log($"[NetworkManager] Role after EndGame: {GameManager.Instance.MyRole}");

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
                        // IMPORTANT: Only restore workers if this is NOT round 1 (fresh game should start with 0 workers)
                        if (GameManager.Instance.MyRole == PlayerRole.King && roundPayload.workers > 0 && roundPayload.roundNumber > 1)
                        {
                            InteractionManager.Instance.RestorePurchasedWorkers(roundPayload.workers, roundPayload.wagonWorkers);
                            Debug.Log($"King's workers restored: {roundPayload.workers} total ({roundPayload.wagonWorkers} wagons)");
                        }
                        else if (GameManager.Instance.MyRole == PlayerRole.King && roundPayload.roundNumber == 1)
                        {
                            // Ensure fresh start for new game (round 1)
                            InteractionManager.Instance.RestorePurchasedWorkers(0, 0);
                            Debug.Log($"New game detected (round 1) - King starting with 0 workers");
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
                        SafeUpdateInfoText(info.payload);
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
            Debug.LogError("❌ Error: Cannot send message, websocket is not open.");
            return;
        }

        // 1. Serialize the payload object (e.g., PathData) into its own JSON string.
        // This is the critical step to fix the serialization bug.
        string payloadJson = JsonUtility.ToJson(payloadObject);

        // 2. Create the outer message wrapper, placing the JSON string into the payload field.
        ClientMessage message = new ClientMessage { type = type, payload = payloadJson };

        // 3. Serialize the final wrapper object into the final JSON string to be sent.
        string finalJson = JsonUtility.ToJson(message);

        await websocket.SendText(finalJson);
    }

    /// <summary>
    /// Called by UI to join a random game.
    /// </summary>
    public void JoinRandomLobby()
    {
        Debug.Log($"JoinRandomLobby() called. IsConnected: {IsConnected}, WebSocket State: {websocket?.State}");
        if (IsConnected)
        {
            Debug.Log("NM -> Sending 'join_random'");
            Send("join_random", new object());
        }
        else
        {
            Debug.LogError($"Cannot join random lobby, not connected! WebSocket State: {websocket?.State}");
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

    // Coroutine to wait for GameManager to be ready before processing grid data
    private System.Collections.IEnumerator DeferGridDataUntilGameManagerReady()
    {
        int attempts = 0;
        while (GameManager.Instance == null && attempts < 50) // Max 5 seconds
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
            Debug.Log($"Waiting for GameManager... Attempt {attempts}");
        }

        if (GameManager.Instance != null)
        {
            Debug.Log("GameManager ready - processing deferred grid_data");
            RaiseGridDataReady();
        }
        else
        {
            Debug.LogError("GameManager never became ready - grid_data processing failed!");
        }
    }

    // Coroutine to wait for GameManager to be ready before processing resource map
    private System.Collections.IEnumerator DeferResourceMapUntilGameManagerReady(List<ResourceData> resourceData)
    {
        int attempts = 0;
        while (GameManager.Instance == null && attempts < 50) // Max 5 seconds
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
            Debug.Log($"Waiting for GameManager for resource_map... Attempt {attempts}");
        }

        if (GameManager.Instance != null)
        {
            Debug.Log("GameManager ready - processing deferred resource_map");
            RaiseGridDataReady();
            RaiseResourceMapReceived(resourceData);
        }
        else
        {
            Debug.LogError("GameManager never became ready - resource_map processing failed!");
        }
    }
}