using System.Collections.Generic;
using System.Linq;
using NetworkingDTOs;
using UnityEngine;

[System.Serializable]
public struct ResourcePrefabEntry
{
    public ResourceType type;
    public GameObject prefab;
}

public class GridVisualsManager : Singleton<GridVisualsManager>
{
    [Header("References")]
    public HexGridGenerator gridGenerator;
    public InteractionManager interactionManager;

    [Header("Containers")]
    public Transform hexFieldContainer;

    [Header("Prefabs")]

    public GameObject desertPrefab;
    public List<ResourcePrefabEntry> resourcePrefabs;
    public List<GameObject> unknownResourcePrefabs;
    public GameObject vertexMarkerPrefab;
    public GameObject edgeMarkerPrefab;

    [HideInInspector]
    private Dictionary<Hex, GameObject> hexObjects = new();
    private Dictionary<HexEdge, GameObject> hexEdgeObjects = new();
    private Dictionary<HexVertex, GameObject> hexVertexObjects = new();
    private Dictionary<ResourceType, GameObject> resourceMap;

    protected override void Awake()
    {
        base.Awake();
        resourceMap = resourcePrefabs
            .ToDictionary(e => e.type, e => e.prefab);
    }

    public void InitializeVisuals(Dictionary<Hex, ResourceType> map)
    {
        ClearPrevious();
        float radius = gridGenerator.hexRadius;

        foreach (var hex in gridGenerator.Model.AllHexes)
        {
            GameObject prefab;
            if (map.TryGetValue(hex, out var resType)) prefab = resourceMap[resType];
            else prefab = desertPrefab;

            var go = Instantiate(prefab, hex.ToWorld(radius), Quaternion.identity, hexFieldContainer);
            go.name = hex.ToString();
            var hm = go.AddComponent<HexMarker>(); hm.hex = hex; hm.interaction = interactionManager;
            hexObjects[hex] = go;
        }

        foreach (var vertex in gridGenerator.Model.AllVertices)
        {
            var pos = vertex.ToWorld(radius);
            var go = Instantiate(vertexMarkerPrefab, pos, Quaternion.identity, hexFieldContainer);
            go.name = vertex.ToString();
            var vm = go.AddComponent<VertexMarker>(); vm.vertex = vertex; vm.interaction = interactionManager;
            hexVertexObjects[vertex] = go;
        }

        foreach (var edge in gridGenerator.Model.AllEdges)
        {
            var pos = edge.ToWorld(radius);
            var go = Instantiate(edgeMarkerPrefab, pos, Quaternion.identity, hexFieldContainer);
            go.name = edge.ToString();
            var em = go.AddComponent<EdgeMarker>(); em.edge = edge; em.interaction = interactionManager;
            hexEdgeObjects[edge] = go;
            go.SetActive(false);
        }
    }

    void ClearPrevious()
    {
        foreach (var go in hexObjects.Values) Destroy(go);
        hexObjects.Clear();
        foreach (var vm in FindObjectsByType<VertexMarker>(FindObjectsSortMode.None)) Destroy(vm.gameObject);
        foreach (var em in FindObjectsByType<EdgeMarker>(FindObjectsSortMode.None)) Destroy(em.gameObject);
    }
}