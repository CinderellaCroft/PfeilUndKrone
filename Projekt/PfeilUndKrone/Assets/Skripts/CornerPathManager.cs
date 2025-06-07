using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PathData
{
    public List<Vector3> path;
}

public class CornerPathManager : MonoBehaviour
{
    public static CornerPathManager Instance;

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

    void Awake()
    {
        Instance = this;
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

    public void OnCornerClicked(CornerNode node)
    {
        if (selectionComplete || isMoving) return;
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
            SubmitPathToServer();
            return;
        }
        validNext.Clear();
        foreach (var neigh in node.neighbors)
            if (!locallySelectedPath.Contains(neigh))
                validNext.Add(neigh);
    }  
    private void SubmitPathToServer()
    {
        PathData dataToSend = new PathData();
        dataToSend.path = new List<Vector3>();
        foreach (var node in locallySelectedPath)
        {
            dataToSend.path.Add(node.position);
        }

        // Use the new structured `Send` method.
        NetworkManager.Instance.Send("submit_path", dataToSend);
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