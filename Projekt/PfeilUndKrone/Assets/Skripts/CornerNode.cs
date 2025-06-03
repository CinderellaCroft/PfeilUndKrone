using System.Collections.Generic;
using UnityEngine;
public class CornerNode
{
    public Vector3 position;
    public List<CornerNode> neighbors = new();

    public CornerNode(Vector3 pos)
    {
        position = pos;
    }
}
