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
        // Wire up all the button listeners
        findRandomGameBtn.onClick.AddListener(HandleFindRandomGame);
        showCreateGameViewBtn.onClick.AddListener(HandleCreatePrivateGame);
        showJoinGameViewBtn.onClick.AddListener(() =>
        {
            createGameViewPanel.SetActive(false);
            joinGameViewPanel.SetActive(true);
        });

        copyLobbyIdBtn.onClick.AddListener(HandleCopyLobbyId);
        joinGameBtn.onClick.AddListener(HandleJoinGameById);
        
        // Ensure panels are hidden at the start
        createGameViewPanel.SetActive(false);
        joinGameViewPanel.SetActive(false);
    }

    // --- Button Handlers ---

    private async void HandleFindRandomGame()
    {
        Debug.Log("UI: Find Random Game clicked.");
        // Ensure we are connected before sending the message
        if (!NetworkManager.Instance.IsConnected)
        {
            await NetworkManager.Instance.Connect();
        }
        NetworkManager.Instance.JoinRandomLobby();
        // Hide these panels, the UIManager will show a "waiting..." text
        gameObject.SetActive(false); 
    }

    private async void HandleCreatePrivateGame()
    {
        Debug.Log("UI: Create Private Game clicked.");
        if (!NetworkManager.Instance.IsConnected)
        {
            await NetworkManager.Instance.Connect();
        }
        NetworkManager.Instance.CreatePrivateLobby();
        // Show the create view, which will be populated by DisplayLobbyId
        createGameViewPanel.SetActive(true);
        joinGameViewPanel.SetActive(false);
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
        if (!NetworkManager.Instance.IsConnected)
        {
            await NetworkManager.Instance.Connect();
        }
        NetworkManager.Instance.JoinLobbyById(lobbyId);
        // Hide these panels, the UIManager will update text on success
        gameObject.SetActive(false);
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