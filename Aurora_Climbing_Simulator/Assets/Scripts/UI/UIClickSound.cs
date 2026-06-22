using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class UIClickSound : MonoBehaviour
{
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayClick()
    {
        if (_audioSource != null && _audioSource.clip != null)
            _audioSource.Play();
    }
}
