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

public enum ResourceType { Wood, Wheat, Ore, Desert }

public class GridVisualsManager : Singleton<GridVisualsManager>
{
    [Header("References")]
    public HexGridGenerator gridGenerator;
    public InteractionManager interactionManager;

    [Header("Containers")]
    public Transform hexFieldContainer;

    [Header("King Prefabs")]
    public GameObject KingCastlePrefab;
    public GameObject KingMoatPrefab;
    public List<ResourcePrefabEntry> resourcePrefabs;

    [Header("Rebel Prefabs")]
    public GameObject RebelCastlePrefab;
    public GameObject RebelMoatPrefab;
    public List<GameObject> unknownResourcePrefabs;

    [Header("Other Prefabs")]
    public GameObject desertPrefab;
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

    public void InitializeVisuals(Dictionary<Hex, FieldType> map)
    {
        ClearPrevious();

        if (gridGenerator == null) { Debug.LogError("GridVisualsManager: gridGenerator NULL"); return; }
        if (hexFieldContainer == null) { Debug.LogError("GridVisualsManager: hexFieldContainer NULL"); return; }
        if (desertPrefab == null) { Debug.LogError("GridVisualsManager: desertPrefab NULL"); return; }
        if (resourceMap == null) { Debug.LogError("GridVisualsManager: prefab map (resourceMap) NULL"); return; }

        float radius = gridGenerator.hexRadius;

        foreach (var t in resourceMap)
            if (t.Value == null)
                Debug.LogError($"Missing/NULL prefab mapping for {t.Key}");

        foreach (var hex in gridGenerator.Model.AllHexes)
        {
            var role = GameManager.Instance.MyRole;
            var prefab = ResolveFieldPrefab(hex, map, role);

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

    private static bool IsCastleField(FieldType f) => f == FieldType.Castle;
    private static bool IsMoatField(FieldType f) => f == FieldType.Moat;
    private static bool TryFieldAsResource(FieldType f, out ResourceType r)
    {
        if (f == FieldType.Castle || f == FieldType.Moat)
        {
            r = default;
            return false;
        }

        if (System.Enum.TryParse(f.ToString(), out r))
            return true;

        return false;
    }

    private GameObject ResolveFieldPrefab(
    Hex hex,
    Dictionary<Hex, FieldType> map,
    PlayerRole role)
    {
        if (map != null && map.TryGetValue(hex, out var ft))
        {
            if (IsCastleField(ft))
                return role == PlayerRole.King ? KingCastlePrefab : RebelCastlePrefab;

            if (IsMoatField(ft))
                return role == PlayerRole.King ? KingMoatPrefab : RebelMoatPrefab;

            if (TryFieldAsResource(ft, out var rt)
                && resourceMap != null
                && resourceMap.TryGetValue(rt, out var resPrefab)
                && resPrefab != null)
            {
                return resPrefab;
            }

            return desertPrefab;
        }

        if (role == PlayerRole.Rebel && unknownResourcePrefabs != null && unknownResourcePrefabs.Count > 0)
            return unknownResourcePrefabs[Random.Range(0, unknownResourcePrefabs.Count)];

        return desertPrefab;
    }

}