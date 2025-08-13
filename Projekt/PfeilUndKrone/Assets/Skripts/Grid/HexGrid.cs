using System;
using System.Linq;
using UnityEngine;

public struct Hex : IEquatable<Hex>
{
    public int Q; // axial q
    public int R; // axial r

    public Hex(int q, int r)
    {
        Q = q;
        R = r;
    }

    public int S => -Q - R; // cube s, derived from q and r (q + r + s = 0)

    public Vector3 ToWorld(float hexSize = 1f)
    {
        float x = hexSize * (Mathf.Sqrt(3) * Q + Mathf.Sqrt(3) / 2 * R);
        float y = 0;
        float z = hexSize * (3f / 2 * R);
        return new Vector3(x, y, z);
    }

    public override string ToString() => $"Hex({Q}, {R})";

    // Equality members for HashSet support
    public bool Equals(Hex other) => Q == other.Q && R == other.R;
    public override bool Equals(object obj) => obj is Hex other && Equals(other);
    public override int GetHashCode() => unchecked(Q * 397 ^ R);
}

public enum HexDirection
{
    Right = 0,
    TopRight = 1,
    TopLeft = 2,
    Left = 3,
    BottomLeft = 4,
    BottomRight = 5
}

public enum VertexDirection
{
    Top = 0,
    TopRight = 1,
    BottomRight = 2,
    Bottom = 3,
    BottomLeft = 4,
    TopLeft = 5
}

public struct HexEdge : IEquatable<HexEdge>
{
    public Hex Hex;
    public HexDirection Direction;

    public HexEdge(Hex hex, HexDirection direction)
    {
        Hex = hex;
        Direction = direction;
    }

    public Vector3 ToWorld(float hexSize = 1f)
    {
        Vector3 a = Hex.ToWorld(hexSize);
        Vector3 b = GetNeighbor().ToWorld(hexSize);
        return (a + b) / 2f;
    }

    public override string ToString() => $"Edge({Hex}, {Direction})";

    public Hex GetNeighbor() => HexNeighbor(Hex, Direction);

    public static Hex HexNeighbor(Hex hex, HexDirection dir)
    {
        (int dq, int dr) = dir switch
        {
            HexDirection.Right => (1, 0),
            HexDirection.TopRight => (0, 1),
            HexDirection.TopLeft => (-1, 1),
            HexDirection.Left => (-1, 0),
            HexDirection.BottomLeft => (0, -1),
            HexDirection.BottomRight => (1, -1),
            _ => (0, 0)
        };
        return new Hex(hex.Q + dq, hex.R + dr);
    }

    public HexVertex[] GetVertexEndpoints()
    {
        // Mapping edge directions to their endpoint vertex directions
        VertexDirection[][] edgeToVertices = new[]
        {
            new[]{ VertexDirection.TopRight,    VertexDirection.BottomRight },  // Right
            new[]{ VertexDirection.Top,         VertexDirection.TopRight    },  // TopRight
            new[]{ VertexDirection.TopLeft,     VertexDirection.Top        },  // TopLeft
            new[]{ VertexDirection.BottomLeft,  VertexDirection.TopLeft    },  // Left
            new[]{ VertexDirection.Bottom,      VertexDirection.BottomLeft },  // BottomLeft
            new[]{ VertexDirection.BottomRight, VertexDirection.Bottom     }   // BottomRight
        };
        var dirs = edgeToVertices[(int)Direction];
        return new[] { new HexVertex(Hex, dirs[0]), new HexVertex(Hex, dirs[1]) };
    }

    // Equality members for HashSet support (undirected edge)
    public bool Equals(HexEdge other)
    {
        var h1 = Hex;
        var h2 = GetNeighbor();
        var o1 = other.Hex;
        var o2 = other.GetNeighbor();
        // order-independent comparison
        return (h1.Equals(o1) && h2.Equals(o2)) || (h1.Equals(o2) && h2.Equals(o1));
    }

    public override bool Equals(object obj) => obj is HexEdge other && Equals(other);

    public override int GetHashCode()
    {
        var h1 = Hex;
        var h2 = GetNeighbor();
        // combine hash codes order-independently
        int hash1 = h1.GetHashCode();
        int hash2 = h2.GetHashCode();
        return hash1 ^ hash2;
    }
}

public struct HexVertex : IEquatable<HexVertex>
{
    public Hex Hex;
    public VertexDirection Direction;

    public HexVertex(Hex hex, VertexDirection direction)
    {
        Hex = hex;
        Direction = direction;
    }

    public Vector3 ToWorld(float hexSize = 1f)
    {
        Hex[] neighbors = GetAdjacentHexes();
        Vector3 sum = Vector3.zero;
        foreach (var h in neighbors)
        {
            sum += h.ToWorld(hexSize);
        }
        return sum / neighbors.Length;
    }

    public override string ToString() => $"Vertex({Hex}, {Direction})";

    public Hex[] GetAdjacentHexes()
    {
        return Direction switch
        {
            VertexDirection.Top => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.TopRight), HexEdge.HexNeighbor(Hex, HexDirection.TopLeft) },
            VertexDirection.TopRight => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.TopRight), HexEdge.HexNeighbor(Hex, HexDirection.Right) },
            VertexDirection.BottomRight => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.Right), HexEdge.HexNeighbor(Hex, HexDirection.BottomRight) },
            VertexDirection.Bottom => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.BottomRight), HexEdge.HexNeighbor(Hex, HexDirection.BottomLeft) },
            VertexDirection.BottomLeft => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.BottomLeft), HexEdge.HexNeighbor(Hex, HexDirection.Left) },
            VertexDirection.TopLeft => new[] { Hex, HexEdge.HexNeighbor(Hex, HexDirection.Left), HexEdge.HexNeighbor(Hex, HexDirection.TopLeft) },
            _ => new[] { Hex }
        };
    }

    // Equality members for HashSet support (undirected vertex)
    public bool Equals(HexVertex other)
    {
        // get sorted adjacent hexes for both
        var a = GetAdjacentHexes()
            .OrderBy(h => h.Q).ThenBy(h => h.R)
            .ToArray();
        var b = other.GetAdjacentHexes()
            .OrderBy(h => h.Q).ThenBy(h => h.R)
            .ToArray();
        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }

    public override bool Equals(object obj) => obj is HexVertex other && Equals(other);

    public override int GetHashCode()
    {
        var arr = GetAdjacentHexes()
            .OrderBy(h => h.Q).ThenBy(h => h.R)
            .ToArray();
        unchecked
        {
            int hash = 17;
            foreach (var h in arr)
                hash = hash * 31 + h.GetHashCode();
            return hash;
        }
    }
}
