using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using UnityEngine.SceneManagement;

public class UIManager : Singleton<UIManager>
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI turnStatusText;
    [SerializeField] private TextMeshProUGUI roundNumberText;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private TextMeshProUGUI resourcesText;

    [Header("Buttons")]
    [SerializeField] private Button doneButton;
    [SerializeField] private Button kingPathButton;
    [SerializeField] private Button banditAmbushButton;

    [Header("End Game Panels")]
    [SerializeField] private GameObject winnerPanel; // Assign the WinnerPanel in the Inspector
    [SerializeField] private GameObject loserPanel;  // Assign the LoserPanel in the Inspector

    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();

        // Ensure end-game panels are hidden at the start
        if (winnerPanel != null) winnerPanel.SetActive(false);
        if (loserPanel != null) loserPanel.SetActive(false);
    }

    //MainBindings.cs MonoBehaviour mit containing fresh references
    //called within MainBindings.cs
    public void Bind(MainBindings b)
    {
        roleText = b.roleText; turnStatusText = b.turnStatusText; roundNumberText = b.roundNumberText;
        infoText = b.infoText; resourcesText = b.resourcesText;
        doneButton = b.doneButton; kingPathButton = b.kingPathButton; banditAmbushButton = b.banditAmbushButton;
        winnerPanel = b.winnerPanel; loserPanel = b.loserPanel;
    }



    void CacheRefs()
    {
        roleText = GameObject.FindWithTag("RoleText")?.GetComponent<TextMeshProUGUI>();
        turnStatusText = GameObject.FindWithTag("TurnStatusText")?.GetComponent<TextMeshProUGUI>();
        roundNumberText = GameObject.FindWithTag("RoundText")?.GetComponent<TextMeshProUGUI>();
        infoText = GameObject.FindWithTag("InfoText")?.GetComponent<TextMeshProUGUI>();
        resourcesText = GameObject.FindWithTag("ResourcesText")?.GetComponent<TextMeshProUGUI>();

        doneButton = GameObject.FindWithTag("DoneButton")?.GetComponent<Button>();
        kingPathButton = GameObject.FindWithTag("WorkerButton")?.GetComponent<Button>();
        banditAmbushButton = GameObject.FindWithTag("AmbushButton")?.GetComponent<Button>();

        winnerPanel = GameObject.FindWithTag("WinnerPanel");
        loserPanel = GameObject.FindWithTag("LoserPanel");
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

        if (roundNumberText == null) Debug.LogWarning("roundNumberText is not assigned in UIManager.", this.gameObject);
        if (kingPathButton == null) Debug.LogWarning("kingPathButton is not assigned in UIManager.", this.gameObject);
        if (banditAmbushButton == null) Debug.LogWarning("banditAmbushButton is not assigned in UIManager.", this.gameObject);

        // NEW: Validate end game panels
        if (winnerPanel == null || loserPanel == null)
        {
            Debug.LogError("---!!! CRITICAL SETUP ERROR IN UIMANAGER !!!---", this.gameObject);
            Debug.LogError("--> The WinnerPanel or LoserPanel is NOT assigned in the Inspector.", this.gameObject);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    void Start()
    {
        doneButton.onClick.AddListener(OnDoneButtonClicked);
        if (kingPathButton != null) kingPathButton.onClick.AddListener(OnKingPathButtonClicked);
        if (banditAmbushButton != null) banditAmbushButton.onClick.AddListener(OnBanditAmbushButtonClicked);

        SetDoneButtonActive(false);
        SetKingButtonsActive(false);
        SetBanditButtonsActive(false);
    }

    public void UpdateRoleText(PlayerRole role)
    {
        roleText.text = $"Role: {role}";
    }

    public void UpdateTurnStatus(string status)
    {
        turnStatusText.text = status;
    }

    public void UpdateRoundNumber(int roundNumber)
    {
        if (roundNumberText != null)
        {
            roundNumberText.text = $"Round: {roundNumber}";
        }
        else
        {
            Debug.LogWarning("roundNumberText is not assigned in UIManager!");
        }
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

    public void SetKingButtonsActive(bool isActive)
    {
        if (kingPathButton != null)
        {
            kingPathButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingPathButtonText();
            }
        }
    }

    public void SetBanditButtonsActive(bool isActive)
    {
        if (banditAmbushButton != null)
        {
            banditAmbushButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateBanditAmbushButtonText();
            }
        }
    }

    public void UpdateKingPathButtonText()
    {
        if (kingPathButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingPathButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = InteractionManager.Instance.GetPathCreationButtonText();
            }
        }
    }

    public void UpdateBanditAmbushButtonText()
    {
        if (banditAmbushButton != null && InteractionManager.Instance != null)
        {
            var buttonText = banditAmbushButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = InteractionManager.Instance.GetAmbushBuyButtonText();
            }

            // Disable button if can't buy ambush
            banditAmbushButton.interactable = InteractionManager.Instance.CanBuyAmbush();
        }
    }

    // === BUTTON CLICK HANDLERS ===

    private void OnKingPathButtonClicked()
    {
        Debug.Log("King Path button clicked!");

        if (InteractionManager.Instance.CanCreateNewPath())
        {
            InteractionManager.Instance.StartNewPath();
        }
        else if (InteractionManager.Instance.CanConfirmPath())
        {
            InteractionManager.Instance.ConfirmCurrentPath();
        }
        else
        {
            Debug.LogError("❌ Error: Cannot create or confirm path in current state!");
        }

        // Update button text after state change
        UpdateKingPathButtonText();
    }

    private void OnBanditAmbushButtonClicked()
    {
        Debug.Log("Bandit Ambush button clicked!");

        if (InteractionManager.Instance.CanBuyAmbush())
        {
            InteractionManager.Instance.BuyAmbush();
        }
        else
        {
            string errorMsg = $"Cannot buy ambush - need {InteractionManager.Instance.GetAmbushBuyButtonText()}";
            Debug.LogError($"❌ Error: {errorMsg}");
            UpdateInfoText(errorMsg);
        }

        // Update button text after attempt
        UpdateBanditAmbushButtonText();
    }

    private void OnDoneButtonClicked()
    {
        Debug.Log($"Done button clicked! Current turn: {GameManager.Instance.CurrentTurn}");
        infoText.text = ""; // Clear any previous info/error messages

        // Check which turn it is to send the correct message
        if (GameManager.Instance.CurrentTurn == GameTurn.KingPlanning)
        {
            bool success = InteractionManager.Instance.SubmitPath();
            if (success)
            {
                // Hide King buttons only after successful submission
                SetKingButtonsActive(false);
                SetDoneButtonActive(false);
            }
            // If submission failed, keep buttons visible so player can try again
        }
        else if (GameManager.Instance.CurrentTurn == GameTurn.BanditPlanning)
        {
            bool success = InteractionManager.Instance.FinalizeAmbushes();
            if (success)
            {
                // Hide Bandit buttons only after successful submission
                SetBanditButtonsActive(false);
                SetDoneButtonActive(false);
            }
            // If finalization failed, keep buttons visible so player can try again
        }
        else
        {
            Debug.LogError($"❌ Error: Done button clicked in invalid turn state: {GameManager.Instance.CurrentTurn}");
        }
    }

    // === TURN-BASED BUTTON VISIBILITY ===

    public void UpdateButtonVisibilityForTurn(GameTurn currentTurn, PlayerRole myRole)
    {
        switch (currentTurn)
        {
            case GameTurn.KingPlanning:
                if (myRole == PlayerRole.King)
                {
                    SetDoneButtonActive(true);
                    SetKingButtonsActive(true);
                    SetBanditButtonsActive(false);
                }
                else
                {
                    SetDoneButtonActive(false);
                    SetKingButtonsActive(false);
                    SetBanditButtonsActive(false);
                }
                break;

            case GameTurn.BanditPlanning:
                if (myRole == PlayerRole.Bandit)
                {
                    SetDoneButtonActive(true);
                    SetKingButtonsActive(false);
                    SetBanditButtonsActive(true);
                }
                else
                {
                    SetDoneButtonActive(false);
                    SetKingButtonsActive(false);
                    SetBanditButtonsActive(false);
                }
                break;

            case GameTurn.Executing:
            case GameTurn.Setup:
            default:
                SetDoneButtonActive(false);
                SetKingButtonsActive(false);
                SetBanditButtonsActive(false);
                break;
        }
    }

    public void ShowEndGamePanel(bool didIWin)
    {
        // Hide all the main game UI
        SetDoneButtonActive(false);
        SetKingButtonsActive(false);
        SetBanditButtonsActive(false);

        roleText.gameObject.SetActive(false);
        turnStatusText.gameObject.SetActive(false);
        roundNumberText.gameObject.SetActive(false);
        infoText.gameObject.SetActive(false);
        resourcesText.gameObject.SetActive(false);

        // Show the appropriate panel
        if (didIWin)
        {
            winnerPanel.SetActive(true);
            loserPanel.SetActive(false);
            Debug.Log("👑 GAME OVER - YOU WIN! 👑");
        }
        else
        {
            winnerPanel.SetActive(false);
            loserPanel.SetActive(true);
            Debug.Log("💀 GAME OVER - YOU LOSE! 💀");
        }
    }
}