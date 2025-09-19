using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : Singleton<UIManager>
{
    protected override bool Persistent => false; // Don't persist across scenes
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
    [SerializeField] private Button quitGameButton;



    [Header("Floating Text")]
    public GameObject floatingTextPrefab; // Assign a prefab with a TextMeshProUGUI and FloatingText component
    public Transform goldTextContainer; // Assign the parent transform for the gold floating text
    public Transform woodTextContainer; // Assign the parent transform for the wood floating text
    public Transform grainTextContainer; // Assign the parent transform for the grain floating text

    private int previousGold, previousWood, previousGrain;

    // Initialize singleton and set up default resource tracking values
    protected override void Awake()
    {
        base.Awake();
        ValidateReferences();

        previousGold = 0;
        previousWood = 0;
        previousGrain = 0;
    }
    // Bind UI element references from MainBindings for multi-scene support
    public void Bind(MainBindings b)
    {
        Debug.Log("[UIManager] Binding references from MainBindings");

        roleText = b.roleText;
        turnStatusText = b.turnStatusText;
        roundNumberText = b.roundNumberText;
        infoText = b.infoText;
        resourcesText = b.resourcesText;
        workerText = b.workerText;

        doneButton = b.doneButton;
        kingPathButton = b.kingPathButton;
        kingPathConfirmButton = b.kingPathConfirmButton;
        kingWorkerBuyButton = b.kingWorkerBuyButton;
        kingWagonUpgradeButton = b.kingWagonUpgradeButton;
        kingWagonPathButton = b.kingWagonPathButton;
        banditAmbushButton = b.banditAmbushButton;
        quitGameButton = b.quitGameButton;


        Debug.Log("[UIManager] All references bound from MainBindings");

        ValidateReferences();
        SetupButtonListeners();

        if (GameManager.Instance != null)
        {
            UpdateRoleText(GameManager.Instance.MyRole);
        }
    }



    // Fallback method to find UI elements by tag if binding fails
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

    }

    // Validate that all critical UI references are properly assigned
    private void ValidateReferences()
    {
        bool hasCriticalError = false;
        if (roleText == null) { Debug.LogError("UIManager: roleText is null"); hasCriticalError = true; }
        if (turnStatusText == null) { Debug.LogError("UIManager: turnStatusText is null"); hasCriticalError = true; }
        if (infoText == null) { Debug.LogError("UIManager: infoText is null"); hasCriticalError = true; }
        if (resourcesText == null) { Debug.LogError("UIManager: resourcesText is null"); hasCriticalError = true; }
        if (doneButton == null) { Debug.LogError("UIManager: doneButton is null"); hasCriticalError = true; }

        if (hasCriticalError)
        {
            Debug.LogError("---!!! CRITICAL SETUP ERROR IN UIMANAGER !!!---", this.gameObject);
            Debug.LogError("--> One or more critical UI element references are NOT assigned. Check MainBindings assignments.", this.gameObject);
        }

        if (roundNumberText == null) Debug.LogWarning("roundNumberText is not assigned in UIManager.", this.gameObject);
        if (kingPathButton == null) Debug.LogWarning("kingPathButton is not assigned in UIManager.", this.gameObject);
        if (kingPathConfirmButton == null) Debug.LogWarning("kingPathConfirmButton is not assigned in UIManager.", this.gameObject);
        if (kingWorkerBuyButton == null) Debug.LogWarning("kingWorkerBuyButton is not assigned in UIManager.", this.gameObject);
        if (banditAmbushButton == null) Debug.LogWarning("banditAmbushButton is not assigned in UIManager.", this.gameObject);
        if (quitGameButton == null) Debug.LogWarning("quitGameButton is not assigned in UIManager.", this.gameObject);


    }

    // Initialize UI state and set up button listeners
    void Start()
    {
        SetupButtonListeners();
        SetDoneButtonActive(false);
        SetKingButtonsActive(false);
        SetBanditButtonsActive(false);

        if (GameManager.Instance != null)
        {
            UpdateRoleText(GameManager.Instance.MyRole);
        }
    }

    // Configure click event listeners for all interactive UI buttons
    private void SetupButtonListeners()
    {
        Debug.Log("[UIManager] Setting up button listeners");

        if (doneButton != null)
        {
            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(OnDoneButtonClicked);
        }
        else Debug.LogError("UIManager: doneButton is null when setting up listeners!");

        if (kingPathButton != null)
        {
            kingPathButton.onClick.RemoveAllListeners();
            kingPathButton.onClick.AddListener(OnKingPathButtonClicked);
        }
        if (kingPathConfirmButton != null)
        {
            kingPathConfirmButton.onClick.RemoveAllListeners();
            kingPathConfirmButton.onClick.AddListener(OnKingPathConfirmButtonClicked);
        }
        if (kingWorkerBuyButton != null)
        {
            kingWorkerBuyButton.onClick.RemoveAllListeners();
            kingWorkerBuyButton.onClick.AddListener(OnKingWorkerBuyButtonClicked);
        }
        if (kingWagonUpgradeButton != null)
        {
            kingWagonUpgradeButton.onClick.RemoveAllListeners();
            kingWagonUpgradeButton.onClick.AddListener(OnKingWagonUpgradeButtonClicked);
        }
        if (kingWagonPathButton != null)
        {
            kingWagonPathButton.onClick.RemoveAllListeners();
            kingWagonPathButton.onClick.AddListener(OnKingWagonPathButtonClicked);
        }
        if (banditAmbushButton != null)
        {
            banditAmbushButton.onClick.RemoveAllListeners();
            banditAmbushButton.onClick.AddListener(OnBanditAmbushButtonClicked);
        }
        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveAllListeners();
            quitGameButton.onClick.AddListener(OnQuitGameButtonClicked);
        }

        Debug.Log("[UIManager] Button listeners setup complete");
    }

    // Update the role display text and refresh worker count display
    public void UpdateRoleText(PlayerRole role)
    {
        if (roleText != null)
        {
            roleText.text = $"{role}";
            UpdateWorkerText();
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateRoleText called but roleText is null - UI not yet bound");
        }
    }

    // Update the turn status display with current game phase information
    public void UpdateTurnStatus(string status)
    {
        if (turnStatusText != null)
        {
            turnStatusText.text = $"Turn:\n{status}";
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateTurnStatus called but turnStatusText is null - UI not yet bound");
        }
    }

    // Update the round number display for current game round
    public void UpdateRoundNumber(int roundNumber)
    {
        if (roundNumberText != null)
        {
            roundNumberText.text = $"Round:\n{roundNumber}";
        }
        else
        {
            Debug.LogWarning("roundNumberText is not assigned in UIManager!");
        }
    }

    // Update the information text area with game feedback or instructions
    public void UpdateInfoText(string info)
    {
        if (infoText != null)
        {
            infoText.text = info;
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateInfoText called but infoText is null - UI not yet bound");
        }
    }

    // Update resource display and show floating text animations for changes
    public void UpdateResourcesText(int gold, int wood, int grain)
    {
        if (resourcesText != null)
        {
            // Show floating text for resource changes
            if (gold != previousGold)
            {
                ShowFloatingText(gold - previousGold, goldTextContainer);
            }
            if (wood != previousWood)
            {
                ShowFloatingText(wood - previousWood, woodTextContainer);
            }
            if (grain != previousGrain)
            {
                ShowFloatingText(grain - previousGrain, grainTextContainer);
            }

            resourcesText.text = $"          : {gold}           : {wood}           : {grain}";

            // Update button states when resources change
            UpdateKingWorkerBuyButtonText();
            UpdateBanditAmbushButtonText();
            // Update worker text when resources change (for King)
            UpdateWorkerText();
        }
        else
        {
            Debug.LogWarning("[UIManager] UpdateResourcesText called but resourcesText is null - UI not yet bound");
        }

        // Always update previous resource values and InteractionManager resources
        previousGold = gold;
        previousWood = wood;
        previousGrain = grain;

        // Update InteractionManager resources
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.UpdateResources(gold, wood, grain);
        }
    }

    // Display animated floating text for resource value changes
    private void ShowFloatingText(int change, Transform container)
    {
        if (floatingTextPrefab == null) return;

        GameObject floatingTextObject = Instantiate(floatingTextPrefab, container);
        FloatingText floatingText = floatingTextObject.GetComponent<FloatingText>();

        string text = (change > 0 ? "+" : "") + change;
        bool isPositive = change > 0;

        floatingText.SetText(text, isPositive);
    }

    // Update the worker count display for King players showing available workers
    public void UpdateWorkerText()
    {
        if (workerText == null) return;

        if (InteractionManager.Instance != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.MyRole == PlayerRole.King)
            {
                int availableForPlacement = InteractionManager.Instance.GetPurchasedWorkerCount() - InteractionManager.Instance.GetCompletedPathCount();
                workerText.text = $"x{availableForPlacement}";

                // Ensure the GameObject is active and visible
                if (workerText.gameObject != null)
                {
                    workerText.gameObject.SetActive(true);
                }
            }
            else
            {
                workerText.text = "";
                if (workerText.gameObject != null)
                {
                    workerText.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            workerText.text = "x0";
            if (workerText.gameObject != null)
            {
                workerText.gameObject.SetActive(true);
            }
        }
    }

    // Control visibility and interactability of the Done button based on game state
    public void SetDoneButtonActive(bool isActive)
    {
        if (doneButton != null && doneButton.gameObject != null)
        {
            // For King planning phase, only activate if paths exist
            if (isActive && GameManager.Instance?.CurrentTurn == GameTurn.KingPlanning)
            {
                bool hasCompletedPaths = InteractionManager.Instance?.HasCompletedPaths() ?? false;
                doneButton.gameObject.SetActive(hasCompletedPaths);
                
                if (doneButton.TryGetComponent<UnityEngine.UI.Button>(out var buttonComponent))
                {
                    buttonComponent.interactable = hasCompletedPaths;
                }
            }
            else
            {
                doneButton.gameObject.SetActive(isActive);
            }
        }
    }

    // Refresh the Done button state based on current turn requirements
    public void UpdateDoneButtonState()
    {
        if (GameManager.Instance?.CurrentTurn == GameTurn.KingPlanning)
        {
            SetDoneButtonActive(true);
        }
    }

    // Control visibility and state of all King-specific action buttons
    public void SetKingButtonsActive(bool isActive)
    {
        if (kingPathButton != null && kingPathButton.gameObject != null)
        {
            kingPathButton.gameObject.SetActive(false);
        }
        if (kingPathConfirmButton != null && kingPathConfirmButton.gameObject != null)
        {
            kingPathConfirmButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingPathConfirmButtonText();
            }
        }
        if (kingWorkerBuyButton != null && kingWorkerBuyButton.gameObject != null)
        {
            kingWorkerBuyButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWorkerBuyButtonText();
            }
        }
        if (kingWagonUpgradeButton != null && kingWagonUpgradeButton.gameObject != null)
        {
            kingWagonUpgradeButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWagonUpgradeButtonText();
            }
        }
        if (kingWagonPathButton != null && kingWagonPathButton.gameObject != null)
        {
            kingWagonPathButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateKingWagonPathButtonText();
            }
        }
    }

    // Control visibility and state of all Bandit-specific action buttons
    public void SetBanditButtonsActive(bool isActive)
    {
        if (banditAmbushButton != null && banditAmbushButton.gameObject != null)
        {
            banditAmbushButton.gameObject.SetActive(isActive);
            if (isActive)
            {
                UpdateBanditAmbushButtonText();
            }
        }
    }

    // Update the King path creation button text and interactability state
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

    // Update the King path confirmation button text and enable state
    public void UpdateKingPathConfirmButtonText()
    {
        if (kingPathConfirmButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingPathConfirmButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Confirm Path";
            }
            // Only enable confirm button when path is ready to confirm
            kingPathConfirmButton.interactable = InteractionManager.Instance.CanConfirmPath();
        }
    }

    // Update the King worker purchase button text and availability
    public void UpdateKingWorkerBuyButtonText()
    {
        /*
        if (kingWorkerBuyButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWorkerBuyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = InteractionManager.Instance.GetWorkerBuyButtonText();
            }
            kingWorkerBuyButton.interactable = InteractionManager.Instance.CanBuyWorker();
        }
        */
    }

    // Update the King wagon upgrade button text and interactability
    public void UpdateKingWagonUpgradeButtonText()
    {
        if (kingWagonUpgradeButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWagonUpgradeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                int availableWorkers = InteractionManager.Instance.GetAvailableRegularWorkerCount();
                buttonText.text = "Upgrade";
            }
            kingWagonUpgradeButton.interactable = InteractionManager.Instance.CanUpgradeWorkerToWagon();
        }
    }

    // Update the King wagon path creation button text and availability
    public void UpdateKingWagonPathButtonText()
    {
        if (kingWagonPathButton != null && InteractionManager.Instance != null)
        {
            var buttonText = kingWagonPathButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                int availableWagonWorkers = InteractionManager.Instance.GetAvailableWagonWorkerCount();
                buttonText.text = "Wagon Path";
            }
            kingWagonPathButton.interactable = InteractionManager.Instance.GetAvailableWagonWorkerCount() > 0;
        }
    }

    // Update the Bandit ambush purchase button text and interactability
    public void UpdateBanditAmbushButtonText()
    {
        /*
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
        */
    }

    // === BUTTON CLICK HANDLERS ===

    // Handle King worker purchase button click and update related UI elements
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
        UpdateKingWagonUpgradeButtonText();
        UpdateWorkerText();
    }

    // Handle King wagon upgrade button click and refresh dependent buttons
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

    // Handle King wagon path creation button click and update UI state
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

    // Handle King worker path creation button click and initiate path planning
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

        UpdateKingPathConfirmButtonText();
        UpdateKingWorkerBuyButtonText();
        UpdateWorkerText();
    }

    // Handle King path confirmation button click and finalize current path
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

        UpdateKingPathConfirmButtonText();
        UpdateKingWorkerBuyButtonText();
        UpdateWorkerText();
    }

    // Handle quit game button click and initiate game termination process
    private void OnQuitGameButtonClicked()
    {

        Debug.Log("Quit Game button clicked!");
        InteractionManager.Instance.QuitGameRequest();
    }

    // Handle Bandit ambush purchase button click and update button state
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

    // Handle Done button click and submit turn actions based on current player role
    private void OnDoneButtonClicked()
    {
        Debug.Log($"Done button clicked! Current turn: {GameManager.Instance.CurrentTurn}");
        infoText.text = ""; // Clear any previous info/error messages

        // Check which turn it is to send the correct message
        if (GameManager.Instance.CurrentTurn == GameTurn.KingPlanning)
        {
            // Check if player has completed paths
            if (!InteractionManager.Instance.HasCompletedPaths())
            {
                UpdateInfoText("Please create at least one path before submitting! Click 'New Path' to start.");
                return;
            }

            bool success = InteractionManager.Instance.SubmitPath();
            if (success)
            {
                // Hide King buttons only after successful submission
                SetKingButtonsActive(false);
                SetDoneButtonActive(false);
            }
            // If submission failed, buttons remain visible for retry
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

    // Update button visibility and auto-start actions based on current turn and player role
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

                    if (InteractionManager.Instance != null && InteractionManager.Instance.CanCreateNewPath())
                    {
                        InteractionManager.Instance.StartNewPath();
                        UpdateInfoText($"King planning phase started! Select a resource field to create path #{InteractionManager.Instance.GetCompletedPathCount() + 1}.");
                    }
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

    // Display end game screen and perform cleanup before transitioning to result scene
    public void ShowEndGamePanel(bool didIWin)
    {
        Debug.Log($"[UIManager] ShowEndGamePanel called - didIWin: {didIWin}");

        // CLEANUP BEFORE SCENE TRANSITION
        Debug.Log("[UIManager] Performing cleanup before scene transition...");

        // 1. Reset GameManager state
        if (GameManager.Instance != null)
        {
            Debug.Log("[UIManager] Resetting GameManager state...");
            GameManager.Instance.ResetGameStateOnly();
        }

        // 2. Reset InteractionManager state
        if (InteractionManager.Instance != null)
        {
            Debug.Log("[UIManager] Resetting InteractionManager state...");
            InteractionManager.Instance.ForceCompleteReset();
        }

        // 3. Disconnect from server
        if (NetworkManager.Instance != null)
        {
            Debug.Log("[UIManager] Disconnecting from server...");
            _ = NetworkManager.Instance.Disconnect();
        }

        // 3.5. Reset all persistent singleton states instead of destroying them
        Debug.Log("[UIManager] Resetting all persistent singleton states...");

        // 4. Clear UI references (they'll be rebound when returning to main scene)
        Debug.Log("[UIManager] Clearing UI references...");
        roleText = null;
        turnStatusText = null;
        roundNumberText = null;
        infoText = null;
        resourcesText = null;
        workerText = null;
        doneButton = null;
        kingPathButton = null;
        kingPathConfirmButton = null;
        kingWorkerBuyButton = null;
        kingWagonUpgradeButton = null;
        kingWagonPathButton = null;
        banditAmbushButton = null;
        quitGameButton = null;

        Debug.Log("[UIManager] Cleanup complete, loading scene...");

        // 5. Load the appropriate scene
        if (didIWin)
        {
            Debug.Log("👑 GAME OVER - YOU WIN! 👑 Loading WinnerScreen...");
            SceneManager.LoadScene("WinnerScreen");
        }
        else
        {
            Debug.Log("💀 GAME OVER - YOU LOSE! 💀 Loading LoserScreen...");
            SceneManager.LoadScene("LoserScreen");
        }
    }

    // Complete reset for a new game session with UI state cleanup
    public void ResetForNewGame()
    {
        Debug.Log("[UIManager] ResetForNewGame() - Resetting UI state");

        // Check if we're in a scene that has UI elements (not title scene)
        if (roleText == null && turnStatusText == null && doneButton == null)
        {
            Debug.Log("[UIManager] ResetForNewGame() - No UI elements found, likely in title scene. Skipping UI reset.");
            return;
        }

        // Reset all UI text to default states (with null checks)
        if (roleText != null) roleText.text = "Role:\nNone";
        if (turnStatusText != null) turnStatusText.text = "Turn:\nWaiting";
        if (roundNumberText != null) roundNumberText.text = "Round:\n0";
        if (infoText != null) infoText.text = "";
        if (resourcesText != null) resourcesText.text = "Gold: 0\nWood: 0\nGrain: 0";
        if (workerText != null) workerText.text = "";

        // Hide all buttons (with null checks)
        SetDoneButtonActive(false);
        SetKingButtonsActive(false);
        SetBanditButtonsActive(false);

        // Reset UI elements (with null checks)

        // Show main game UI elements (with null checks)
        if (roleText != null && roleText.gameObject != null) roleText.gameObject.SetActive(true);
        if (turnStatusText != null && turnStatusText.gameObject != null) turnStatusText.gameObject.SetActive(true);
        if (roundNumberText != null && roundNumberText.gameObject != null) roundNumberText.gameObject.SetActive(true);
        if (infoText != null && infoText.gameObject != null) infoText.gameObject.SetActive(true);
        if (resourcesText != null && resourcesText.gameObject != null) resourcesText.gameObject.SetActive(true);
        if (workerText != null && workerText.gameObject != null) workerText.gameObject.SetActive(true);

        Debug.Log("[UIManager] ResetForNewGame() - UI reset complete");
    }

}
