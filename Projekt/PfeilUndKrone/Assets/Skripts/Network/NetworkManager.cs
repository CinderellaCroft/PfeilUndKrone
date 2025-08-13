using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System;
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
            Debug.Log("Connection to server opened!");
            //erstmal nur join_random verwenden, später auch create_lobby und join_lobby
            Send("join_random", new object());  // -> response: lobby_randomly_joined


            //Send("create_lobby", new object());  // -> lobby_created

            // var payload = new { lobby_id = "priv-1753375008725-882" }; // Replace with actual lobby ID
            // Send("join_lobbyById", payload);
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("Connection Error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed: " + e);
        };

        // This is where we handle messages from the server
        websocket.OnMessage += (bytes) =>
        {
            var messageString = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Message received from server: " + messageString);

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
                        Debug.Log($"Created: {msg.lobbyID}");
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
                        Debug.Log(GameManager.Instance == null ? "GameManager is not set up!" : "GameManager is ready.");
                        GameManager.Instance.SetRole(matchMessage.payload.role);
                        break;

                    case "grid_data":
                        RaiseGridDataReady();
                        break;

                    case "resource_map":
                        var rm = JsonUtility.FromJson<ServerMessageResourceMap>(messageString);
                        RaiseResourceMapReceived(rm.payload);
                        break;

                    case "king_turn_start":
                        //GameManager.Instance.StartKingTurn();
                        break;

                    case "bandit_turn_start":
                        //GameManager.Instance.StartBanditTurn();
                        break;

                    case "ambush_approved":
                        var ambushMessage = JsonUtility.FromJson<ServerMessageAmbushApproved>(messageString);
                        RaiseAmbushConfirmed(ambushMessage.payload);
                        break;

                    case "ambush_denied":
                        Debug.LogWarning("Server denied ambush purchase.");
                        UIManager.Instance.UpdateInfoText("Ambush denied: Not enough resources!");
                        break;

                    case "resource_update":
                        var resourceMessage = JsonUtility.FromJson<ServerMessageResourceUpdate>(messageString);
                        UIManager.Instance.UpdateResourcesText(resourceMessage.payload.gold, resourceMessage.payload.wood, resourceMessage.payload.grain);
                        break;

                    case "execute_round":
                        Debug.Log(messageString);
                        var execMsg = JsonUtility.FromJson<ServerMessageExecuteRound>(messageString).payload;

                        // 2) Gib es an den GameManager weiter (mit echten Listen, nicht JSON-Strings)
                        /* GameManager.Instance.StartExecutionPhase(
                            execMsg.kingPaths,
                            execMsg.banditAmbushes
                        ); */


                        var r = execMsg.winnerResourceUpdate;

                        //ResourceUpdate
                        if (GameManager.Instance.MyRole.ToString() == execMsg.winner)
                        {
                            UIManager.Instance.UpdateResourcesText(r.gold, r.wood, r.grain);
                        }


                        Debug.Log($"Winner: {execMsg.winner}, Gold: {r.gold}, Wood: {r.wood}, Grain: {r.grain}");
                        //Log outcome
                        Debug.Log(
                            $"Rounds Outcome. Winner: {execMsg.winner}, Bandits successful ambushes: {execMsg.outcome} ");

                        // 3) Nun direkt den ersten King-Pfad ausführen
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            if (execMsg.kingPaths != null && execMsg.kingPaths.Count > 0)
                            {
                                //var firstPath = execMsg.kingPaths[0].path;
                                //Debug.Log($"[NM] Führe ersten King-Pfad aus mit {firstPath.Count} Ecken.");
                                //CornerPathManager.Instance.ExecuteServerPath(firstPath);
                            }
                            else
                            {
                                Debug.LogWarning("[NM] Keine kingPaths im execute_round-Payload.");
                            }
                        });
                        break;


                    case "new_round":
                        // Assuming "new_round" also sends a ResourcePayload
                        var newRoundMessage = JsonUtility.FromJson<ServerMessageResourceUpdate>(messageString);
                        UIManager.Instance.UpdateResourcesText(newRoundMessage.payload.gold, newRoundMessage.payload.wood, newRoundMessage.payload.grain);
                        UIManager.Instance.UpdateInfoText("New round starting...");
                        break;

                    case "info":
                        var info = JsonUtility.FromJson<ServerMessageStringPayload>(messageString);
                        Debug.Log($"Info-Message from Server: {info}");
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

        Debug.Log("Attempting to connect to server...");
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
            Debug.LogWarning("Cannot send message, websocket is not open.");
            return;
        }

        // 1. Serialize the payload object (e.g., PathData) into its own JSON string.
        // This is the critical step to fix the serialization bug.
        string payloadJson = JsonUtility.ToJson(payloadObject);

        // 2. Create the outer message wrapper, placing the JSON string into the payload field.
        ClientMessage message = new ClientMessage { type = type, payload = payloadJson };

        // 3. Serialize the final wrapper object into the final JSON string to be sent.
        string finalJson = JsonUtility.ToJson(message);

        Debug.Log("Sending message: " + finalJson);
        await websocket.SendText(finalJson);
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