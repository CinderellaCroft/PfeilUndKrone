using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PathData
{
    public List<Vector3> path;
}

[System.Serializable]
public class WorkerPathsPayload
{
    public List<PathData> paths;
}

public class CornerPathManager : MonoBehaviour
{
    private static CornerPathManager _instance;
    public static CornerPathManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindFirstObjectByType<CornerPathManager>();
            return _instance;
        }
    }

    [Header("Worker move")]
    public GameObject workerPrefab;
    private GameObject playerObject;
    private List<CornerNode> locallySelectedPath = new();
    private List<Vector3> serverConfirmedPath = new();
    private HashSet<CornerNode> validNext = new();
    private bool selectionComplete = false;
    private bool isMoving = false;
    private int currentStep = 0;
    private float speed = 1f;
    private bool _isSelectionEnabled = false;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (workerPrefab != null)
        {
            playerObject = Instantiate(workerPrefab);
            playerObject.name = "Worker";
            playerObject.SetActive(false);
        }
        else
        {
            Debug.LogError("[CornerPathManager] Kein workerPrefab zugewiesen!");
        }
    }

    public void EnablePathSelection()
    {
        _isSelectionEnabled = true;
        locallySelectedPath.Clear();
        serverConfirmedPath.Clear();
        selectionComplete = false;
        isMoving = false;
        if (playerObject) playerObject.SetActive(false);
        Debug.Log("Path selection enabled.");
    }

    public void DisablePathSelection()
    {
        _isSelectionEnabled = false;
    }

    public void OnCornerClicked(CornerNode node)
    {
        if (!_isSelectionEnabled || selectionComplete || isMoving) return;
        if (locallySelectedPath.Count == 0)
        {
            locallySelectedPath.Add(node);
            Debug.Log($"Start-Ecke gewählt: {node.position}");
            validNext.Clear();
            foreach (var neigh in node.neighbors) validNext.Add(neigh);
            return;
        }
        if (!validNext.Contains(node))
        {
            Debug.Log($"Ecke {node.position} ist nicht gültige Nachbar-Ecke");
            return;
        }

        locallySelectedPath.Add(node);
        Debug.Log($"Ecke zur Pfadliste hinzugefügt {node.position}");
        if (HexGridGenerator.Instance.centralCorners.Contains(node))
        {
            selectionComplete = true;
            // The Done button now handles submission. This part is less critical.
            // You could have it auto-submit or wait for the button.
            // For now, let's just log it.
            Debug.Log("Path reached center. Click 'Done' to submit.");
        }
        validNext.Clear();
        foreach (var neigh in node.neighbors)
            if (!locallySelectedPath.Contains(neigh))
                validNext.Add(neigh);
    }

    public void SubmitPathToServer()
    {
        if (!_isSelectionEnabled) return;

        PathData singlePath = new PathData
        {
            path = new List<Vector3>()
        };
        foreach (var node in locallySelectedPath)
        {
            singlePath.path.Add(node.position);
        }

        var pathsList = new List<PathData> { singlePath };

        WorkerPathsPayload payload = new WorkerPathsPayload { paths = pathsList };
        
        NetworkManager.Instance.Send("place_workers", payload);
        DisablePathSelection();
        UIManager.Instance.SetDoneButtonActive(false);
    }

    public void ExecuteServerPath(List<Vector3> serverPath)
    {
        serverConfirmedPath = serverPath;
        if (playerObject != null && serverConfirmedPath.Count > 0)
        {
            Vector3 firstPos = serverConfirmedPath[0];
            playerObject.transform.position = new Vector3(firstPos.x, playerObject.transform.position.y, firstPos.z);
            playerObject.SetActive(true);
            isMoving = true;
            currentStep = 0;
        }
    }

    void Update()
    {
        if (!isMoving || serverConfirmedPath.Count < 2) return;
        Vector3 nextCorner = serverConfirmedPath[currentStep + 1];
        Vector3 current = playerObject.transform.position;
        Vector3 desired = new Vector3(nextCorner.x, current.y, nextCorner.z);
        playerObject.transform.position = Vector3.MoveTowards(current, desired, speed * Time.deltaTime);
        if (Vector2.Distance(new Vector2(current.x, current.z), new Vector2(nextCorner.x, nextCorner.z)) < 0.01f)
        {
            currentStep++;
            if (currentStep >= serverConfirmedPath.Count - 1)
            {
                isMoving = false;
                Debug.Log("Pfad vollständig.");
                playerObject.SetActive(false);
                locallySelectedPath.Clear();
                serverConfirmedPath.Clear();
                selectionComplete = false;
            }
        }
    }
}