using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System;
using PimDeWitte.UnityMainThreadDispatcher; // Make sure you have imported this asset

/// <summary>
/// This class is used to structure messages SENT TO the server.
/// The payload is a JSON string, not a complex object.
/// </summary>


namespace NetworkingDTOs
{

    [Serializable]
    public class ClientMessage
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


    /////////////////////////////////////
    //////// Resources Received ///////// 
    /////////////////////////////////////
    [Serializable]
    public class ServerMessageResourceMap
    {
        public string type;
        public ResourceMapPayload payload;
    }

    [Serializable]
    public class ResourceMapPayload
    {
        public long seed;
        public List<ResourceDataJson> map;
    }

    // Incoming version (resource still a string!)
    [Serializable]
    public class ResourceDataJson
    {
        public int q;
        public int r;
        public string resource;
    }

    // Internal version you actually use everywhere
    [Serializable]
    public struct ResourceData
    {
        public int q;
        public int r;
        public FieldType resource;
    }

    // [Serializable]
    // public struct ResourceData { public int q; public int r; public ResourceType resource; }

    // [Serializable] class ServerMessageResourceMap { public string type; public ResourceMapPayload payload; }
    // [Serializable] class ResourceMapPayload { public long seed; public List<ResourceData> map; }


    /////////////////////////////////////
    ///////////////////////////////////// 
    /////////////////////////////////////

    [Serializable]
    public class ResourcePayload
    {
        public int gold;
        public int wood;
        public int grain;
    }

    [Serializable]
    public class NewRoundPayload
    {
        public int roundNumber;
        public ResourcePayload resources;
    }

    [Serializable]
    public class ServerMessageNewRound
    {
        public string type;
        public NewRoundPayload payload;
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
        public HexEdge payload;
    }

    // kingPaths: gs.king.submittedPaths,
    // banditAmbushes: gs.bandit.submittedAmbushes,
    // banditBonus: { ...gameState.bandit.resources },
    // kingBonus: { ...gameState.king.resources },
    // outcome: outcome,


    [Serializable]
    public class ExecuteRoundPayload
    {
        public SerializablePathData[] kingPaths; // Array of path objects from server
        public SerializableAmbushEdge[] banditAmbushes; // Changed from List<AmbushEdge> to match server format

        public ResourcePayload banditBonus;
        public ResourcePayload kingBonus;

        public List<HexEdge> outcome; //wird nicht mehr verwendet??
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
        public List<HexVertex> path;
    }


    [Serializable]
    public class MatchCreatedPayload
    {
        public string role;
        // public MapData map; // Add later
    }



    [Serializable]
    public class PathData
    {
        public List<HexVertex> path;
    }

    [Serializable]
    public class SerializableHexVertex
    {
        public int q;
        public int r;
        public int direction; // VertexDirection as int

        public SerializableHexVertex() { }

        public SerializableHexVertex(HexVertex vertex)
        {
            q = vertex.Hex.Q;
            r = vertex.Hex.R;
            direction = (int)vertex.Direction;
        }

        public HexVertex ToHexVertex()
        {
            return new HexVertex(new Hex(q, r), (VertexDirection)direction);
        }
    }

    [Serializable]
    public class SerializablePathData
    {
        public SerializableHexVertex[] path;
        public int resourceFieldQ;
        public int resourceFieldR;
        public string resourceType;
    }

    [Serializable]
    public class PlaceWorkersPayload
    {
        public SerializablePathData[] paths; // Array of path objects
    }

    [Serializable]
    public class AmbushEdge
    {
        public HexVertex cornerA;
        public HexVertex cornerB;
    }

    [Serializable]
    public class SerializableAmbushEdge
    {
        public SerializableHexVertex cornerA;
        public SerializableHexVertex cornerB;

        public SerializableAmbushEdge() { }

        public SerializableAmbushEdge(AmbushEdge edge)
        {
            cornerA = new SerializableHexVertex(edge.cornerA);
            cornerB = new SerializableHexVertex(edge.cornerB);
        }

        public AmbushEdge ToAmbushEdge()
        {
            return new AmbushEdge
            {
                cornerA = cornerA.ToHexVertex(),
                cornerB = cornerB.ToHexVertex()
            };
        }
    }

    [Serializable]
    public class PlaceAmbushesPayload
    {
        public SerializableAmbushEdge[] ambushes;
    }

    [Serializable]
    public class LobbyJoinedPayload
    {
        public string lobby_id;
        public bool queued;
    }

    [Serializable]
    public class ServerMessageLobbyJoinedRandomly
    {
        public string type;
        public LobbyJoinedPayload payload;
    }

    [Serializable]
    public class ServerMessageLobbyCreated
    {
        public string type;
        public string lobbyID;
    }

    [Serializable]
    public class ServerMessageLobbyJoinedById
    {
        public string type;
        public LobbyJoinedPayload payload;
    }

    // Add these new classes to the end of NetworkingDTOs.cs

    [Serializable]
    public class GameOverPayload
    {
        public string winner;
        public string reason;
    }

    [Serializable]
    public class ServerMessageGameOver
    {
        public string type;
        public GameOverPayload payload;
    }
}
