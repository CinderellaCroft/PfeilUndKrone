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
    protected override bool Persistent => false; // Don't persist across scenes
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
    [FormerlySerializedAs("RebelDeskPrefab")] public GameObject BanditDeskPrefab;

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



    // Initialize singleton and build resource prefab mapping
    protected override void Awake()
    {
        base.Awake();

        resourceMap = resourcePrefabs
            .ToDictionary(e => e.type, e => e.prefab);
    }

    // Bind prefab references from MainBindings for multi-scene support
    public void Bind(MainBindings b)
    {
        Debug.Log("[GridVisualsManager] Binding references from MainBindings");
        Debug.Log($"[GridVisualsManager] MainBindings.hexFieldContainer is: {(b.hexFieldContainer != null ? b.hexFieldContainer.name : "NULL")}");
        hexFieldContainer = b.hexFieldContainer;

        // If these were unassigned because the singleton was created in Title,
        // take them from the scene bindings:
        if (KingCastlePrefab == null) KingCastlePrefab = b.KingCastlePrefab;
        if (KingMoatPrefab == null) KingMoatPrefab = b.KingMoatPrefab;
        if (resourcePrefabs == null || resourcePrefabs.Count == 0)
        {
            resourcePrefabs = b.resourcePrefabs;
            // Rebuild resource map when prefabs are updated
            if (resourcePrefabs != null && resourcePrefabs.Count > 0)
            {
                resourceMap = resourcePrefabs.ToDictionary(e => e.type, e => e.prefab);
                Debug.Log($"[GridVisualsManager] Rebuilt resourceMap with {resourceMap.Count} entries");
            }
        }

        if (BanditCastlePrefab == null) BanditCastlePrefab = b.BanditCastlePrefab;
        if (BanditMoatPrefab == null) BanditMoatPrefab = b.BanditMoatPrefab;
        if (unknownResourcePrefabs == null || unknownResourcePrefabs.Count == 0)
            unknownResourcePrefabs = b.unknownResourcePrefabs;

        if (desertPrefab == null) desertPrefab = b.desertPrefab;
        if (vertexMarkerPrefab == null) vertexMarkerPrefab = b.vertexMarkerPrefab;
        if (edgeMarkerPrefab == null) edgeMarkerPrefab = b.edgeMarkerPrefab;

        Debug.Log("[GridVisualsManager] Binding complete - all references should now be available");
    }

    // Generate and place all visual elements for the hex grid based on resource map
    public void InitializeVisuals(Dictionary<Hex, FieldType> map)
    {
        ClearPrevious();

        if (gridGenerator == null) { Debug.LogError("GridVisualsManager: gridGenerator NULL"); return; }

        // Always try to rebind from MainBindings for multi-game session support
        var mainBindings = FindFirstObjectByType<MainBindings>();
        if (mainBindings != null)
        {
            Debug.Log("GridVisualsManager: Found MainBindings - rebinding references for new game session");
            Bind(mainBindings);
        }

        if (hexFieldContainer == null)
        {
            Debug.LogError("GridVisualsManager: hexFieldContainer still NULL after binding attempt - attempting fallback search");

            // Fallback: try to find hexFieldContainer by common names
            var candidates = new string[] { "HexFieldContainer", "HexContainer", "GridContainer", "FieldContainer" };
            foreach (var name in candidates)
            {
                var found = GameObject.Find(name);
                if (found != null)
                {
                    hexFieldContainer = found.transform;
                    Debug.Log($"GridVisualsManager: Found hexFieldContainer by name search: {name}");
                    break;
                }
            }

            // Last resort: create a new container
            if (hexFieldContainer == null)
            {
                Debug.LogWarning("GridVisualsManager: Creating new HexFieldContainer as fallback");
                var go = new GameObject("HexFieldContainer");
                hexFieldContainer = go.transform;
            }
        }
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
            Debug.Log($"[GridVisualsManager] InitializeVisuals - My role is: {role}");

            FieldType ft = default;
            bool hasField = map != null && map.TryGetValue(hex, out ft);

            var (prefab, rot) = ResolveFieldVisual(hex, ft, hasField, role, radius);

            var go = Instantiate(prefab, hex.ToWorld(radius), rot, hexFieldContainer);
            go.name = hex.ToString();

            var hm = go.AddComponent<HexMarker>(); hm.hex = hex; hm.interaction = interactionManager;
            hexObjects[hex] = go;
        }

        // Create vertex objects & set visibility based on role
        foreach (var vertex in gridGenerator.Model.AllVertices)
        {
            var pos = vertex.ToWorld(radius);
            pos.y += 0.4f;
            var go = Instantiate(vertexMarkerPrefab, pos, Quaternion.identity, hexFieldContainer);
            go.name = vertex.ToString();
            var vm = go.AddComponent<VertexMarker>(); vm.vertex = vertex; vm.interaction = interactionManager;
            hexVertexObjects[vertex] = go;

            // Initially hide all vertices - they will be shown based on game state
            go.SetActive(false);
        }

        // Add bandit desk for bandit role
        if (GameManager.Instance.MyRole == PlayerRole.Bandit)
        {
            Instantiate(BanditDeskPrefab, new Vector3(0f, -15.25f, 3), Quaternion.identity, hexFieldContainer);
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

    // Get the GameObject associated with a specific vertex
    public GameObject GetVertexGameObject(HexVertex vertex)
    {
        return hexVertexObjects.TryGetValue(vertex, out var go) ? go : null;
    }

    // Set visibility of a single vertex marker
    public void SetVertexVisible(HexVertex vertex, bool visible)
    {
        if (hexVertexObjects.TryGetValue(vertex, out var go))
        {
            go.SetActive(visible);
        }
    }

    // Set visibility for multiple vertex markers
    public void SetVerticesVisible(IEnumerable<HexVertex> vertices, bool visible)
    {
        foreach (var vertex in vertices)
        {
            SetVertexVisible(vertex, visible);
        }
    }

    // Hide all vertex markers in the grid
    public void HideAllVertices()
    {
        foreach (var go in hexVertexObjects.Values)
        {
            go.SetActive(false);
        }
    }

    // Show vertex markers around a selected resource field for King players
    public void ShowVerticesForSelectedResourceField(Hex resourceHex)
    {
        if (GameManager.Instance.MyRole != PlayerRole.King) return;

        // Show vertices around the selected resource field
        var hexRadius = gridGenerator.hexRadius;
        var vertices = GetVerticesAroundHex(resourceHex);
        SetVerticesVisible(vertices, true);
    }

    // Show vertex markers for a specific path
    public void ShowVerticesForPath(IEnumerable<HexVertex> pathVertices)
    {
        SetVerticesVisible(pathVertices, true);
    }

    // Get all vertices that surround a given hex
    private IEnumerable<HexVertex> GetVerticesAroundHex(Hex hex)
    {
        // Get all vertices that belong to this hex
        var vertices = new List<HexVertex>();
        for (int i = 0; i < 6; i++)
        {
            var direction = (VertexDirection)i;
            vertices.Add(new HexVertex(hex, direction));
        }
        return vertices;
    }

    // Destroy all existing visual elements and clear tracking dictionaries
    void ClearPrevious()
    {
        foreach (var go in hexObjects.Values) Destroy(go);
        hexObjects.Clear();
        foreach (var vm in FindObjectsByType<VertexMarker>(FindObjectsSortMode.None)) Destroy(vm.gameObject);
        foreach (var em in FindObjectsByType<EdgeMarker>(FindObjectsSortMode.None)) Destroy(em.gameObject);
        hexVertexObjects.Clear();
        hexEdgeObjects.Clear();
    }

    // Complete reset for a new game session
    public void ResetForNewGame()
    {
        Debug.Log("[GridVisualsManager] ResetForNewGame() - Clearing all visual elements");
        ClearPrevious();

        // Clear references that might be stale
        hexFieldContainer = null;

        Debug.Log("[GridVisualsManager] ResetForNewGame() - Reset complete");
    }

    // Check if field type is a castle
    private static bool IsCastleField(FieldType f) => f == FieldType.Castle;

    // Check if field type is a moat
    private static bool IsMoatField(FieldType f) => f == FieldType.Moat;

    // Try to convert FieldType to ResourceType, excluding castle and moat
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

    // Determine the appropriate prefab and rotation for a hex field based on type and player role
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
                Debug.Log($"[GridVisualsManager] Castle field - Role: {role}, Using prefab: {prefab?.name ?? "NULL"}");
            }
            else if (IsMoatField(ft))
            {
                prefab = role == PlayerRole.King ? KingMoatPrefab : BanditMoatPrefab;
                rotation = GetMoatRotation(hex, hexRadius);
                Debug.Log($"[GridVisualsManager] Moat field - Role: {role}, Using prefab: {prefab?.name ?? "NULL"}");
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

    // Calculate proper rotation for moat pieces based on their position relative to castle
    private Quaternion GetMoatRotation(Hex hex, float hexRadius)
    {
        Vector3 pos = hex.ToWorld(hexRadius);

        if (pos.sqrMagnitude <= 1e-6f)
            return Quaternion.identity;

        float angleDeg = Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;
        if (angleDeg < 0f) angleDeg += 360f;

        int sector = Mathf.FloorToInt((angleDeg + 30f) / 60f) % 6;
        if (sector < 0) sector += 6;

        float y = sector * 60f + moatRotationOffsetY;
        return Quaternion.Euler(0f, y, 0f);
    }

    // Set visibility of a specific edge marker
    public void SetEdgeVisible(HexEdge edge, bool visible)
    {
        if (hexEdgeObjects.TryGetValue(edge, out var go)) go.SetActive(visible);
        else Debug.LogWarning($"[GVM] Edge GameObject not found for {edge}");
    }

    // Hide all edge markers in the grid
    public void HideAllEdges()
    {
        foreach (var go in hexEdgeObjects.Values) go.SetActive(false);
    }
}