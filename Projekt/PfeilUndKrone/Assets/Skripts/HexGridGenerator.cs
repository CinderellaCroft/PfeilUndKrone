using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    public static HexGridGenerator Instance;

    [Header("Field Prefabs")]
    public GameObject castlePrefab;
    public GameObject simpleFieldPrefab;
    public GameObject[] resourcePrefabs;

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
        int gridRadius = 3;

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        allCorners.Clear();
        centralCorners.Clear();

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 center = HexToWorld(q, r, hexRadius);

                int hexDist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(-q - r)) / 2;

                GameObject fieldPrefab;
                if (hexDist == 0)
                    fieldPrefab = castlePrefab;
                else if (hexDist == 1)
                    fieldPrefab = simpleFieldPrefab;
                else
                    fieldPrefab = resourcePrefabs[Random.Range(0, resourcePrefabs.Length)];

                GameObject hex = Instantiate(
                    fieldPrefab,
                    center,
                    fieldPrefab.transform.rotation,
                    transform
                );
                hex.name = $"Hex_{q}_{r}";

                Vector3[] corners = GetHexCorners(center, hexRadius);

                if (q == 0 && r == 0)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        CornerNode cn = FindOrCreateCorner(0, 0, i, corners[i]);
                        centralCorners.Add(cn);
                    }
                }

                // 2.7) Graph-Verknüpfung: für jedes Hex die Nachbarn verbinden
                for (int i = 0; i < 6; i++)
                {
                    // Eck-Koordinaten (q,r,i) und (q,r,(i+1)%6)
                    CornerNode nodeA = FindOrCreateCorner(q, r, i, corners[i]);
                    CornerNode nodeB = FindOrCreateCorner(q, r, (i + 1) % 6, corners[(i + 1) % 6]);

                    // Gegenseitige Nachbarschaft
                    if (!nodeA.neighbors.Contains(nodeB)) nodeA.neighbors.Add(nodeB);
                    if (!nodeB.neighbors.Contains(nodeA)) nodeB.neighbors.Add(nodeA);
                }
            }
        }

        // 3) Abschließend die sichtbaren Corner-Marker hinzufügen
        CreateCornerVisuals();
    }


    public Vector3 HexToWorld(int q, int r, float size)
    {
        float x = size * Mathf.Sqrt(3f) * (q + r / 2f);
        float z = size * 1.5f * r;
        return new Vector3(x, 0f, z);
    }

    public Vector3[] GetHexCorners(Vector3 center, float radius)
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

    CornerNode FindOrCreateCorner(int q, int r, int i, Vector3 pos, float epsilon = 0.01f)
    {
        // Suche nach bestehendem Knoten an derselben Welt‐Position
        foreach (var node in allCorners)
            if (Vector3.Distance(node.position, pos) < epsilon)
                return node;

        // Wenn nicht gefunden: neuen anlegen mit (q,r,i)
        var newNode = new CornerNode(q, r, i, pos);
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
