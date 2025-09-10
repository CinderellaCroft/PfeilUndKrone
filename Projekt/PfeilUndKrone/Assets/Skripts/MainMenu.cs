using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Add this for TextMeshPro elements
using UnityEngine.UI; // Add this for Button elements

public class MainMenu : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject joinGameView;
    [SerializeField] private GameObject createGameView;

    [Header("Join Game View")]
    [SerializeField] private TMP_InputField lobbyIdInput;
    [SerializeField] private Button joinGameByIdBtn;

    [Header("Create Game View")]
    [SerializeField] private TextMeshProUGUI lobbyIdText;
    [SerializeField] private Button copyLobbyIdBtn;

    [Header("General UI")]
    [SerializeField] private TextMeshProUGUI infoText; // Optional: A text to show "Connecting...", "Waiting..."

    private void Awake()
    {
        // Make sure NetworkManager is connected when the scene starts
        // This assumes TitleSceneManager has already ensured NetworkManager exists
        ConnectToServer();
    }

    private void OnEnable()
    {
        // Subscribe to events from NetworkManager
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnLobbyCreated += HandleLobbyCreated;
            NetworkManager.Instance.OnLobbyInfo += HandleLobbyInfo;
        }

        // Add button listeners programmatically
        if (joinGameByIdBtn != null)
        {
            joinGameByIdBtn.onClick.AddListener(JoinLobby);
        }
        if (copyLobbyIdBtn != null)
        {
            copyLobbyIdBtn.onClick.AddListener(CopyLobbyId);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnLobbyCreated -= HandleLobbyCreated;
            NetworkManager.Instance.OnLobbyInfo -= HandleLobbyInfo;
        }

        // Remove listeners
        if (joinGameByIdBtn != null)
        {
            joinGameByIdBtn.onClick.RemoveListener(JoinLobby);
        }
        if (copyLobbyIdBtn != null)
        {
            copyLobbyIdBtn.onClick.RemoveListener(CopyLobbyId);
        }
    }

    private async void ConnectToServer()
    {
        if (infoText != null) infoText.text = "Connecting to server...";
        await NetworkManager.Instance.Connect();
        if (NetworkManager.Instance.IsConnected)
        {
            if (infoText != null) infoText.text = "Connected!";
        }
        else
        {
            if (infoText != null) infoText.text = "Connection failed. Please restart.";
        }
    }

    // --- Public Methods for UI Buttons ---

    public void ShowJoinGameView()
    {
        joinGameView.SetActive(true);
        createGameView.SetActive(false);
    }

    public void ShowCreateGameView()
    {
        createGameView.SetActive(true);
        joinGameView.SetActive(false);
        NetworkManager.Instance.CreatePrivateLobby();
        if (infoText != null) infoText.text = "Creating lobby...";
    }

    public void JoinLobby()
    {
        string lobbyId = lobbyIdInput.text;
        if (string.IsNullOrEmpty(lobbyId))
        {
            Debug.LogWarning("Lobby ID input is empty.");
            if (infoText != null) infoText.text = "Please enter a Lobby ID.";
            return;
        }
        NetworkManager.Instance.JoinLobbyById(lobbyId);
    }

    public void CopyLobbyId()
    {
        GUIUtility.systemCopyBuffer = lobbyIdText.text;
        Debug.Log($"Copied to clipboard: {lobbyIdText.text}");
        if (infoText != null) infoText.text = "Lobby ID copied!";
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void HandleLobbyCreated(string lobbyId)
    {
        // This is called when NetworkManager receives the "lobby_created" message
        lobbyIdText.text = lobbyId;
        if (infoText != null) infoText.text = "Lobby created! Share the ID with a friend.";
    }

    private void HandleLobbyInfo(string message)
    {
        // This is called for general status updates
        if (infoText != null)
        {
            infoText.text = message;
        }
    }
}