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
    public Button doneButton, kingPathButton, banditAmbushButton;
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
        if (UIManager.Instance != null)
            UIManager.Instance.Bind(this);
        if (GridVisualsManager.Instance != null)
            GridVisualsManager.Instance.Bind(this);
    }
}