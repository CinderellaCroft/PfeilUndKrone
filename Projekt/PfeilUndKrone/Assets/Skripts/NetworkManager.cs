using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System;
using PimDeWitte.UnityMainThreadDispatcher; // Make sure you have imported this asset

/// <summary>
/// This class is used to structure messages SENT TO the server.
/// The payload is a JSON string, not a complex object.
/// </summary>
[Serializable]
class ClientMessage
{
    public string type;
    public string payload;
}

// A simple class to determine the message type before full deserialization
[Serializable]
public class ServerMessageType
{
    public string type;
}

// Specific message classes for each type from the server
[Serializable]
public class ServerMessageMatchCreated
{
    public string type;
    public MatchCreatedPayload payload;
}

[Serializable]
public class ServerMessageResourceUpdate
{
    public string type;
    public ResourcePayload payload;
}

[Serializable]
public class ServerMessageAmbushApproved
{
    public string type;
    public AmbushEdge payload;
}

[Serializable]
public class ServerMessageExecuteRound
{
    public string type;
    public ExecuteRoundPayload payload;
}

[Serializable]
public class ServerMessagePathApproved
{
    public string type;
    public PathApprovedPayload payload;
}


// For simple string payloads like "info" and "error"
[Serializable]
public class ServerMessageStringPayload
{
    public string type;
    public string payload;
}

/// <summary>
/// A specific class to deserialize the payload of a "path_approved" message.
/// </summary>
[Serializable]
public class PathApprovedPayload
{
    public List<CornerCoord> path;
}


[Serializable]
public class MatchCreatedPayload
{
    public string role;
    // public MapData map; // Add later
}

[Serializable]
public class ResourcePayload
{
    public int gold;
    public int wood;
    public int grain;
}

[Serializable]
public class ExecuteRoundPayload
{
    public List<PathData> kingPaths;
    public List<AmbushEdge> banditAmbushes;
    // public List<Outcome> outcomes; // falls du Outcomes noch brauchst
}



public class NetworkManager : MonoBehaviour
{
    // --- A robust Singleton pattern to prevent script execution order issues ---
    private static NetworkManager _instance;
    public static NetworkManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find the instance in the scene.
                _instance = FindFirstObjectByType<NetworkManager>();

                // If it's still null, it means the GameObject with this script is missing.
                if (_instance == null)
                {
                    Debug.LogError("FATAL ERROR: An instance of NetworkManager is needed in the scene, but there is none. Please add the NetworkManager script to a GameObject.");
                }
            }
            return _instance;
        }
    }

    private WebSocket websocket;

    void Awake()
    {
        // This enforces the Singleton pattern, ensuring only one instance exists.
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scene loads.
        }
        else if (_instance != this)
        {
            // If another instance already exists, destroy this duplicate.
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        // Connect to the port specified in your server.js (8080)
        websocket = new WebSocket("ws://localhost:8080");

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection to server opened!");
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
                    case "match_created":
                        var matchMessage = JsonUtility.FromJson<ServerMessageMatchCreated>(messageString);
                        Debug.Log(GameManager.Instance == null ? "GameManager is not set up!" : "GameManager is ready.");
                        GameManager.Instance.SetRole(matchMessage.payload.role);
                        break;

                    case "match_found":
                        Debug.Log("Match found! Waiting for other players...");
                        UIManager.Instance.UpdateInfoText("Match found! Waiting for other players...");
                        break;

                    case "king_turn_start":
                        GameManager.Instance.StartKingTurn();
                        break;

                    case "bandit_turn_start":
                        GameManager.Instance.StartBanditTurn();
                        break;

                    case "ambush_approved":
                        var ambushMessage = JsonUtility.FromJson<ServerMessageAmbushApproved>(messageString);
                        AmbushManager.Instance.ConfirmAmbushPlacement(ambushMessage.payload);
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
                        var execMsg = JsonUtility.FromJson<ServerMessageExecuteRound>(messageString);

                        // 2) Gib es an den GameManager weiter (mit echten Listen, nicht JSON-Strings)
                        GameManager.Instance.StartExecutionPhase(
                            execMsg.payload.kingPaths,
                            execMsg.payload.banditAmbushes
                        );

                        // 3) Nun direkt den ersten King-Pfad ausführen
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            if (execMsg.payload.kingPaths != null && execMsg.payload.kingPaths.Count > 0)
                            {
                                var firstPath = execMsg.payload.kingPaths[0].path;
                                Debug.Log($"[NM] Führe ersten King-Pfad aus mit {firstPath.Count} Ecken.");
                                CornerPathManager.Instance.ExecuteServerPath(firstPath);
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
    public async void Send(string type, object payloadObject)
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