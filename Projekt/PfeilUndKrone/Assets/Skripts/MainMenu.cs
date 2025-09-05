using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Add this for TextMeshPro elements

public class MainMenu : MonoBehaviour
{
    // --- NEW: Singleton Pattern ---
    // This allows the NetworkManager to easily find and talk to this script.
    public static MainMenu Instance { get; private set; }

    // --- NEW: Lobby UI References ---
    [Header("Lobby UI Panels")]
    [SerializeField] private GameObject createGameViewPanel;
    [SerializeField] private GameObject joinGameViewPanel;

    [Header("Lobby Buttons")]
    [SerializeField] private Button findRandomGameBtn;
    [SerializeField] private Button showCreateGameViewBtn;
    [SerializeField] private Button showJoinGameViewBtn;
    [SerializeField] private Button copyLobbyIdBtn;
    [SerializeField] private Button joinGameBtn;

    [Header("Lobby Text & Input")]
    [SerializeField] private TMP_Text lobbyIdText; // Displays the created lobby ID
    [SerializeField] private TMP_InputField lobbyIdInput; // For typing in a lobby ID
    [SerializeField] private TMP_Text statusText; // For showing "Connecting...", "Waiting..."

    private void Awake()
    {
        // --- NEW: Singleton Setup ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // --- NEW: Wire up all the lobby button listeners ---
        if (findRandomGameBtn != null) findRandomGameBtn.onClick.AddListener(HandleFindRandomGame);
        if (showCreateGameViewBtn != null) showCreateGameViewBtn.onClick.AddListener(HandleCreatePrivateGame);
        if (showJoinGameViewBtn != null) showJoinGameViewBtn.onClick.AddListener(ShowJoinGamePanel);
        if (copyLobbyIdBtn != null) copyLobbyIdBtn.onClick.AddListener(HandleCopyLobbyId);
        if (joinGameBtn != null) joinGameBtn.onClick.AddListener(HandleJoinGameById);
        
        // Ensure panels are hidden at the start
        if (createGameViewPanel != null) createGameViewPanel.SetActive(false);
        if (joinGameViewPanel != null) joinGameViewPanel.SetActive(false);
        if(statusText != null) statusText.text = "";
    }
    
    // --- ADJUSTED METHOD ---
    /// <summary>
    /// Toggles the visibility of a UI panel. Ensures that only one panel (create or join) is visible at a time.
    /// If the panel to be toggled is already active, it will be hidden.
    /// If it's inactive, it will be shown and the other panel will be hidden.
    /// </summary>
    /// <param name="panel">The panel to toggle.</param>
    public void TogglePanel(GameObject panel)
    {
        if (panel != null)
        {
            // First, get the current state of the panel we are trying to toggle.
            bool wasActive = panel.activeSelf;

            // Deactivate all managed panels to ensure only one is visible at a time.
            if (createGameViewPanel != null) createGameViewPanel.SetActive(false);
            if (joinGameViewPanel != null) joinGameViewPanel.SetActive(false);

            // If the panel was not active before, activate it now.
            // This results in a "show" behavior while hiding others.
            // If it was already active, it remains hidden from the step above,
            // resulting in a "hide" behavior.
            if (!wasActive)
            {
                panel.SetActive(true);
            }
        }
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // --- NEW: Lobby Logic Handlers ---

    private async void HandleFindRandomGame()
    {
        UpdateStatusText("Connecting...");
        if (!NetworkManager.Instance.IsConnected) await NetworkManager.Instance.Connect();
        UpdateStatusText("Finding a random game...");
        NetworkManager.Instance.JoinRandomLobby();
    }

    private async void HandleCreatePrivateGame()
    {
        UpdateStatusText("Connecting...");
        if (!NetworkManager.Instance.IsConnected) await NetworkManager.Instance.Connect();
        UpdateStatusText("Creating a private lobby...");
        NetworkManager.Instance.CreatePrivateLobby();
    }

    private void ShowJoinGamePanel()
    {
        if (createGameViewPanel != null) createGameViewPanel.SetActive(false);
        if (joinGameViewPanel != null) joinGameViewPanel.SetActive(true);
        UpdateStatusText("Enter a Lobby ID to join.");
    }

    private async void HandleJoinGameById()
    {
        string lobbyId = lobbyIdInput.text.Trim();
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            UpdateStatusText("Lobby ID cannot be empty.");
            return;
        }

        UpdateStatusText("Connecting...");
        if (!NetworkManager.Instance.IsConnected) await NetworkManager.Instance.Connect();
        UpdateStatusText($"Joining lobby {lobbyId}...");
        NetworkManager.Instance.JoinLobbyById(lobbyId);
    }

    private void HandleCopyLobbyId()
    {
        GUIUtility.systemCopyBuffer = lobbyIdText.text;
        UpdateStatusText($"Copied '{lobbyIdText.text}' to clipboard!");
    }

    // --- NEW: Public Methods for NetworkManager ---

    public void ShowCreatedLobbyPanel(string lobbyId)
    {
        lobbyIdText.text = lobbyId;
        UpdateStatusText("Lobby created! Share the ID with a friend.");
        if (createGameViewPanel != null) createGameViewPanel.SetActive(true);
        if (joinGameViewPanel != null) joinGameViewPanel.SetActive(false);
    }

    public void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
