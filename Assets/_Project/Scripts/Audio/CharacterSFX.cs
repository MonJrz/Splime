using UnityEngine;

public class CharacterSFX : MonoBehaviour
{
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip interactClip;
    
    [Header("Ability Transition Clips")]
    [SerializeField] private AudioClip metalFormOnClip;
    [SerializeField] private AudioClip metalFormOffClip;
    [SerializeField] private AudioClip squeezeOnClip;
    [SerializeField] private AudioClip squeezeOffClip;

    public void PlayWalk()
    {
        sfxSource.PlayOneShot(walkClip);
    }

    public void PlayJump()
    {
        sfxSource.PlayOneShot(jumpClip);
    }

    public void PlayInteract()
    {
        sfxSource.PlayOneShot(interactClip);
    }

    public void PlayMetalFormOn()
    {
        sfxSource.PlayOneShot(metalFormOnClip);
    }

    public void PlayMetalFormOff()
    {
        sfxSource.PlayOneShot(metalFormOffClip);
    }

    public void PlaySqueezeOn()
    {
        sfxSource.PlayOneShot(squeezeOnClip);
    }

    public void PlaySqueezeOff()
    {
        sfxSource.PlayOneShot(squeezeOffClip);
    }
}