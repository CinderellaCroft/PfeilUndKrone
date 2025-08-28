using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System;
using System.Linq;
using PimDeWitte.UnityMainThreadDispatcher; // Make sure you have imported this asset
using NetworkingDTOs;


public class NetworkManager : SingletonNetworkService<NetworkSimulator>
{
    private WebSocket websocket;

    async void Start()
    {
        // Connect to the port specified in your server.js (8080)
        websocket = new WebSocket("ws://localhost:8080");

        websocket.OnOpen += () =>
        {
            Debug.Log("✅ Connection to server opened!");
            //erstmal nur join_random verwenden, später auch create_lobby und join_lobby
            Send("join_random", new object());  // -> response: lobby_randomly_joined


            //Send("create_lobby", new object());  // -> lobby_created

            // var payload = new { lobby_id = "priv-1753375008725-882" }; // Replace with actual lobby ID
            // Send("join_lobbyById", payload);
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("❌ Connection Error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("❌ Connection closed: " + e);
        };

        // This is where we handle messages from the server
        websocket.OnMessage += (bytes) =>
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
                        var lj = JsonUtility.FromJson<ServerMessageLobbyJoinedRandomly>(messageString);
                        Debug.Log($"Joined lobby {lj.payload.lobby_id} (queued={lj.payload.queued})");
                        UIManager.Instance.UpdateInfoText(
                            lj.payload.queued
                                ? $"Waiting for opponent… (Lobby ID: {lj.payload.lobby_id})"
                                : $"Opponent found! Starting…"
                        );
                        break;

                    //receive lobbyID by server, log lobbyID to console -> share lobbyID with a friend
                    case "lobby_created":
                        var msg = JsonUtility.FromJson<ServerMessageLobbyCreated>(messageString);
                        Debug.Log($"Created lobby: {msg.lobbyID}");
                        UIManager.Instance.UpdateInfoText(
                                $"Waiting for opponent… (Lobby ID: {msg.lobbyID})"
                        );
                        break;

                    case "lobby_joinedById":
                        var joinedMsg = JsonUtility.FromJson<ServerMessageLobbyJoinedById>(messageString);
                        Debug.Log($"Joined lobby {joinedMsg.payload.lobby_id} (queued={joinedMsg.payload.queued})");
                        break;


                    case "match_created":
                        var matchMessage = JsonUtility.FromJson<ServerMessageMatchCreated>(messageString);
                        Debug.Log($"Player joined as: {matchMessage.payload.role}");
                        Debug.Log(GameManager.Instance == null ? "GameManager is not set up!" : "GameManager is ready.");
                        RaiseMatchCreated(matchMessage.payload.role);
                        break;

                    case "grid_data":
                        RaiseGridDataReady();
                        break;

                    // case "resource_map":
                    //     var rm = JsonUtility.FromJson<ServerMessageResourceMap>(messageString);
                    //     RaiseResourceMapReceived(rm.payload);
                    //     break;

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



                    // case "resource_map":
                    //     {
                    //         Debug.Log("resource_map received (raw): " + messageString);
                    //         var resMsg = JsonUtility.FromJson<ServerMessageResourceMap>(messageString);

                    //         if (resMsg == null || resMsg.payload == null || resMsg.payload.map == null)
                    //         {
                    //             Debug.LogError("resource_map: deserialization failed or payload.map is null");
                    //             break;
                    //         }

                    //         var list = resMsg.payload.map;
                    //         Debug.Log($"resource_map parsed: {list.Count} entries");

                    //         // log each resource
                    //         foreach (var rd in list)
                    //         {
                    //             Debug.Log($"Hex({rd.q},{rd.r}) -> {rd.resource}");
                    //         }

                    //         // summary counts by type
                    //         var counts = list.GroupBy(rd => rd.resource)
                    //                         .ToDictionary(g => g.Key, g => g.Count());
                    //         foreach (var kv in counts)
                    //         {
                    //             Debug.Log($"Resource {kv.Key}: {kv.Value}");
                    //         }

                    //         RaiseGridDataReady();
                    //         RaiseResourceMapReceived(list);
                    //         break;
                    //     }

                    case "king_turn_start":
                        GameManager.Instance.StartKingTurn();
                        break;

                    case "bandit_turn_start":
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

                        var r = execMsg.winnerResourceUpdate;

                        //ResourceUpdate - Nur der Gewinner bekommt die neuen Ressourcen
                        if (GameManager.Instance.MyRole.ToString() == execMsg.winner)
                        {
                            UIManager.Instance.UpdateResourcesText(r.gold, r.wood, r.grain);
                            Debug.Log($"You won! Updated resources: 💰{r.gold}, 🪵{r.wood}, 🌾{r.grain}");
                        }
                        else
                        {
                            Debug.Log($"You lost this round. Winner: {execMsg.winner}");
                        }

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
                        
                        // Update bandit button text if bandit player
                        if (GameManager.Instance.MyRole == PlayerRole.Bandit)
                        {
                            UIManager.Instance.UpdateBanditAmbushButtonText();
                        }
                        break;

                    case "turn_status":
                        var turnStatusMsg = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        UIManager.Instance.UpdateTurnStatus(turnStatusMsg.payload);
                        break;

                    case "info":
                        var info = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        UIManager.Instance.UpdateInfoText(info.payload);
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

        await websocket.Connect();
    }

    void Update()
    {
        // The NativeWebSocket library requires this to be called each frame
        // to process message queues on the main thread.
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
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
    
    // Coroutine to clean up animation after delay
    private System.Collections.IEnumerator DelayedAnimationCleanup(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"[NM] DelayedAnimationCleanup: Cleaning up animation for {GameManager.Instance?.MyRole}");
        InteractionManager.Instance.CleanupAfterRoundAnimation();
    }

    private async void OnApplicationQuit()
    {
        // Ensure the connection is closed when the game exits.
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Close();
        }
    }
}