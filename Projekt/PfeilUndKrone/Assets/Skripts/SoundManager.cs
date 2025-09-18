using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Events;

public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    public static SoundManager Instance 
    { 
        get 
        { 
            if (_instance == null)
            {
                _instance = FindObjectOfType<SoundManager>();
            }
            return _instance; 
        } 
    }
    
    // Raised whenever the play/pause state changes (or is applied)
    public static event System.Action<bool> OnPlayStateChanged;
    
    [Header("Audio Components")]
    public AudioSource backgroundMusic;
    public Slider volumeSlider;
    public AudioMixer audioMixer;
    
    [Header("Settings")]
    [SerializeField]
    private string volumeParameter = "MasterParam";
    private float lastVolume = 0.5f;
    private bool isPlaying = true;
    
    private static AudioSource persistentAudioSource;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
            
            // Store the AudioSource
            if (backgroundMusic != null)
            {
                persistentAudioSource = backgroundMusic;
                if (backgroundMusic.transform.parent == null || backgroundMusic.transform.parent == transform)
                {
                    DontDestroyOnLoad(backgroundMusic.gameObject);
                }
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        if (persistentAudioSource != null)
        {
            backgroundMusic = persistentAudioSource;
        }
    }

    private void Start()
    {
        if (persistentAudioSource != null)
        {
            backgroundMusic = persistentAudioSource;
        }
        
        if (volumeSlider != null)
        {
            volumeSlider.value = lastVolume;
            SetVolume(volumeSlider.value);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        ApplyMusicState();
    }
    
    private void ApplyMusicState()
    {
        if (backgroundMusic == null) return;
        
        if (isPlaying && !backgroundMusic.isPlaying)
        {
            backgroundMusic.Play();
            Debug.Log("Music started playing");
        }
        else if (!isPlaying && backgroundMusic.isPlaying)
        {
            backgroundMusic.Pause();
            Debug.Log("Music paused");
        }
    // Notify listeners so all UI can sync to the current state
    NotifyPlayStateChanged();
    }

    public void SetVolume(float volume)
    {
        if (volume > 0)
        {
            lastVolume = volume;
        }

        // Konvertiere den Slider-Wert in Dezibel
        float dB = Mathf.Log10(volume) * 20;
        if (volume == 0)
            dB = -80;

        audioMixer.SetFloat(volumeParameter, dB);
        SaveSettings();
    }

    public void ToggleMute()
    {
        TogglePlayPause();
    }
    
    public void TogglePlayPause()
    {
        // Always use the persistent AudioSource
        if (persistentAudioSource != null)
        {
            backgroundMusic = persistentAudioSource;
        }
        
        if (backgroundMusic == null) 
        {
            Debug.LogWarning("No AudioSource found for music control!");
            return;
        }
        
        isPlaying = !isPlaying;
        
        if (isPlaying)
        {
            backgroundMusic.Play();
            Debug.Log("Music Playing");
        }
        else
        {
            backgroundMusic.Pause();
            Debug.Log("Music Paused");
        }
        
    // Let any subscribed buttons update
    NotifyPlayStateChanged();
        SaveSettings();
    }

    public bool IsPlaying()
    {
        return isPlaying;
    }
    
    // Static methods for external buttons in any scene
    public static void ToggleMusic()
    {
        if (Instance != null)
        {
            Instance.TogglePlayPause();
        }
        else
        {
            Debug.LogError("❌ SoundManager Instance not found!");
        }
    }
    
    public static bool IsMusicPlaying()
    {
        return Instance != null ? Instance.IsPlaying() : true;
    }
    
    // UI binders can call this to force an immediate sync when enabling
    public static void ForceSyncUI()
    {
        if (Instance != null)
        {
            Instance.NotifyPlayStateChanged();
        }
    }
    
    private void LoadSettings()
    {
        lastVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        isPlaying = PlayerPrefs.GetInt("MusicPlaying", 1) == 1;
        Debug.Log($"Loaded settings - Volume: {lastVolume}, Playing: {isPlaying}");
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume", lastVolume);
        PlayerPrefs.SetInt("MusicPlaying", isPlaying ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Saved settings - Volume: {lastVolume}, Playing: {isPlaying}");
    }
    
    // Debug method to check state
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogCurrentState()
    {
        Debug.Log($"SoundManager State - Playing: {isPlaying}, AudioSource: {(backgroundMusic != null ? backgroundMusic.name : "NULL")}, Instance: {(_instance != null ? "OK" : "NULL")}");
    }

    private void NotifyPlayStateChanged()
    {
        OnPlayStateChanged?.Invoke(isPlaying);
    }
}
