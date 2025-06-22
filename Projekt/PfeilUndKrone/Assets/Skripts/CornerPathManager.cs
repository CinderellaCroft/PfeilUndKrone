using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Payload‐Klassen für die Worker‐Pfad‐Daten
[System.Serializable]
public class PathData
{
    public List<CornerCoord> path;
}

[System.Serializable]
public class WorkerPathsPayload
{
    public List<PathData> paths;
}

public class CornerPathManager : MonoBehaviour
{
    public static CornerPathManager Instance;

    [Header("Worker (Kugel) zum Bewegen")]
    public GameObject workerPrefab;

    // Laufende Pfadauswahl
    private List<CornerNode> locallySelectedPath = new();
    private HashSet<CornerNode> validNext = new();

    // Ergebnis vom Server in CornerCoords + Welt-Positionen
    private List<CornerCoord> serverConfirmedCoords = new();
    private List<Vector3> serverConfirmedPath = new();

    private GameObject playerObject;
    private bool selectionComplete = false;
    private bool isMoving = false;
    private int currentStep = 0;
    private float speed = 1f;
    private bool _isSelectionEnabled = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        if (workerPrefab != null)
        {
            playerObject = Instantiate(workerPrefab);
            playerObject.name = "Worker";
            playerObject.SetActive(false);
        }
        else Debug.LogError("[CornerPathManager] Kein workerPrefab zugewiesen!");
    }

    // Aktiviert die Pfadauswahl
    public void EnablePathSelection()
    {
        _isSelectionEnabled = true;
        locallySelectedPath.Clear();
        serverConfirmedCoords.Clear();
        serverConfirmedPath.Clear();
        selectionComplete = false;
        isMoving = false;
        if (playerObject) playerObject.SetActive(false);
    }

    public void DisablePathSelection()
    {
        _isSelectionEnabled = false;
    }

    // Klick auf eine Ecke
    public void OnCornerClicked(CornerNode node)
    {
        if (!_isSelectionEnabled || selectionComplete || isMoving)
            return;

        // 1. Klick = Startpunkt
        if (locallySelectedPath.Count == 0)
        {
            locallySelectedPath.Add(node);
            Debug.Log($"Start-Corner: ({node.hexQ},{node.hexR},{node.cornerIndex})");

            // Nächste valide Ecken = alle Nachbarn dieses Knotens
            validNext.Clear();
            foreach (var neigh in node.neighbors)
                validNext.Add(neigh);

            return;
        }

        // 2. Klick auf ungültige Ecke
        if (!validNext.Contains(node))
        {
            Debug.Log($"Invalid Corner: ({node.hexQ},{node.hexR},{node.cornerIndex}).");

            var last = locallySelectedPath[locallySelectedPath.Count - 1];
            validNext.Clear();
            foreach (var neigh in last.neighbors)
                validNext.Add(neigh);

            return;
        }

        // 3. Gültige Auswahl: Ecke zum Pfad hinzufügen
        locallySelectedPath.Add(node);
        Debug.Log($"Added Corner ({node.hexQ},{node.hexR},{node.cornerIndex})");

        // 4. Prüfen: Ist diese Ecke eine zentrale Ecke von Hex(0,0)?
        if (HexGridGenerator.Instance.centralCorners.Contains(node))
        {
            selectionComplete = true;
            Debug.Log("Pfad bis Zentrum ausgewählt. Jetzt 'Done' drücken.");
        }

        // 5. Neue validNext = alle unbesuchten Nachbarn der aktuellen Ecke
        validNext.Clear();
        foreach (var neigh in node.neighbors)
        {
            if (!locallySelectedPath.Contains(neigh))
                validNext.Add(neigh);
        }
    }


    // Schickt den gewählten Pfad als CornerCoords ans Backend
    public void SubmitPathToServer()
    {
        if (!_isSelectionEnabled || !selectionComplete) return;

        var pd = new PathData
        {
            path = locallySelectedPath
                   .Select(n => n.ToCoord())
                   .ToList()
        };
        var payload = new WorkerPathsPayload { paths = new List<PathData> { pd } };

        NetworkManager.Instance.Send("place_workers", payload);
        DisablePathSelection();
    }

    // Wird vom NetworkManager gerufen nach "path_approved"
    public void ExecuteServerPath(List<CornerCoord> coords)
    {
        serverConfirmedCoords = coords;
        serverConfirmedPath = coords
            .Select(c => CoordToWorld(c))
            .ToList();

        if (serverConfirmedPath.Count > 0 && playerObject != null)
        {
            // Setze Worker ans erste Eck
            Vector3 first = serverConfirmedPath[0];
            playerObject.transform.position = new Vector3(first.x, playerObject.transform.position.y, first.z);
            playerObject.SetActive(true);
            isMoving = true;
            currentStep = 0;
        }
    }

    void Update()
    {
        if (!isMoving || serverConfirmedPath.Count < 2) return;

        Vector3 current = playerObject.transform.position;
        Vector3 target = serverConfirmedPath[currentStep + 1];
        Vector3 desired = new Vector3(target.x, current.y, target.z);

        playerObject.transform.position = Vector3.MoveTowards(
            current, desired, speed * Time.deltaTime
        );


        float distXZ = Vector2.Distance(
            new Vector2(current.x, current.z),
            new Vector2(target.x, target.z)
        );
        if (distXZ < 0.01f)
        {
            currentStep++;
            if (currentStep >= serverConfirmedPath.Count - 1)
            {
                isMoving = false;
                playerObject.SetActive(false);
                locallySelectedPath.Clear();
                serverConfirmedCoords.Clear();
                serverConfirmedPath.Clear();
                selectionComplete = false;
            }
        }
    }

    // Hilfsfunktion: CornerCoord → Weltposition
    private Vector3 CoordToWorld(CornerCoord c)
    {
        float radius = HexGridGenerator.Instance.hexRadius;
        Vector3 center = HexGridGenerator.Instance.HexToWorld(c.q, c.r, radius);
        Vector3[] corners = HexGridGenerator.Instance.GetHexCorners(center, radius);
        return corners[c.i];
    }
}
