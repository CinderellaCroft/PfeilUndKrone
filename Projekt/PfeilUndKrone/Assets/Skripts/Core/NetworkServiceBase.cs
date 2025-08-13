using System;
using System.Collections.Generic;
using UnityEngine;
using NetworkingDTOs;

public abstract class NetworkServiceBase : MonoBehaviour
{
    public event Action OnGridDataReady;
    public event Action<List<ResourceData>> OnResourceMapReceived;
    public event Action<HexEdge> OnAmbushConfirmed;

    protected void RaiseGridDataReady()
        => OnGridDataReady?.Invoke();

    protected void RaiseResourceMapReceived(List<ResourceData> map)
        => OnResourceMapReceived?.Invoke(map);

    protected void RaiseAmbushConfirmed(HexEdge edge)
        => OnAmbushConfirmed?.Invoke(edge);

    public abstract void Send(string type, object payload);
}
