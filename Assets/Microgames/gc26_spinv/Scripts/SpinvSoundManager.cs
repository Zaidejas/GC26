using UnityEngine;

namespace GC26.Spinv
{
    public enum Sfx { Shoot, Explosion, Coin, PlayerHit, Heal, InvaderDeath, BombExplosion, Laser, Ability, ClockTick, EnemyShot, BossHit, MineBeep }

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
        [Tooltip("Щелчок крупных часов: каждый рывок диска в сценках")]
        public AudioClip clockTick;
        [Tooltip("Выстрел пришельцев, Босса 1 и Босса 2")]
        public AudioClip enemyShot;
        [Tooltip("Попадание по боссу")]
        public AudioClip bossHit;
        [Tooltip("Писк мины Bomber-а на каждое загорание огонька")]
        public AudioClip mineBeep;
        [Tooltip("Громкость попадания по боссу - сама по себе, не от общей громкости")]
        [Range(0f, 1f)] public float bossHitVolume = 0.25f;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Звук монетки относительно общей громкости")]
        [Range(0f, 1f)] public float coinVolume = 0.5f;
        [Tooltip("Звук смерти пришельца относительно общей громкости")]
        [Range(0f, 1f)] public float invaderDeathVolume = 0.85f;
        [Tooltip("Нажатие способности относительно общей громкости (1.2 - на 20% громче)")]
        [Range(0f, 1.6f)] public float abilityVolume = 1.2f;

        AudioSource source;
        [Tooltip("Минимум секунд между звуками монетки: монетка, подобранная раньше, звука не даёт")]
        public float coinMinInterval = 0.2f;
        int enemyShotFrame = -1;
        float mineBeepUntil;
        float coinBusyUntil; // до какого момента (реальное время) новая монетка молчит

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
                Sfx.ClockTick => Instance.clockTick,
                Sfx.EnemyShot => Instance.enemyShot,
                Sfx.BossHit => Instance.bossHit,
                Sfx.MineBeep => Instance.mineBeep,
                _ => null
            };

            float volume = Instance.volume * sfx switch
            {
                Sfx.Coin => Instance.coinVolume,
                Sfx.InvaderDeath => Instance.invaderDeathVolume,
                Sfx.Ability => Instance.abilityVolume,
                _ => 1f
            };
            if (sfx == Sfx.BossHit) volume = Instance.bossHitVolume;
            if (clip == null) return;

            // Залп (несколько пуль в один кадр) звучит одним выстрелом; очередь - каждой пулей
            if (sfx == Sfx.EnemyShot)
            {
                if (Instance.enemyShotFrame == Time.frameCount) return;
                Instance.enemyShotFrame = Time.frameCount;
            }
            // Писки мин не накладываются: пока звучит прошлый, новый молчит.
            // Огонёк к концу мигает 16 раз в секунду, а писк длиннее - наложенные писки складывались и трещали
            if (sfx == Sfx.MineBeep)
            {
                if (Time.time < Instance.mineBeepUntil) return;
                Instance.mineBeepUntil = Time.time + clip.length;
            }

            // Монетки подряд не трещат: если с прошлого звука монетки прошло меньше coinMinInterval, новая молчит
            if (sfx == Sfx.Coin)
            {
                if (Time.unscaledTime < Instance.coinBusyUntil) return;
                Instance.coinBusyUntil = Time.unscaledTime + Instance.coinMinInterval;
            }
            // Ползунки настроек: игровые звуки - SOUND, остальное - SFX
            volume *= SpinvAudioSettings.IsSound(sfx) ? SpinvAudioSettings.SoundVolume : SpinvAudioSettings.SfxVolume;
            Instance.source.PlayOneShot(clip, volume);
        }
    }
}
