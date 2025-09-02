using System.Collections.Generic;
using System.Linq;
using NetworkingDTOs;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Bandit Prefabs")]
    [FormerlySerializedAs("RebelCastlePrefab")] public GameObject BanditCastlePrefab;
    [FormerlySerializedAs("RebelMoatPrefab")] public GameObject BanditMoatPrefab;
    public List<GameObject> unknownResourcePrefabs;

    [Header("Other Prefabs")]
    public GameObject desertPrefab;
    public GameObject vertexMarkerPrefab;
    public GameObject edgeMarkerPrefab;

    [Header("Rotation Settings")]
    [Tooltip("Optionaler Feintuning-Offset, falls das Prefab lokal nicht exakt auf die Weltachse zeigt.")]
    public float moatRotationOffsetY = 0f;

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

    public void Bind(MainBindings b)
    {
        hexFieldContainer = b.hexFieldContainer;

        // If these were unassigned because the singleton was created in Title,
        // take them from the scene bindings:
        if (KingCastlePrefab == null) KingCastlePrefab = b.KingCastlePrefab;
        if (KingMoatPrefab == null) KingMoatPrefab = b.KingMoatPrefab;
        if (resourcePrefabs == null || resourcePrefabs.Count == 0) resourcePrefabs = b.resourcePrefabs;

        if (BanditCastlePrefab == null) BanditCastlePrefab = b.BanditCastlePrefab;
        if (BanditMoatPrefab == null) BanditMoatPrefab = b.BanditMoatPrefab;
        if (unknownResourcePrefabs == null || unknownResourcePrefabs.Count == 0)
            unknownResourcePrefabs = b.unknownResourcePrefabs;

        if (desertPrefab == null) desertPrefab = b.desertPrefab;
        if (vertexMarkerPrefab == null) vertexMarkerPrefab = b.vertexMarkerPrefab;
        if (edgeMarkerPrefab == null) edgeMarkerPrefab = b.edgeMarkerPrefab;
    }

    public void InitializeVisuals(Dictionary<Hex, FieldType> map)
    {
        ClearPrevious();

        if (gridGenerator == null) { Debug.LogError("GridVisualsManager: gridGenerator NULL"); return; }
        if (hexFieldContainer == null) { Debug.LogError("GridVisualsManager: hexFieldContainer NULL"); return; }
        if (desertPrefab == null) { Debug.LogError("GridVisualsManager: desertPrefab NULL"); return; }
        if (resourceMap == null) { Debug.LogError("GridVisualsManager: prefab map (resourceMap) NULL"); return; }

        Debug.Log("GridVisualsManager: InitializeVisuals() called");

        float radius = gridGenerator.hexRadius;

        foreach (var t in resourceMap)
            if (t.Value == null)
                Debug.LogError($"Missing/NULL prefab mapping for {t.Key}");

        foreach (var hex in gridGenerator.Model.AllHexes)
        {
            var role = GameManager.Instance.MyRole;

            FieldType ft = default;
            bool hasField = map != null && map.TryGetValue(hex, out ft);

            var (prefab, rot) = ResolveFieldVisual(hex, ft, hasField, role, radius);

            var go = Instantiate(prefab, hex.ToWorld(radius), rot, hexFieldContainer);
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
            var rot = edge.Rotation;

            var go = Instantiate(edgeMarkerPrefab, pos, rot, hexFieldContainer);
            go.name = edge.ToString();
            var em = go.AddComponent<EdgeMarker>(); em.edge = edge; em.interaction = interactionManager;
            hexEdgeObjects[edge] = go;
            go.SetActive(false);
        }
    }

    public GameObject GetVertexGameObject(HexVertex vertex)
    {
        return hexVertexObjects.TryGetValue(vertex, out var go) ? go : null;
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

    private (GameObject prefab, Quaternion rotation) ResolveFieldVisual(
    Hex hex,
    FieldType ft,
    bool hasField,
    PlayerRole role,
    float hexRadius)
    {
        GameObject prefab;
        Quaternion rotation = Quaternion.identity;

        if (hasField)
        {
            if (IsCastleField(ft))
            {
                prefab = role == PlayerRole.King ? KingCastlePrefab : BanditCastlePrefab;
                rotation = role == PlayerRole.King ? Quaternion.identity : Quaternion.Euler(0f, 120f, 0f);
            }
            else if (IsMoatField(ft))
            {
                prefab = role == PlayerRole.King ? KingMoatPrefab : BanditMoatPrefab;
                rotation = GetMoatRotation(hex, hexRadius);
            }
            else if (TryFieldAsResource(ft, out var rt)
                         && resourceMap != null
                         && resourceMap.TryGetValue(rt, out var resPrefab)
                         && resPrefab != null)
            {
                prefab = resPrefab;
                rotation = Quaternion.Euler(0f, Random.Range(0, 6) * 60f, 0f);
            }
            else
                prefab = desertPrefab;
        }
        else
        {
            if (role == PlayerRole.Bandit && unknownResourcePrefabs != null && unknownResourcePrefabs.Count > 0)
            {
                prefab = unknownResourcePrefabs[Random.Range(0, unknownResourcePrefabs.Count)];
                rotation = Quaternion.Euler(0f, 120f, 0f);
            }
            else
                prefab = desertPrefab;
        }

        return (prefab, rotation);
    }

    private Quaternion GetMoatRotation(Hex hex, float hexRadius)
    {
        // Weltposition des Hex (Burgzentrum ist 0,0 → pos = (0,0,0))
        Vector3 pos = hex.ToWorld(hexRadius);

        // Falls irgendwas doch die Burg (0,0) wäre: neutral
        if (pos.sqrMagnitude <= 1e-6f)
            return Quaternion.identity;

        // Winkel um Y ermitteln. Wichtig: Atan2(x, z) → Winkel relativ zur Z-Achse
        float angleDeg = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;
        if (angleDeg < 0f) angleDeg += 360f;

        // In 60°-Sektoren runden (0..5)
        int sector = Mathf.FloorToInt((angleDeg + 30f) / 60f) % 6;
        if (sector < 0) sector += 6;

        // Finale Y-Rotation inkl. optionalem Offset fürs Prefab
        float y = sector * 60f + moatRotationOffsetY;
        return Quaternion.Euler(0f, y, 0f);
    }
}