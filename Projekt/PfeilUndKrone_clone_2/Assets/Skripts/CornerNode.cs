using System.Collections.Generic;
using UnityEngine;

public class CornerNode
{
    // Netz‐Position
    public int hexQ, hexR;
    public int cornerIndex;

    // Spiel‐Position (Unity‐Koordinate)
    public Vector3 position;

    public List<CornerNode> neighbors = new();

    public CornerNode(int q, int r, int i, Vector3 pos)
    {
        hexQ = q;
        hexR = r;
        cornerIndex = i;
        position = pos;
    }

    // Gibt das Tripel zurück, das wir netzwerken
    public CornerCoord ToCoord() => new CornerCoord(hexQ, hexR, cornerIndex);
}

[System.Serializable]
public struct CornerCoord
{
    public int q;
    public int r;
    public int i;  // Eck‐Index 0..5

    public CornerCoord(int q, int r, int i)
    {
        this.q = q;
        this.r = r;
        this.i = i;
    }
}
