using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : Singleton<HexGridGenerator>
{
    protected override bool Persistent => false; // Don't persist across scenes

    [HideInInspector] public HexGridModel Model { get; private set; }
    public float hexRadius = 1f;

    protected override void Awake()
    {
        base.Awake();
        Model = new HexGridModel();
    }

    // q: | r:
    // -3 |        0   1   2   3
    // -2 |     -1   0   1   2   3
    // -1 |   -2  -1   0   1   2   3
    //  0 | -3  -2  -1   0   1   2   3
    //  1 |   -3  -2  -1   0   1   2
    //  2 |     -3  -2  -1   0   1
    //  3 |       -3  -2  -1   0
    public void GenerateGrid()
    {
        int gridRadius = 3;

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        Model.AllEdges.Clear();
        Model.AllVertices.Clear();
        Model.AllHexes.Clear();

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                Hex hexCoord = new Hex(q, r);
                if (!Model.AllHexes.Contains(hexCoord))
                    Model.AllHexes.Add(hexCoord);

                for (int i = 0; i < 6; i++)
                {
                    var edge = new HexEdge(hexCoord, (HexDirection)i);
                    if (!Model.AllEdges.Contains(edge))
                        Model.AllEdges.Add(edge);
                }

                for (int i = 0; i < 6; i++)
                {
                    var vertex = new HexVertex(hexCoord, (VertexDirection)i);
                    if (!Model.AllVertices.Contains(vertex))
                        Model.AllVertices.Add(vertex);
                }
            }
        }
    }
}
