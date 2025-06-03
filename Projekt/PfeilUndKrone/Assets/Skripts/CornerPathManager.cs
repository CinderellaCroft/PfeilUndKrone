using System.Collections.Generic;
using UnityEngine;

public class CornerPathManager : MonoBehaviour
{
    public static CornerPathManager Instance;

    [Header("Worker move")]
    public GameObject workerPrefab;
    private GameObject playerObject;

    private List<CornerNode> path = new();

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
        }
        else
        {
            Debug.LogError("[CornerPathManager] Kein workerPrefab zugewiesen!");
        }
    }

    public void OnCornerClicked(CornerNode node)
    {
        if (selectionComplete) return;
        if (isMoving) return;

        if (path.Count == 0)
        {
            path.Add(node);
            Debug.Log($"Start-Ecke gewählt: {node.position}");

            validNext.Clear();
            foreach (var neigh in node.neighbors)
                validNext.Add(neigh);

            return;
        }

        if (!validNext.Contains(node))
        {
            Debug.Log($"Ecke {node.position} ist nicht gültige Nachbar-Ecke");
            return;
        }

        path.Add(node);
        Debug.Log($"Ecke zur Pfadliste hinzugefügt {node.position}");

        if (HexGridGenerator.Instance.centralCorners.Contains(node))
        {
            selectionComplete = true;
            Debug.Log("Letzte Ecke erreicht (Zentral-Hex).  Kugel startet.");

            if (playerObject != null)
            {
                Vector3 firstCorner = path[0].position;
                float y = playerObject.transform.position.y;
                playerObject.transform.position = new Vector3(firstCorner.x, y, firstCorner.z);
            }

            isMoving = true;
            currentStep = 0;
            return;
        }

        validNext.Clear();
        foreach (var neigh in node.neighbors)
            if (!path.Contains(neigh))
                validNext.Add(neigh);
    }

    void Update()
    {
        if (!isMoving || path.Count < 2 || playerObject == null) return;

        Vector3 nextCorner = path[currentStep + 1].position;

        Vector3 current = playerObject.transform.position;

        Vector3 desired = new Vector3(nextCorner.x, current.y, nextCorner.z);

        playerObject.transform.position = Vector3.MoveTowards(
            current,
            desired,
            speed * Time.deltaTime
        );

        float distXZ = Vector2.Distance(
            new Vector2(current.x, current.z),
            new Vector2(nextCorner.x, nextCorner.z)
        );
        if (distXZ < 0.01f)
        {
            currentStep++;
            if (currentStep >= path.Count - 1)
            {
                isMoving = false;
                Debug.Log("Pfad vollständig.");
            }
        }
    }

}
