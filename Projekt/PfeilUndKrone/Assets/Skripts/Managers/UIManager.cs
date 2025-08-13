using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : Singleton<UIManager>
{
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI turnStatusText;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private TextMeshProUGUI resourcesText;
    [SerializeField] private Button doneButton;

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (roleText == null || turnStatusText == null || infoText == null || resourcesText == null || doneButton == null)
        {
            Debug.LogError("---!!! CRITICAL SETUP ERROR IN UIMANAGER !!!---", this.gameObject);
            Debug.LogError("--> One or more UI element references (Text, Button) are NOT assigned in the Inspector. Please select _UIManager and assign them.", this.gameObject);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    void Start()
    {
        doneButton.onClick.AddListener(OnDoneButtonClicked);
        SetDoneButtonActive(false);
    }

    public void UpdateRoleText(PlayerRole role)
    {
        roleText.text = $"Role: {role}";
    }

    public void UpdateTurnStatus(string status)
    {
        turnStatusText.text = status;
    }

    public void UpdateInfoText(string info)
    {
        infoText.text = info;
    }

    public void UpdateResourcesText(int gold, int wood, int grain)
    {
        resourcesText.text = $"Gold: {gold} | Wood: {wood} | Grain: {grain}";
    }

    public void SetDoneButtonActive(bool isActive)
    {
        doneButton.gameObject.SetActive(isActive);
    }

    private void OnDoneButtonClicked()
    {
        infoText.text = "";
        if (GameManager.Instance.CurrentTurn == GameTurn.KingPlanning)
        {
            //CornerPathManager.Instance.SubmitPathToServer();
        }
        else if (GameManager.Instance.CurrentTurn == GameTurn.BanditPlanning)
        {
            //AmbushManager.Instance.FinalizeAmbushes();
        }
    }
}