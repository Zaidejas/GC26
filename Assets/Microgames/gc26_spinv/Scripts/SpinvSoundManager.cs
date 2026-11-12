using UnityEngine;

namespace GC26.Spinv
{
    public enum Sfx { Shoot, Explosion, Coin, PlayerHit, Heal, InvaderDeath, BombExplosion, Laser, Ability }

    // Простые звуковые эффекты. Если клип не задан - просто тишина
    [RequireComponent(typeof(AudioSource))]
    public class SpinvSoundManager : MonoBehaviour
    {
        public static SpinvSoundManager Instance { get; private set; }

        public AudioClip shoot;
        public AudioClip explosion;
        public AudioClip coin;
        public AudioClip playerHit;
        [Tooltip("Подбор сердечка")]
        public AudioClip heal;
        [Tooltip("Взрыв сбитого пришельца")]
        public AudioClip invaderDeath;
        [Tooltip("Взрыв мин и гроздей Bomber-а")]
        public AudioClip bombExplosion;
        [Tooltip("Залп лазера Босса 2")]
        public AudioClip laser;
        [Tooltip("Нажатие способности")]
        public AudioClip ability;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Звук монетки относительно общей громкости")]
        [Range(0f, 1f)] public float coinVolume = 0.5f;
        [Tooltip("Звук смерти пришельца относительно общей громкости")]
        [Range(0f, 1f)] public float invaderDeathVolume = 0.85f;
        [Tooltip("Нажатие способности относительно общей громкости (1.2 - на 20% громче)")]
        [Range(0f, 1.6f)] public float abilityVolume = 1.2f;

        AudioSource source;
        float coinBusyUntil; // до какого момента (реальное время) ещё звучит прошлая монетка

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
                // Новые звуки без своего клипа звучат как старые
                Sfx.Heal => Instance.heal != null ? Instance.heal : Instance.coin,
                Sfx.InvaderDeath => Instance.invaderDeath != null ? Instance.invaderDeath : Instance.explosion,
                Sfx.BombExplosion => Instance.bombExplosion != null ? Instance.bombExplosion : Instance.explosion,
                Sfx.Laser => Instance.laser != null ? Instance.laser : Instance.explosion,
                Sfx.Ability => Instance.ability,
                _ => null
            };

            float volume = Instance.volume * sfx switch
            {
                Sfx.Coin => Instance.coinVolume,
                Sfx.InvaderDeath => Instance.invaderDeathVolume,
                Sfx.Ability => Instance.abilityVolume,
                _ => 1f
            };
            if (clip == null) return;

            // Монетки не наслаиваются: пока звучит прошлая, новая подобранная монетка молчит
            if (sfx == Sfx.Coin)
            {
                if (Time.unscaledTime < Instance.coinBusyUntil) return;
                Instance.coinBusyUntil = Time.unscaledTime + clip.length;
            }
            Instance.source.PlayOneShot(clip, volume);
        }
    }
}
