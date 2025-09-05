// Create a new script: LobbyUIController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUIController : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    [Tooltip("Button to find a random public game.")]
    [SerializeField] private Button findRandomGameBtn;
    [Tooltip("Button to show the private lobby creation view.")]
    [SerializeField] private Button showCreateGameViewBtn;
    [Tooltip("Button to show the view for joining a lobby by ID.")]
    [SerializeField] private Button showJoinGameViewBtn;

    [Header("Create Game View")]
    [Tooltip("The panel that shows the created lobby ID.")]
    [SerializeField] private GameObject createGameViewPanel;
    [Tooltip("The text element to display the lobby ID.")]
    [SerializeField] private TMP_Text lobbyIdText;
    [Tooltip("The button to copy the lobby ID to the clipboard.")]
    [SerializeField] private Button copyLobbyIdBtn;

    [Header("Join Game View")]
    [Tooltip("The panel for entering a lobby ID to join.")]
    [SerializeField] private GameObject joinGameViewPanel;
    [Tooltip("The input field for the lobby ID.")]
    [SerializeField] private TMP_InputField lobbyIdInput;
    [Tooltip("The button to confirm and join the specified lobby.")]
    [SerializeField] private Button joinGameBtn;

    void Start()
    {
        // Wire up all the button listeners with null checks
        if (findRandomGameBtn != null)
            findRandomGameBtn.onClick.AddListener(HandleFindRandomGame);
        else
            Debug.LogError("findRandomGameBtn is null in LobbyUIController!");

        if (showCreateGameViewBtn != null)
            showCreateGameViewBtn.onClick.AddListener(HandleCreatePrivateGame);
        else
            Debug.LogError("showCreateGameViewBtn is null in LobbyUIController!");

        if (showJoinGameViewBtn != null)
        {
            showJoinGameViewBtn.onClick.AddListener(() =>
            {
                if (createGameViewPanel != null) createGameViewPanel.SetActive(false);
                if (joinGameViewPanel != null) joinGameViewPanel.SetActive(true);
            });
        }
        else
        {
            Debug.LogError("showJoinGameViewBtn is null in LobbyUIController!");
        }

        if (copyLobbyIdBtn != null)
            copyLobbyIdBtn.onClick.AddListener(HandleCopyLobbyId);
        else
            Debug.LogError("copyLobbyIdBtn is null in LobbyUIController!");

        if (joinGameBtn != null)
            joinGameBtn.onClick.AddListener(HandleJoinGameById);
        else
            Debug.LogError("joinGameBtn is null in LobbyUIController!");
        
        // Ensure panels are hidden at the start with null checks
        if (createGameViewPanel != null) createGameViewPanel.SetActive(false);
        if (joinGameViewPanel != null) joinGameViewPanel.SetActive(false);
    }

    // --- Button Handlers ---

    private async void HandleFindRandomGame()
    {
        Debug.Log("UI: Find Random Game clicked.");
        
        // Check if NetworkManager exists
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager.Instance is null! Make sure TitleSceneManager is in the scene.");
            return;
        }
        
        // Ensure we are connected before sending the message
        if (!NetworkManager.Instance.IsConnected)
        {
            Debug.Log("Not connected, attempting to connect...");
            await NetworkManager.Instance.Connect();
            
            // Wait for connection to be properly established
            int attempts = 0;
            while (!NetworkManager.Instance.IsConnected && attempts < 50) // Max 5 seconds
            {
                await System.Threading.Tasks.Task.Delay(100);
                attempts++;
                Debug.Log($"Waiting for connection... Attempt {attempts}, IsConnected: {NetworkManager.Instance.IsConnected}");
            }
        }
        
        // Double-check connection before sending
        Debug.Log($"Connection state after connect: {NetworkManager.Instance.IsConnected}");
        if (NetworkManager.Instance.IsConnected)
        {
            Debug.Log("About to call JoinRandomLobby()");
            NetworkManager.Instance.JoinRandomLobby();
            Debug.Log("JoinRandomLobby() called successfully");
            // Hide these panels, the UIManager will show a "waiting..." text
            gameObject.SetActive(false);
            Debug.Log("LobbyUI panels hidden - waiting for server response");
        }
        else
        {
            Debug.LogError("Failed to connect to server!");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInfoText("Failed to connect to server!");
            }
        }
    }

    private async void HandleCreatePrivateGame()
    {
        Debug.Log("UI: Create Private Game clicked.");
        
        // Check if NetworkManager exists
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager.Instance is null! Make sure TitleSceneManager is in the scene.");
            return;
        }
        
        if (!NetworkManager.Instance.IsConnected)
        {
            Debug.Log("Not connected, attempting to connect...");
            await NetworkManager.Instance.Connect();
        }
        
        if (NetworkManager.Instance.IsConnected)
        {
            NetworkManager.Instance.CreatePrivateLobby();
            // Show the create view, which will be populated by DisplayLobbyId
            createGameViewPanel.SetActive(true);
            joinGameViewPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("Failed to connect to server!");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInfoText("Failed to connect to server!");
            }
        }
    }

    private async void HandleJoinGameById()
    {
        string lobbyId = lobbyIdInput.text.Trim();
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            Debug.LogWarning("Lobby ID input is empty.");
            return;
        }

        Debug.Log($"UI: Joining game with ID: {lobbyId}");
        
        // Check if NetworkManager exists
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager.Instance is null! Make sure TitleSceneManager is in the scene.");
            return;
        }
        
        if (!NetworkManager.Instance.IsConnected)
        {
            Debug.Log("Not connected, attempting to connect...");
            await NetworkManager.Instance.Connect();
        }
        
        if (NetworkManager.Instance.IsConnected)
        {
            NetworkManager.Instance.JoinLobbyById(lobbyId);
            // Hide these panels, the UIManager will update text on success
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Failed to connect to server!");
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateInfoText("Failed to connect to server!");
            }
        }
    }

    private void HandleCopyLobbyId()
    {
        GUIUtility.systemCopyBuffer = lobbyIdText.text;
        Debug.Log($"Copied to clipboard: {lobbyIdText.text}");
        // You could add some user feedback here, like a "Copied!" message.
    }

    // --- Public Methods ---

    /// <summary>
    /// Called by the UIManager to display the received lobby ID.
    /// </summary>
    public void DisplayLobbyId(string lobbyId)
    {
        lobbyIdText.text = lobbyId;
    }
}