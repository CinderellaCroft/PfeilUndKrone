using System;
using System.Collections.Generic;
using UnityEngine;
using NetworkingDTOs;
using System.Threading.Tasks;

public abstract class NetworkServiceBase : MonoBehaviour
{
    public event Action<string> OnRoleAssigned;
    public event Action OnGridDataReady;
    public event Action<List<ResourceData>> OnResourceMapReceived;
    public event Action<HexEdge> OnAmbushConfirmed;

    protected void RaiseMatchCreated(string roleName)
        => OnRoleAssigned?.Invoke(roleName);
    protected void RaiseGridDataReady()
        => OnGridDataReady?.Invoke();

    protected void RaiseResourceMapReceived(List<ResourceData> map)
        => OnResourceMapReceived?.Invoke(map);

    protected void RaiseAmbushConfirmed(HexEdge edge)
        => OnAmbushConfirmed?.Invoke(edge);

    public abstract void Send(string type, object payload);
    public abstract Task Connect();
    public abstract Task Disconnect();
    public abstract bool IsConnected { get; }
}
