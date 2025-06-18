using UnityEngine;
using UnityEngine.Audio;

public class MusicVollumeMixer : MonoBehaviour
{
    public AudioMixer audioMixer;
    public string parameter;

    public void OnSliderChange(float input)
    {
        audioMixer.SetFloat(parameter, input);
    }
}
