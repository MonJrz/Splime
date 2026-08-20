using UnityEngine;

public class LevelMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip levelMusic;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    private void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(levelMusic, volume);
        }
        else
        {   
            Debug.LogWarning($"[{nameof(LevelMusicPlayer)}] AudioManager.Instance es null — ¿se entró a la escena sin pasar por Main?", this);
        }
    }
}