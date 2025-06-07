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

/// <summary>
/// This class is used to deserialize the outer layer of messages RECEIVED FROM the server.
/// The payload is a JSON string that we will deserialize separately.
/// </summary>
[Serializable]
public class ServerMessage
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
    public List<Vector3> path;
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

            // First, deserialize the outer message to determine its type
            ServerMessage message = JsonUtility.FromJson<ServerMessage>(messageString);

            // Route the message based on its type
            switch (message.type)
            {
                case "path_approved":
                    // Now, deserialize the inner payload string into our specific data class
                    PathApprovedPayload pathPayload = JsonUtility.FromJson<PathApprovedPayload>(message.payload);
                    
                    // We must use the Main Thread Dispatcher to interact with GameObjects
                    // from a network thread to avoid errors.
                    UnityMainThreadDispatcher.Instance().Enqueue(() => 
                    {
                        CornerPathManager.Instance.ExecuteServerPath(pathPayload.path);
                    });
                    break;
                
                // You can add more cases here for future server events
                // case "game_state_update":
                //     ...
                //     break;
                    
                default:
                    Debug.LogWarning("Received message of unknown type: " + message.type);
                    break;
            }
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