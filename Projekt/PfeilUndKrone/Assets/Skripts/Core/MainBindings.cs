using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainBindings : MonoBehaviour
{
    // UIManager refs
    [Header("UIManager")]
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI turnStatusText;
    public TextMeshProUGUI roundNumberText;
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI resourcesText;
    public TextMeshProUGUI workerText;
    
    [Header("Buttons")]
    public Button doneButton;
    public Button kingPathButton;
    public Button kingPathConfirmButton;
    public Button kingWorkerBuyButton;
    public Button kingWagonUpgradeButton;
    public Button kingWagonPathButton;
    public Button banditAmbushButton;
    
    [Header("Panels")]
    public GameObject winnerPanel, loserPanel;

    //GridVisualManager references
    // GridVisualsManager refs
    [Header("GridVisualsManager")]
    public Transform hexFieldContainer;

    [Header("King Prefabs")]
    public GameObject KingCastlePrefab;
    public GameObject KingMoatPrefab;
    public List<ResourcePrefabEntry> resourcePrefabs;

    [Header("Bandit Prefabs")]
    public GameObject BanditCastlePrefab;
    public GameObject BanditMoatPrefab;
    public List<GameObject> unknownResourcePrefabs;

    [Header("Other Prefabs")]
    public GameObject desertPrefab;
    public GameObject vertexMarkerPrefab;
    public GameObject edgeMarkerPrefab;


    void Awake()
    {
        Debug.Log("[MainBindings] Awake() - Binding managers");
        Debug.Log($"[MainBindings] hexFieldContainer assigned: {(hexFieldContainer != null ? hexFieldContainer.name : "NULL")}");
        Debug.Log($"[MainBindings] KingCastlePrefab assigned: {(KingCastlePrefab != null ? KingCastlePrefab.name : "NULL")}");
        Debug.Log($"[MainBindings] desertPrefab assigned: {(desertPrefab != null ? desertPrefab.name : "NULL")}");
        
        // Check UI elements
        Debug.Log($"[MainBindings] roleText assigned: {(roleText != null ? "YES" : "NULL")}");
        Debug.Log($"[MainBindings] turnStatusText assigned: {(turnStatusText != null ? "YES" : "NULL")}");
        Debug.Log($"[MainBindings] doneButton assigned: {(doneButton != null ? "YES" : "NULL")}");
        Debug.Log($"[MainBindings] kingPathButton assigned: {(kingPathButton != null ? "YES" : "NULL")}");
        Debug.Log($"[MainBindings] banditAmbushButton assigned: {(banditAmbushButton != null ? "YES" : "NULL")}");
        
        // Bind immediately when available
        if (UIManager.Instance != null)
        {
            UIManager.Instance.Bind(this);
            Debug.Log("[MainBindings] UIManager bound");
        }
        
        if (GridVisualsManager.Instance != null)
        {
            GridVisualsManager.Instance.Bind(this);
            Debug.Log("[MainBindings] GridVisualsManager bound");
        }
    }
    
    void Start()
    {
        Debug.Log("[MainBindings] Start() - Double-checking assignments");
        Debug.Log($"[MainBindings] hexFieldContainer in Start: {(hexFieldContainer != null ? hexFieldContainer.name : "NULL")}");
        
        // Double-check bindings in Start() in case managers weren't ready in Awake()
        if (UIManager.Instance != null)
        {
            UIManager.Instance.Bind(this);
            Debug.Log("[MainBindings] UIManager re-bound in Start()");
        }
        
        if (GridVisualsManager.Instance != null)
        {
            GridVisualsManager.Instance.Bind(this);
            Debug.Log("[MainBindings] GridVisualsManager re-bound in Start()");
        }
    }
}