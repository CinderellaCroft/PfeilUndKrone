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
    /// 
    /// 
    [Serializable]
    public struct ResourceData { public int q; public int r; public ResourceType resource; }

    [Serializable]
    public class ServerMessageResourceMap
    {
        public string type;          // "resource_map"
        public int seed;             // forwarded from server
        public List<ResourceData> map;
    }

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


    [Serializable]
    public class ExecuteRoundPayload
    {
        public List<HexEdge> kingPaths;
        public List<HexEdge> banditAmbushes;
        public List<HexEdge> outcome;
        public ResourcePayload winnerResourceUpdate;

        public string winner;
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



}
