using System.Collections.Generic;
using UnityEngine;

public class GridVisualsManager : MonoBehaviour
{
    public HexGridGenerator gridGenerator;
    public InteractionManager interactionManager;

    public GameObject castlePrefab;
    public GameObject simpleFieldPrefab;
    public List<GameObject> resourcePrefabs;
    public GameObject vertexMarkerPrefab;
    public GameObject edgeMarkerPrefab;

    private Dictionary<Hex, GameObject> hexObjects = new();

    public void InitializeVisuals()
    {
        ClearPrevious();
        float radius = gridGenerator.hexRadius;

        foreach (var hex in gridGenerator.Model.AllHexes)
        {
            GameObject prefab;
            if (hex.Equals(new Hex(0, 0))) prefab = castlePrefab;
            else if (IsNeighbor(new Hex(0, 0), hex)) prefab = simpleFieldPrefab;
            else prefab = resourcePrefabs[Random.Range(0, resourcePrefabs.Count)];

            var go = Instantiate(prefab, hex.ToWorld(radius), Quaternion.identity, transform);
            go.name = $"Hex_{hex.Q}_{hex.R}";
            var hm = go.AddComponent<HexMarker>(); hm.hex = hex; hm.interaction = interactionManager;
            hexObjects[hex] = go;
        }

        foreach (var vertex in gridGenerator.Model.AllVertices)
        {
            var pos = vertex.ToWorld(radius);
            var go = Instantiate(vertexMarkerPrefab, pos, Quaternion.identity, transform);
            go.name = $"Vertex_{vertex}";
            var vm = go.AddComponent<VertexMarker>(); vm.vertex = vertex; vm.interaction = interactionManager;
        }

        foreach (var edge in gridGenerator.Model.AllEdges)
        {
            var pos = edge.ToWorld(radius);
            var go = Instantiate(edgeMarkerPrefab, pos, Quaternion.identity, transform);
            go.name = $"Edge_{edge}";
            var em = go.AddComponent<EdgeMarker>(); em.edge = edge; em.interaction = interactionManager;
            go.SetActive(false);
        }
    }

    bool IsNeighbor(Hex a, Hex b)
    {
        int dq = Mathf.Abs(a.Q - b.Q);
        int dr = Mathf.Abs(a.R - b.R);
        int ds = Mathf.Abs(a.S - b.S);
        return Mathf.Max(dq, dr, ds) == 1;
    }

    void ClearPrevious()
    {
        foreach (var go in hexObjects.Values) Destroy(go);
        hexObjects.Clear();
        foreach (var vm in FindObjectsByType<VertexMarker>(FindObjectsSortMode.None)) Destroy(vm.gameObject);
        foreach (var em in FindObjectsByType<EdgeMarker>(FindObjectsSortMode.None)) Destroy(em.gameObject);
    }
}