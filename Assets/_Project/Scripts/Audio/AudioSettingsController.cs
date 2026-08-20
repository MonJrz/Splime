using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsController : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        float musicDb, sfxDb;
        mixer.GetFloat("MusicVolume", out musicDb);
        mixer.GetFloat("SFXVolume", out sfxDb);

        musicSlider.SetValueWithoutNotify(Mathf.Pow(10, musicDb / 20));
        sfxSlider.SetValueWithoutNotify(Mathf.Pow(10, sfxDb / 20));

        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMusicVolume(float value)
    {
        mixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
    }

    public void SetSFXVolume(float value)
    {
        mixer.SetFloat("SFXVolume", Mathf.Log10(value) * 20);
    }
}