using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NetworkingDTOs;

public enum InteractionMode { None, PathSelection, AmbushPlacement }

public class InteractionManager : Singleton<InteractionManager>
{
    public HexGridGenerator gridGen;
    public NetworkServiceBase net;

    public GameObject workerPrefab;
    public GameObject ambushOrb; // Prefab für die Kugeln bei Ambushes
    public float workerSpeed = 1f;

    public Material ambushLineMaterial;
    public int maxAmbushes = 5;

    private InteractionMode currentMode = InteractionMode.None;

    private HashSet<HexVertex> selectedVertices = new();
    private HashSet<HexVertex> validNextVertices = new();
    private List<HexVertex> centralVertices;
    private bool pathComplete;
    private GameObject workerObj;
    private List<Vector3> serverPathWorld = new();
    private int pathStep;
    private bool isMoving;

    private HexVertex ambushStart;
    private List<NetworkingDTOs.AmbushEdge> placedAmbushes = new();
    private List<GameObject> ambushOrbObjects = new(); // Lokale Kugeln nur für Bandit während Platzierung
    private List<GameObject> animationAmbushOrbObjects = new(); // Animation Kugeln für beide Spieler
    private HashSet<HexEdge> ambushEdges = new();

    // Vertex highlighting system
    private HashSet<HexVertex> highlightedVertices = new();
    private Color originalVertexColor = Color.red;
    private Color highlightColor = Color.magenta;

    protected override void Awake()
    {
        base.Awake();
        workerObj = Instantiate(workerPrefab); workerObj.SetActive(false);
        //net.OnPathApproved += ExecuteServerPath;
        //net.OnAmbushConfirmed += ConfirmAmbushPlacement;
    }

    void Start()
    {
        centralVertices = Enum.GetValues(typeof(VertexDirection))
            .Cast<VertexDirection>()
            .Select(d => new HexVertex(new Hex(0, 0), d))
            .ToList();
    }

    public void EnableInteraction(PlayerRole role)
    {
        if (role == PlayerRole.King) currentMode = InteractionMode.PathSelection;
        else if (role == PlayerRole.Bandit) currentMode = InteractionMode.AmbushPlacement;
        else currentMode = InteractionMode.None;

        ResetState();
        workerObj.SetActive(false);
    }
    public void DisableInteraction() { currentMode = InteractionMode.None; }

    void Update()
    {
        if (isMoving) MoveWorker();
        if (currentMode == InteractionMode.AmbushPlacement) DrawAmbushLines();
    }

    public void OnHexClicked(Hex h)
    {
    }

    public void OnEdgeClicked(HexEdge e)
    {
    }

    public void OnVertexClicked(HexVertex v)
    {
        // Handle vertex highlighting (pink toggle)
        ToggleVertexHighlight(v);
        
        if (currentMode == InteractionMode.PathSelection) HandlePathClick(v);
        else if (currentMode == InteractionMode.AmbushPlacement) HandleAmbushClick(v);
    }

    void ToggleVertexHighlight(HexVertex vertex)
    {
        var gridVisuals = GridVisualsManager.Instance;
        var vertexGO = gridVisuals.GetVertexGameObject(vertex);
        
        if (vertexGO == null)
        {
            Debug.LogError($"❌ Error: Could not find GameObject for vertex {vertex}");
            return;
        }

        var renderer = vertexGO.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError($"❌ Error: No Renderer found on vertex GameObject {vertex}");
            return;
        }

        if (highlightedVertices.Contains(vertex))
        {
            // Remove highlight - back to original color
            highlightedVertices.Remove(vertex);
            renderer.material.color = originalVertexColor;
            Debug.Log($"Vertex ({vertex.Hex.Q},{vertex.Hex.R}) Direction: {vertex.Direction} - Highlight removed");
        }
        else
        {
            // Add highlight - change to pink
            highlightedVertices.Add(vertex);
            renderer.material.color = highlightColor;
            Debug.Log($"Vertex ({vertex.Hex.Q},{vertex.Hex.R}) Direction: {vertex.Direction} - Highlighted pink");
        }
    }

    void ResetAllVertexHighlights()
    {
        var gridVisuals = GridVisualsManager.Instance;
        
        foreach (var vertex in highlightedVertices.ToList())
        {
            var vertexGO = gridVisuals.GetVertexGameObject(vertex);
            if (vertexGO != null)
            {
                var renderer = vertexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = originalVertexColor;
                }
            }
        }
        
        highlightedVertices.Clear();
        Debug.Log("🎨 All vertex highlights reset to original color");
    }

    void ResetVertexHighlightsKeepAmbushVertices()
    {
        var gridVisuals = GridVisualsManager.Instance;
        var ambushVertices = new HashSet<HexVertex>();
        
        // Sammle alle Vertices die an Ambushes beteiligt sind
        foreach (var ambush in placedAmbushes)
        {
            ambushVertices.Add(ambush.cornerA);
            ambushVertices.Add(ambush.cornerB);
        }
        
        foreach (var vertex in highlightedVertices.ToList())
        {
            // Wenn dieser Vertex NICHT an einem Ambush beteiligt ist, setze ihn zurück
            if (!ambushVertices.Contains(vertex))
            {
                var vertexGO = gridVisuals.GetVertexGameObject(vertex);
                if (vertexGO != null)
                {
                    var renderer = vertexGO.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = originalVertexColor;
                    }
                }
                highlightedVertices.Remove(vertex);
            }
        }
        
        Debug.Log($"🎨 Vertex highlights reset, kept {ambushVertices.Count} ambush-connected vertices pink");
    }

    void EnsureVertexHighlighted(HexVertex vertex)
    {
        if (highlightedVertices.Contains(vertex))
            return; // Already highlighted
        
        var gridVisuals = GridVisualsManager.Instance;
        var vertexGO = gridVisuals.GetVertexGameObject(vertex);
        
        if (vertexGO != null)
        {
            var renderer = vertexGO.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = highlightColor;
                highlightedVertices.Add(vertex);
            }
        }
    }

    void UpdateVertexHighlightsForAmbushVertices(HexVertex vertex)
    {
        // Prüfe ob dieser Vertex noch an anderen Ambushes beteiligt ist
        bool stillConnectedToAmbush = false;
        foreach (var ambush in placedAmbushes)
        {
            if (ambush.cornerA.Equals(vertex) || ambush.cornerB.Equals(vertex))
            {
                stillConnectedToAmbush = true;
                break;
            }
        }
        
        // Wenn der Vertex nicht mehr an Ambushes beteiligt ist, kann er wieder normal werden
        // (aber nur wenn der Spieler ihn nicht manuell highlighted hat)
        if (!stillConnectedToAmbush && highlightedVertices.Contains(vertex))
        {
            var gridVisuals = GridVisualsManager.Instance;
            var vertexGO = gridVisuals.GetVertexGameObject(vertex);
            
            if (vertexGO != null)
            {
                var renderer = vertexGO.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = originalVertexColor;
                    highlightedVertices.Remove(vertex);
                }
            }
        }
    }

    // Public method to reset highlights (can be called from UI or other systems)
    public void ResetVertexHighlights()
    {
        ResetAllVertexHighlights();
    }

    // Debug method to check highlighted vertices
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugHighlightedVertices()
    {
        Debug.Log($"🎨 Currently highlighted vertices: {highlightedVertices.Count}");
        foreach (var vertex in highlightedVertices)
        {
            Debug.Log($"  - Vertex({vertex.Hex.Q},{vertex.Hex.R}) Direction: {vertex.Direction}");
        }
    }

    void HandlePathClick(HexVertex v)
    {
        if (pathComplete || isMoving) return;

        Debug.Log($"Vertex clicked: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");

        if (!selectedVertices.Any())
        {
            selectedVertices.Add(v);
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v));
            return;
        }
        if (!validNextVertices.Contains(v)) { 
            validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(selectedVertices.Last())); 
            return; 
        }

        selectedVertices.Add(v);
        if (centralVertices.Contains(v)) {
            pathComplete = true;
        }
        validNextVertices = new HashSet<HexVertex>(GetNeighborVertices(v).Except(selectedVertices));
    }

    public void SubmitPath()
    {
        if (currentMode != InteractionMode.PathSelection)
        {
            Debug.LogError("❌ Error: Not in path selection mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in path selection mode!");
            return;
        }
        
        if (!pathComplete) 
        {
            Debug.LogError("❌ Error: Path is not complete! Please reach a center vertex.");
            UIManager.Instance.UpdateInfoText("Error: Path is not complete! Please reach a center vertex.");
            return;
        }
        
        if (selectedVertices.Count == 0)
        {
            Debug.LogError("❌ Error: No path selected!");
            UIManager.Instance.UpdateInfoText("Error: No path selected!");
            return;
        }
        
        var serializableVertices = selectedVertices.Select(v => new SerializableHexVertex(v)).ToArray();
        
        var pathData = new PlaceWorkersPayload 
        { 
            paths = new SerializablePathData[] 
            { 
                new SerializablePathData { path = serializableVertices }
            }
        };
        
        net.Send("place_workers", pathData);
        DisableInteraction();
        
        // Reset vertex highlights when submitting path
        ResetAllVertexHighlights();
    }

    public void ExecuteServerPath(List<HexVertex> path)
    {
        serverPathWorld = path.Select(v => v.ToWorld(gridGen.hexRadius)).ToList();
        if (!serverPathWorld.Any()) return;
        
        // Hebe alle Pfad-Punkte um 1f an (gleiche Höhe wie ambushOrb)
        for (int i = 0; i < serverPathWorld.Count; i++)
        {
            serverPathWorld[i] = new Vector3(serverPathWorld[i].x, serverPathWorld[i].y + 0.35f, serverPathWorld[i].z);
        }
        
        workerObj.transform.position = serverPathWorld[0];
        workerObj.SetActive(true);
        isMoving = true; pathStep = 0;
    }

    void MoveWorker()
    {
        if (pathStep >= serverPathWorld.Count) 
        {
            isMoving = false; 
            workerObj.SetActive(false);
            
            // Clear animation orbs after movement is complete
            ClearAnimationOrbs();
            
            ResetState(); 
            return;
        }
        
        var curr = workerObj.transform.position;
        var targ = serverPathWorld[pathStep];
        
        // Visual debug - draw a line to show worker path
        Debug.DrawLine(curr, targ, Color.red, 0.1f);
        
        workerObj.transform.position = Vector3.MoveTowards(curr, targ, workerSpeed * Time.deltaTime);
        
        // Check for collisions with ambush orbs
        CheckCollisionWithOrbs(curr);
        
        if (Vector3.Distance(curr, targ) < 0.01f) 
        {
            pathStep++;
            if (pathStep >= serverPathWorld.Count) 
            { 
                isMoving = false; 
                workerObj.SetActive(false);
                
                // Clear animation orbs after movement is complete
                ClearAnimationOrbs();
                
                ResetState(); 
            }
        }
    }

    void CheckCollisionWithOrbs(Vector3 workerPosition)
    {
        float collisionDistance = 0.1f; // Increased collision distance for easier detection
        
        // Check collision with animation orbs (visible to both players)
        for (int i = 0; i < animationAmbushOrbObjects.Count; i++)
        {
            var orb = animationAmbushOrbObjects[i];
            if (orb != null)
            {
                float distance = Vector3.Distance(workerPosition, orb.transform.position);
                
                if (distance <= collisionDistance)
                {
                    Debug.Log($"🎯 AMBUSH COLLISION DETECTED! ({GameManager.Instance?.MyRole})");
                    
                    // Visual feedback - change orb color or add particle effect
                    var renderer = orb.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.red; // Change to red to indicate collision
                    }
                    
                    // Optionally, you can destroy the orb or add visual effects here
                    // Destroy(orb);
                }
            }
        }
    }

    IEnumerable<HexVertex> GetNeighborVertices(HexVertex v)
        => v.GetAdjacentHexes()
            .SelectMany(hex => Enum.GetValues(typeof(HexDirection)).Cast<HexDirection>().Select(dir => new HexEdge(hex, dir)))
            .Where(e => e.GetVertexEndpoints().Contains(v))
            .SelectMany(e => e.GetVertexEndpoints())
            .Where(x => !x.Equals(v))
            .Distinct();

    void HandleAmbushClick(HexVertex v)
    {
        Debug.Log($"Vertex clicked for ambush: ({v.Hex.Q},{v.Hex.R}) Direction: {v.Direction}");
        
        if (ambushStart.Equals(default)) 
        { 
            ambushStart = v; 
            return; 
        }

        Debug.Log($"INTERACTIONMANAGER: Checking if vertices are neighbors - Start: {ambushStart}, End: {v}");
        
        // Prüfe ob die Vertices benachbart sind
        var neighbors = GetNeighborVertices(ambushStart);
        if (!neighbors.Contains(v))
        {
            Debug.LogError("❌ Error: Vertices are not neighbors! Resetting ambush start.");
            ambushStart = default;
            return;
        }

        // Erstelle neue Ambush
        var newAmbush = new NetworkingDTOs.AmbushEdge
        {
            cornerA = ambushStart,
            cornerB = v
        };

        // Suche nach bereits existierender Ambush (in beide Richtungen)
        int existingIndex = -1;
        for (int i = 0; i < placedAmbushes.Count; i++)
        {
            var existing = placedAmbushes[i];
            if ((existing.cornerA.Equals(newAmbush.cornerA) && existing.cornerB.Equals(newAmbush.cornerB)) ||
                (existing.cornerA.Equals(newAmbush.cornerB) && existing.cornerB.Equals(newAmbush.cornerA)))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            // Ambush existiert bereits - entfernen (Toggle)
            var removedAmbush = placedAmbushes[existingIndex];
            placedAmbushes.RemoveAt(existingIndex);
            
            // Entferne auch die visuelle Kugel
            if (existingIndex < ambushOrbObjects.Count)
            {
                Destroy(ambushOrbObjects[existingIndex]);
                ambushOrbObjects.RemoveAt(existingIndex);
            }
            
            // Prüfe ob die Vertices noch an anderen Ambushes beteiligt sind
            UpdateVertexHighlightsForAmbushVertices(removedAmbush.cornerA);
            UpdateVertexHighlightsForAmbushVertices(removedAmbush.cornerB);
            
            Debug.Log($"🗑️ Ambush removed between vertices ({ambushStart.Hex.Q},{ambushStart.Hex.R}) and ({v.Hex.Q},{v.Hex.R})");
        }
        else
        {
            // Neue Ambush erstellen
            if (placedAmbushes.Count >= maxAmbushes) 
            {
                Debug.LogError($"❌ Error: Maximum ambushes ({maxAmbushes}) already placed!");
                ambushStart = default;
                return;
            }

            placedAmbushes.Add(newAmbush);
            
            // Stelle sicher, dass beide Vertices pink sind (da sie jetzt an einem Ambush beteiligt sind)
            EnsureVertexHighlighted(ambushStart);
            EnsureVertexHighlighted(v);
            
            // Erstelle visuelle Kugel
            if (ambushOrb == null)
            {
                Debug.LogError("❌ Error: ambushOrb prefab is null!");
                return;
            }
            
            Vector3 midPoint = (ambushStart.ToWorld(gridGen.hexRadius) + v.ToWorld(gridGen.hexRadius)) / 2f;
            // Hebe die Kugel um ihre eigene Höhe an
            midPoint.y += 0.35f;
            GameObject orbObj = Instantiate(ambushOrb, midPoint, Quaternion.identity);
            ambushOrbObjects.Add(orbObj);
            
            Debug.Log($"✅ Ambush created between vertices ({ambushStart.Hex.Q},{ambushStart.Hex.R}) and ({v.Hex.Q},{v.Hex.R})");
        }

        ambushStart = default;
    }

    public void FinalizeAmbushes()
    {
        if (currentMode != InteractionMode.AmbushPlacement)
        {
            Debug.LogError("❌ Error: Not in ambush placement mode!");
            UIManager.Instance.UpdateInfoText("Error: Not in ambush placement mode!");
            return;
        }
        
        if (placedAmbushes.Count == 0)
        {
            Debug.LogError("❌ Error: No ambushes placed!");
            UIManager.Instance.UpdateInfoText("Error: No ambushes placed! Place at least one ambush.");
            return;
        }

        try
        {
            // Erstelle Payload für Server mit korrekter Serialisierung
            var serializableAmbushes = placedAmbushes.Select(a => new SerializableAmbushEdge(a)).ToArray();
            var ambushPayload = new PlaceAmbushesPayload
            {
                ambushes = serializableAmbushes
            };
            
            net.Send("place_ambushes", ambushPayload);
            DisableInteraction();
            
            // Reset ALL vertex highlights when bandit submits ambushes
            ResetAllVertexHighlights();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error: Failed to send ambushes to server: {e.Message}");
        }
    }

    void DrawAmbushLines()
    {
        // Zeichne alle platzierten Ambushes
        foreach (var ambush in placedAmbushes)
        {
            Vector3 posA = ambush.cornerA.ToWorld(gridGen.hexRadius);
            Vector3 posB = ambush.cornerB.ToWorld(gridGen.hexRadius);
            Debug.DrawLine(posA, posB, Color.red);
        }
        
        // Zeichne temporäre Linie zum aktuellen ambushStart
        if (!ambushStart.Equals(default))
        {
            Vector3 startPos = ambushStart.ToWorld(gridGen.hexRadius);
            // Zeichne einen kleinen Kreis um den Start-Punkt
            Debug.DrawLine(startPos + Vector3.right * 0.1f, startPos + Vector3.left * 0.1f, Color.yellow);
            Debug.DrawLine(startPos + Vector3.forward * 0.1f, startPos + Vector3.back * 0.1f, Color.yellow);
        }
    }

    public void DisplayAnimationOrbs(List<NetworkingDTOs.AmbushEdge> banditAmbushes)
    {
        // Debug: Log received ambush data in detail
        int validAmbushes = 0;
        for (int i = 0; i < banditAmbushes.Count; i++)
        {
            var ambush = banditAmbushes[i];
            
            // Enhanced validation for ambush data
            bool isValidAmbush = IsValidAmbushForAnimation(ambush);
            if (!isValidAmbush)
            {
                Debug.LogWarning($"⚠️ [IM] Ambush [{i}] failed validation, skipping");
                continue;
            }
            
            validAmbushes++;
            Debug.Log($"🎯 [IM] Valid ambush [{i}]: cornerA({ambush.cornerA.Hex.Q},{ambush.cornerA.Hex.R},{ambush.cornerA.Direction}) <-> cornerB({ambush.cornerB.Hex.Q},{ambush.cornerB.Hex.R},{ambush.cornerB.Direction})");
            
            Vector3 cornerAWorld = ambush.cornerA.ToWorld(gridGen.hexRadius);
            Vector3 cornerBWorld = ambush.cornerB.ToWorld(gridGen.hexRadius);
            Vector3 midPoint = (cornerAWorld + cornerBWorld) / 2f;
        }
        
        // Clear any existing animation orbs
        ClearAnimationOrbs();
        
        // For Bandit: Hide local placement orbs during animation to prevent duplication
        if (GameManager.Instance?.MyRole == PlayerRole.Bandit)
        {
            foreach (var orb in ambushOrbObjects)
            {
                if (orb != null) 
                {
                    orb.SetActive(false); // Hide during animation
                }
            }
        }
        
        if (ambushOrb == null)
        {
            Debug.LogError("❌ Fehler: ambushOrb Prefab is not assigned in the Inspector!");
            return;
        }
        
        // Create animation orbs for valid ambushes for both players
        foreach (var ambush in banditAmbushes)
        {
            // Validate ambush before creating orb
            if (!IsValidAmbushForAnimation(ambush))
            {
                Debug.LogWarning($"⚠️ [IM] Skipping invalid ambush during orb creation");
                continue;
            }
            
            Vector3 cornerAWorld = ambush.cornerA.ToWorld(gridGen.hexRadius);
            Vector3 cornerBWorld = ambush.cornerB.ToWorld(gridGen.hexRadius);
            Vector3 midPoint = (cornerAWorld + cornerBWorld) / 2f;
            midPoint.y += 0.35f; // Same height as worker
            
            GameObject orbObj = Instantiate(ambushOrb, midPoint, Quaternion.identity);
            animationAmbushOrbObjects.Add(orbObj);
            
            Debug.Log($"✅ Animation orb created at position {midPoint} for ambush {ambush.cornerA} <-> {ambush.cornerB}");
        }
    }
    
    bool IsValidAmbushForAnimation(NetworkingDTOs.AmbushEdge ambush)
    {
        // Check for default/null vertices
        if (ambush.cornerA.Equals(default(HexVertex)) || ambush.cornerB.Equals(default(HexVertex)))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has default/invalid corners, skipping");
            return false;
        }
        
        // Check for identical corners
        if (ambush.cornerA.Equals(ambush.cornerB))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has identical corners ({ambush.cornerA}), skipping");
            return false;
        }
        
        // Check for center hex ambushes (should not be at 0,0)
        bool isCornerACenter = ambush.cornerA.Hex.Q == 0 && ambush.cornerA.Hex.R == 0;
        bool isCornerBCenter = ambush.cornerB.Hex.Q == 0 && ambush.cornerB.Hex.R == 0;
        if (isCornerACenter && isCornerBCenter)
        {
            Debug.LogWarning($"⚠️ [IM] Ambush has both corners at center hex (0,0), skipping");
            return false;
        }
        
        // Additional validation: Check if vertices are actually neighbors
        var neighborsOfA = GetNeighborVertices(ambush.cornerA);
        if (!neighborsOfA.Contains(ambush.cornerB))
        {
            Debug.LogWarning($"⚠️ [IM] Ambush vertices are not neighbors: {ambush.cornerA} <-> {ambush.cornerB}");
            return false;
        }
        
        return true;
    }
    
    public void StartSynchronizedAnimation(List<HexVertex> kingPath, List<NetworkingDTOs.AmbushEdge> banditAmbushes)
    {
        // First, display all ambush orbs for both players
        DisplayAnimationOrbs(banditAmbushes);
        
        // Small delay to ensure orbs are created before worker starts moving
        StartCoroutine(DelayedWorkerExecution(kingPath, 0.1f));
    }
    
    System.Collections.IEnumerator DelayedWorkerExecution(List<HexVertex> path, float delay)
    {
        yield return new WaitForSeconds(delay);
        ExecuteServerPath(path);
    }
    
    public void ClearAnimationOrbs()
    {
        foreach (var orb in animationAmbushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        animationAmbushOrbObjects.Clear();
        
        // Show local placement orbs again (for Bandit)
        if (GameManager.Instance?.MyRole == PlayerRole.Bandit)
        {
            foreach (var orb in ambushOrbObjects)
            {
                if (orb != null) 
                {
                    orb.SetActive(true);
                }
            }
        }
    }

    void ResetState()
    {
        selectedVertices.Clear(); validNextVertices.Clear(); pathComplete = false;
        isMoving = false; workerObj?.SetActive(false);
        ambushStart = default; 
        
        // Only reset local placement orbs if we're not in animation mode
        if (!isMoving)
        {
            // Lösche alle lokalen Ambush-Kugeln (Bandit Platzierung) - but only if not animating
            foreach (var orb in ambushOrbObjects)
            {
                if (orb != null) Destroy(orb);
            }
            ambushOrbObjects.Clear();
            placedAmbushes.Clear();
        }
        
        // Don't clear animation orbs here - they should be cleared by ClearAnimationOrbs when animation completes
        
        ambushEdges.Clear();
    }
    
    public void ForceCompleteReset()
    {
        selectedVertices.Clear(); validNextVertices.Clear(); pathComplete = false;
        isMoving = false; workerObj?.SetActive(false);
        ambushStart = default;
        
        // Force clear all orbs
        foreach (var orb in ambushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        ambushOrbObjects.Clear();
        
        foreach (var orb in animationAmbushOrbObjects)
        {
            if (orb != null) Destroy(orb);
        }
        animationAmbushOrbObjects.Clear();
        
        placedAmbushes.Clear();
        ambushEdges.Clear();
        
        // Reset all vertex highlights when new round starts
        ResetAllVertexHighlights();
    }

    HexDirection GetDirectionBetween(HexVertex a, HexVertex b)
    {
        foreach (var hex in a.GetAdjacentHexes())
            if (b.GetAdjacentHexes().Contains(hex))
                foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
                    if (Vector3.Distance(new HexEdge(hex, dir).ToWorld(gridGen.hexRadius),
                        (a.ToWorld(gridGen.hexRadius) + b.ToWorld(gridGen.hexRadius)) / 2f) < 0.01f)
                        return dir;
        return HexDirection.Right;
    }
}
