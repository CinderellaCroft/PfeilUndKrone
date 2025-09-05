using UnityEngine;
using UnityEngine.SceneManagement;
using PimDeWitte.UnityMainThreadDispatcher;
using NetworkingDTOs;
using System.Threading.Tasks;

/// <summary>
/// Minimal manager for the title scene that handles networking and scene transitions.
/// This script ensures all required singletons are available for networking in the title scene.
/// </summary>
public class TitleSceneManager : MonoBehaviour
{
    [Header("Scene Setup")]
    [SerializeField] private string mainSceneName = "Main";
    
    private NetworkManager networkManager;
    private UnityMainThreadDispatcher mainThreadDispatcher;
    private static string assignedRole = null; // Store the role for GameManager to use
    
    private void Awake()
    {
        Debug.Log("[TitleSceneManager] Setting up title scene components...");
        
        // Clear any stored role from previous game
        assignedRole = null;
        Debug.Log("[TitleSceneManager] Cleared previous role assignment");
        
        // If we're returning from a game, perform complete cleanup
        if (GameManager.Instance != null)
        {
            Debug.Log("[TitleSceneManager] Found existing GameManager - performing complete game reset");
            // Only reset game state, don't call UI reset in title scene
            GameManager.Instance.ResetGameStateOnly();
        }
        
        // Create NetworkManager if it doesn't exist
        if (NetworkManager.Instance == null)
        {
            Debug.Log("[TitleSceneManager] Creating NetworkManager instance...");
            GameObject nmObject = new GameObject("NetworkManager");
            networkManager = nmObject.AddComponent<NetworkManager>();
            DontDestroyOnLoad(nmObject);
        }
        else
        {
            networkManager = NetworkManager.Instance;
            Debug.Log("[TitleSceneManager] NetworkManager already exists.");
        }
        
        // Create MainThreadDispatcher if it doesn't exist
        if (!UnityMainThreadDispatcher.Exists())
        {
            Debug.Log("[TitleSceneManager] Creating UnityMainThreadDispatcher instance...");
            GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
            mainThreadDispatcher = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(dispatcherObject);
        }
        else
        {
            Debug.Log("[TitleSceneManager] UnityMainThreadDispatcher already exists.");
        }
        
        // Subscribe to match creation event for automatic scene transition
        if (networkManager != null)
        {
            networkManager.OnRoleAssigned += OnMatchCreated;
            Debug.Log("[TitleSceneManager] Subscribed to OnRoleAssigned event.");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up event subscriptions
        if (networkManager != null)
        {
            networkManager.OnRoleAssigned -= OnMatchCreated;
            Debug.Log("[TitleSceneManager] Unsubscribed from OnRoleAssigned event.");
        }
    }
    
    /// <summary>
    /// Called when a match is created and role is assigned. Automatically transitions to main scene.
    /// </summary>
    private void OnMatchCreated(string role)
    {
        Debug.Log($"[TitleSceneManager] Match created! Assigned role: {role}. Transitioning to {mainSceneName} scene...");
        
        // Store the role for GameManager to use when it loads
        string previousRole = assignedRole;
        assignedRole = role;
        Debug.Log($"[TitleSceneManager] Stored role: {assignedRole} for GameManager (previous was: {previousRole})");
        Debug.Log($"[TitleSceneManager] Role change: '{previousRole}' → '{assignedRole}'");
        
        // Ensure the scene transition happens on the main thread
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                SceneManager.LoadScene(mainSceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(mainSceneName);
        }
    }
    
    /// <summary>
    /// Public method for GameManager to get the assigned role
    /// </summary>
    public static string GetAssignedRole()
    {
        Debug.Log($"[TitleSceneManager] GetAssignedRole() called, returning: '{assignedRole}'");
        return assignedRole;
    }
    
    private void Start()
    {
        Debug.Log("[TitleSceneManager] Title scene setup complete. All required components should be available.");
        
        // Validate that required components exist
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[TitleSceneManager] NetworkManager.Instance is still null after setup!");
        }
        
        if (!UnityMainThreadDispatcher.Exists())
        {
            Debug.LogError("[TitleSceneManager] UnityMainThreadDispatcher is still missing after setup!");
        }
    }
}