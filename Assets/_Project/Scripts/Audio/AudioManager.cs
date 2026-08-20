using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    [Header("Level Stinger Clips")]
    [SerializeField] private AudioClip levelStartClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip defeatClip;

    [Header("Level Music Settings")]
    [SerializeField, Range(0f, 1f)] private float levelMusicVolume = 0.7f;

   private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PlayHover()
    {
        sfxSource.PlayOneShot(hoverClip);
    }

    public void PlayClick()
    {
        sfxSource.PlayOneShot(clickClip);
    }

    public void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (clip == null || musicSource == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return; // ya está sonando, no reiniciar

        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayLevelStart() => sfxSource.PlayOneShot(levelStartClip);
    public void PlayVictory() => sfxSource.PlayOneShot(victoryClip);
    public void PlayDefeat() => sfxSource.PlayOneShot(defeatClip);
}