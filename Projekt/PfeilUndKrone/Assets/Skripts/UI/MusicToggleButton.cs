using UnityEngine;
using UnityEngine.UI;

// Attach to a UI Button OR a UI Toggle to bind it to the global music state
public class MusicToggleButton : MonoBehaviour
{
    [Header("Optional visuals")]
    [SerializeField] private Text label;
    [SerializeField] private GameObject playIcon;
    [SerializeField] private GameObject pauseIcon;
    [SerializeField] private bool disableChildRaycasts = true;

    private Button _button;
    private Toggle _toggle;
    private bool _suppressToggleEvent;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _toggle = GetComponent<Toggle>();

        if (_button != null)
        {
            _button.onClick.AddListener(OnClick);
        }
        if (_toggle != null)
        {
            _toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        if (_button == null && _toggle == null)
        {
            Debug.LogWarning("MusicToggleButton: No Button or Toggle found on this GameObject.");
        }

        if (disableChildRaycasts)
        {
            TryDisableBlockingRaycasts();
        }
    }

    private void OnEnable()
    {
        SoundManager.OnPlayStateChanged += HandleStateChanged;
        // Force initial sync in case we enabled after the event fired
        SoundManager.ForceSyncUI();
    }

    private void OnDisable()
    {
        SoundManager.OnPlayStateChanged -= HandleStateChanged;
    }

    private void OnClick()
    {
        SoundManager.ToggleMusic();
    }

    private void OnToggleChanged(bool value)
    {
        if (_suppressToggleEvent) return;
        // Make toggle reflect the global state; if user changed it, toggle sound if needed
        bool currentlyPlaying = SoundManager.IsMusicPlaying();
        if (value != currentlyPlaying)
        {
            SoundManager.ToggleMusic();
        }
    }

    private void HandleStateChanged(bool playing)
    {
        if (label != null)
        {
            label.text = playing ? "Pause" : "Play";
        }
        if (playIcon != null) playIcon.SetActive(!playing);
        if (pauseIcon != null) pauseIcon.SetActive(playing);

        if (_toggle != null)
        {
            _suppressToggleEvent = true;
            _toggle.isOn = playing; // Info: Toggle on == playing
            _suppressToggleEvent = false;
        }
    }

    private void TryDisableBlockingRaycasts()
    {
        if (label != null) label.raycastTarget = false;

        if (playIcon != null)
        {
            var graphics = playIcon.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics) g.raycastTarget = false;
        }
        if (pauseIcon != null)
        {
            var graphics = pauseIcon.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics) g.raycastTarget = false;
        }
    }
}
