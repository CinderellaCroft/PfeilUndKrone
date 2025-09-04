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
    [SerializeField] private TextMeshProUGUI workerText;

    [Header("Buttons")]
    [SerializeField] private Button doneButton;
    [SerializeField] private Button kingPathButton;
    [SerializeField] private Button kingPathConfirmButton;
    [SerializeField] private Button kingWorkerBuyButton;
    [SerializeField] private Button kingWagonUpgradeButton;
    [SerializeField] private Button kingWagonPathButton;
    [SerializeField] private Button banditAmbushButton;

    [Header("End Game Panels")]
    [SerializeField] private GameObject winnerPanel; // Assign the WinnerPanel in the Inspector
    [SerializeField] private GameObject loserPanel;  // Assign the LoserPanel in the Inspector

    [SerializeField] private LobbyUIController lobbyUIController;

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
        if (roleText == null || turnStatusText == null || infoText == null || resourcesText == null || workerText == null || doneButton == null)
        {
            Debug.LogError("---!!! CRITICAL SETUP ERROR IN UIMANAGER !!!---", this.gameObject);
            Debug.LogError("--> One or more UI element references (Text, Button) are NOT assigned in the Inspector. Please select _UIManager and assign them.", this.gameObject);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        if (roundNumberText == null) Debug.LogWarning("roundNumberText is not assigned in UIManager.", this.gameObject);
        if (kingPathButton == null) Debug.LogWarning("kingPathButton is not assigned in UIManager.", this.gameObject);
        if (kingPathConfirmButton == null) Debug.LogWarning("kingPathConfirmButton is not assigned in UIManager.", this.gameObject);
        if (kingWorkerBuyButton == null) Debug.LogWarning("kingWorkerBuyButton is not assigned in UIManager.", this.gameObject);
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
        if (kingPathConfirmButton != null) kingPathConfirmButton.onClick.AddListener(OnKingPathConfirmButtonClicked);
        if (kingWorkerBuyButton != null) kingWorkerBuyButton.onClick.AddListener(OnKingWorkerBuyButtonClicked);
        if (kingWagonUpgradeButton != null) kingWagonUpgradeButton.onClick.AddListener(OnKingWagonUpgradeButtonClicked);
        if (kingWagonPathButton != null) kingWagonPathButton.onClick.AddListener(OnKingWagonPathButtonClicked);
        if (banditAmbushButton != null) banditAmbushButton.onClick.AddListener(OnBanditAmbushButtonClicked);

        SetDoneButtonActive(false);
        SetKingButtonsActive(false);
        SetBanditButtonsActive(false);
    }

    public void UpdateRoleText(PlayerRole role)
    {
        roleText.text = $"Role: {role}";
        // Update worker text when role changes
        UpdateWorkerText();
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

        // Update InteractionManager resources
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.UpdateResources(gold, wood, grain);
            // Update button states when resources change
            UpdateKingWorkerBuyButtonText();
            UpdateBanditAmbushButtonText();
            // Update worker text when resources change (for King)
            UpdateWorkerText();
        }
    }

    public void UpdateWorkerText()
    {
        if (workerText == null) return;

        if (InteractionManager.Instance != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.MyRole == PlayerRole.King)
            {
                int availableWorkers = InteractionManager.Instance.GetAvailableWorkerCount();
                int ownedWorkers = InteractionManager.Instance.GetPurchasedWorkerCount();
                int availableWagonWorkers = InteractionManager.Instance.GetAvailableWagonWorkerCount();
                int availableRegularWorkers = InteractionManager.Instance.GetAvailableRegularWorkerCount();
                
                int totalRegularWorkers = InteractionManager.Instance.GetTotalOwnedRegularWorkers();
                int totalWagonWorkers = InteractionManager.Instance.GetTotalOwnedWagonWorkers();
                
                workerText.text = $"Workers {availableRegularWorkers}/{totalRegularWorkers}\nWagon Workers {availableWagonWorkers}/{totalWagonWorkers}";
            }
            else
            {
                workerText.text = "";
            }
        }
        else
        {
            workerText.text = "Workers: 0/0";
        }
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
        if (kingPathConfirmButton != null)
        {
            kingPathConfirmButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingPathConfirmButtonText();
            }
        }
        if (kingWorkerBuyButton != null)
        {
            kingWorkerBuyButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWorkerBuyButtonText();
            }
        }
        if (kingWagonUpgradeButton != null)
        {
            kingWagonUpgradeButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWagonUpgradeButtonText();
            }
        }
        if (kingWagonPathButton != null)
        {
            kingWagonPathButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWagonPathButtonText();
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
            // Only enable path button for creating new paths
            kingPathButton.interactable = InteractionManager.Instance.CanCreateNewPath();
        }
    }

    public void UpdateKingPathConfirmButtonText()
    {
        if (kingPathConfirmButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingPathConfirmButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Pfad bestätigen";
            }
            // Only enable confirm button when path is ready to confirm
            kingPathConfirmButton.interactable = InteractionManager.Instance.CanConfirmPath();
        }
    }

    public void UpdateKingWorkerBuyButtonText()
    {
        if (kingWorkerBuyButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWorkerBuyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = InteractionManager.Instance.GetWorkerBuyButtonText();
            }
            kingWorkerBuyButton.interactable = InteractionManager.Instance.CanBuyWorker();
        }
    }

    public void UpdateKingWagonUpgradeButtonText()
    {
        if (kingWagonUpgradeButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWagonUpgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                int availableWorkers = InteractionManager.Instance.GetAvailableRegularWorkerCount();
                buttonText.text = $"Upgrade Worker (50 wood)\nWorkers: {availableWorkers}";
            }
            kingWagonUpgradeButton.interactable = InteractionManager.Instance.CanUpgradeWorkerToWagon();
        }
    }

    public void UpdateKingWagonPathButtonText()
    {
        if (kingWagonPathButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWagonPathButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                int availableWagonWorkers = InteractionManager.Instance.GetAvailableWagonWorkerCount();
                buttonText.text = $"Wagon Path\nAvailable: {availableWagonWorkers}";
            }
            kingWagonPathButton.interactable = InteractionManager.Instance.GetAvailableWagonWorkerCount() > 0;
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

    private void OnKingWorkerBuyButtonClicked()
    {
        Debug.Log("King Worker Buy button clicked!");

        if (InteractionManager.Instance.CanBuyWorker())
        {
            InteractionManager.Instance.BuyWorker();
        }
        else
        {
            string errorMsg = $"Cannot buy worker - {InteractionManager.Instance.GetWorkerBuyButtonText()}";
            Debug.LogError($"Error: {errorMsg}");
            UpdateInfoText(errorMsg);
        }

        UpdateKingWorkerBuyButtonText();
        UpdateKingPathButtonText();
        UpdateKingWagonUpgradeButtonText();
        UpdateWorkerText();
    }

    private void OnKingWagonUpgradeButtonClicked()
    {
        Debug.Log("King Wagon Upgrade button clicked!");

        if (InteractionManager.Instance.CanUpgradeWorkerToWagon())
        {
            InteractionManager.Instance.UpgradeWorkerToWagon();
        }
        else
        {
            string errorMsg = "Cannot upgrade worker to wagon - insufficient wood or no available workers";
            Debug.LogError($"Error: {errorMsg}");
            UpdateInfoText(errorMsg);
        }

        UpdateKingWagonUpgradeButtonText();
        UpdateKingWagonPathButtonText();
        UpdateWorkerText();
    }

    private void OnKingWagonPathButtonClicked()
    {
        Debug.Log("King Wagon Path button clicked!");

        if (InteractionManager.Instance.GetAvailableWagonWorkerCount() > 0)
        {
            InteractionManager.Instance.StartNewWagonWorkerPath();
            UpdateInfoText("Creating wagon worker path - select a resource field");
        }
        else
        {
            string errorMsg = "Cannot create wagon worker path - no wagon workers available";
            Debug.LogError($"Error: {errorMsg}");
            UpdateInfoText(errorMsg);
        }

        UpdateKingWagonPathButtonText();
        UpdateKingPathConfirmButtonText();
    }

    private void OnKingPathButtonClicked()
    {
        Debug.Log("King Path button clicked!");

        if (InteractionManager.Instance.CanCreateNewPath())
        {
            InteractionManager.Instance.StartNewPath();
        }
        else
        {
            Debug.LogError("❌ Error: Cannot create path in current state!");
        }

        // Update button texts after state change
        UpdateKingPathButtonText();
        UpdateKingPathConfirmButtonText();
        UpdateKingWorkerBuyButtonText();
        UpdateWorkerText();
    }

    private void OnKingPathConfirmButtonClicked()
    {
        Debug.Log("King Path Confirm button clicked!");

        if (InteractionManager.Instance.CanConfirmPath())
        {
            InteractionManager.Instance.ConfirmCurrentPath();
        }
        else
        {
            Debug.LogError("❌ Error: Cannot confirm path in current state!");
        }

        // Update button texts after state change
        UpdateKingPathButtonText();
        UpdateKingPathConfirmButtonText();
        UpdateKingWorkerBuyButtonText();
        UpdateWorkerText();
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
            Debug.LogError($"Error: {errorMsg}");
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

        // Update worker text whenever turn/visibility changes
        UpdateWorkerText();
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
        workerText.gameObject.SetActive(false);

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

    public void ShowCreatedLobbyPanel(string lobbyId)
    {
        if (lobbyUIController != null)
        {
            // Defer the work to the specific controller
            lobbyUIController.DisplayLobbyId(lobbyId);
            // You might also want to activate the panel containing the lobby controller here
            // lobbyUIController.gameObject.SetActive(true);
        }
        else
        {
            // Fallback if the controller isn't assigned
            UpdateInfoText($"Lobby Created! ID: {lobbyId}\n(Share with a friend)");
        }
    }
}