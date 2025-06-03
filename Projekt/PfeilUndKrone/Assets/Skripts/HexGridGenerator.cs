using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    public static HexGridGenerator Instance;

    public GameObject hexTilePrefab;
    public GameObject cornerMarkerPrefab;
    public float hexRadius = 1f;

    public List<CornerNode> allCorners = new();

    [HideInInspector]
    public List<CornerNode> centralCorners = new();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        int gridRadius = 2;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        allCorners.Clear();
        centralCorners.Clear();

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 position = HexToWorld(q, r, hexRadius);
                GameObject hex = Instantiate(hexTilePrefab, position, hexTilePrefab.transform.rotation, transform);
                hex.name = $"Hex_{q}_{r}";

                Vector3[] corners = GetHexCorners(position, hexRadius);
                if (q == 0 && r == 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        CornerNode centralNode = FindOrCreateCorner(corners[i]);
                        if (!centralCorners.Contains(centralNode))
                            centralCorners.Add(centralNode);
                    }
                }

                for (int i = 0; i < 6; i++)
                {
                    CornerNode nodeA = FindOrCreateCorner(corners[i]);
                    CornerNode nodeB = FindOrCreateCorner(corners[(i + 1) % 6]);
                    if (!nodeA.neighbors.Contains(nodeB)) nodeA.neighbors.Add(nodeB);
                    if (!nodeB.neighbors.Contains(nodeA)) nodeB.neighbors.Add(nodeA);
                }
            }
        }

        CreateCornerVisuals();
    }

    Vector3 HexToWorld(int q, int r, float size)
    {
        float x = size * Mathf.Sqrt(3f) * (q + r / 2f);
        float z = size * 1.5f * r;
        return new Vector3(x, 0f, z);
    }

    Vector3[] GetHexCorners(Vector3 center, float radius)
    {
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60 * i - 30;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            corners[i] = new Vector3(
                center.x + radius * Mathf.Cos(angleRad),
                center.y,
                center.z + radius * Mathf.Sin(angleRad)
            );
        }
        return corners;
    }

    CornerNode FindOrCreateCorner(Vector3 pos, float epsilon = 0.01f)
    {
        foreach (CornerNode node in allCorners)
        {
            if (Vector3.Distance(node.position, pos) < epsilon)
                return node;
        }
        var newNode = new CornerNode(pos);
        allCorners.Add(newNode);
        return newNode;
    }

    void CreateCornerVisuals()
    {
        foreach (CornerNode node in allCorners)
        {
            GameObject marker = Instantiate(cornerMarkerPrefab, node.position, Quaternion.identity, transform);
            marker.name = $"Corner_{node.position}";

            Debug.Log($"Marker erstellt: {marker.name} mit Komponenten:");
            foreach (var comp in marker.GetComponents<Component>())
                Debug.Log(" → " + comp.GetType());

            var cm = marker.GetComponent<CornerMarker>();
            if (cm == null)
            {
                Debug.LogError($"[HexGridGenerator] Prefab '{cornerMarkerPrefab.name}' hat kein Script");
                cm = marker.AddComponent<CornerMarker>();
            }
            cm.node = node;
        }
    }


}
