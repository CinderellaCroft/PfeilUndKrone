using System.Collections.Generic;

public class HexGridModel
{
    public HashSet<Hex> AllHexes { get; } = new();
    public HashSet<HexEdge> AllEdges { get; } = new();
    public HashSet<HexVertex> AllVertices { get; } = new();
}
