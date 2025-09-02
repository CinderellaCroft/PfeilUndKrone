using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Events;

public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;
    
    public AudioSource backgroundMusic;
    public Slider volumeSlider;
    public AudioMixer audioMixer;
    [SerializeField]
    private string volumeParameter = "MasterParam";
    private float lastVolume = 0.5f;
    private bool isMuted = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = lastVolume;
            SetVolume(volumeSlider.value);

            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    public void SetVolume(float volume)
    {
        if (!isMuted)
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
        }
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        
        if (isMuted)
        {
            if (volumeSlider != null && volumeSlider.value > 0)
            {
                lastVolume = volumeSlider.value;
            }
            audioMixer.SetFloat(volumeParameter, -80f);
        }
        else
        {
            if (volumeSlider != null)
            {
                volumeSlider.value = lastVolume;
            }
            SetVolume(lastVolume);
        }
    }

    public bool IsMuted()
    {
        return isMuted;
    }
}
