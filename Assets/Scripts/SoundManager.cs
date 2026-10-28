using UnityEngine;

public enum Sfx { Shoot, Explosion, Coin, PlayerHit }

// Простые звуковые эффекты. Если клип не задан - просто тишина
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public AudioClip shoot;
    public AudioClip explosion;
    public AudioClip coin;
    public AudioClip playerHit;
    [Range(0f, 1f)] public float volume = 0.6f;

    AudioSource source;

    void Awake()
    {
        Instance = this;
        source = GetComponent<AudioSource>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Play(Sfx sfx)
    {
        if (Instance == null) return;

        AudioClip clip = sfx switch
        {
            Sfx.Shoot => Instance.shoot,
            Sfx.Explosion => Instance.explosion,
            Sfx.Coin => Instance.coin,
            Sfx.PlayerHit => Instance.playerHit,
            _ => null
        };

        if (clip != null)
            Instance.source.PlayOneShot(clip, Instance.volume);
    }
}
