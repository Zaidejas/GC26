using UnityEngine;

namespace GC26.Spinv
{
    // Звуки интерфейса: щелчок кнопки и звук печати текста. Работают и в меню, и в игре.
    // Висит рядом с фоновым звуком на корневом объекте игры, его не выключают
    [RequireComponent(typeof(AudioSource))]
    public class SpinvUiSound : MonoBehaviour
    {
        static SpinvUiSound instance;

        public AudioClip click;
        [Range(0f, 1f)] public float volume = 0.7f;

        [Tooltip("Получение достижения (плашка): звучит и в меню, и в игре")]
        public AudioClip achievement;
        [Range(0f, 1f)] public float achievementVolume = 0.7f;

        [Header("Печать текста")]
        [Tooltip("Длинный звук «бегущего текста»: играет по кругу, пока печатаются буквы")]
        public AudioClip typing;
        [Range(0f, 1f)] public float typingVolume = 1f;
        [Tooltip("Скорость воспроизведения: 0.8 - на 20% медленнее")]
        public float typingPitch = 0.8f;
        [Tooltip("Если новых букв нет дольше этого (секунд) - звук печати смолкает")]
        public float typingTimeout = 0.2f;

        [Header("Музыка победы")]
        [Tooltip("Играет по кругу на экране победы вместо фона, до выключения автомата")]
        public AudioClip victoryMusic;
        [Range(0f, 1f)] public float musicVolume = 0.6f;

        [Header("Сцена самозванца")]
        [Tooltip("Шипение на время жёсткой тряски экрана (тот же шум, что на включении)")]
        public AudioClip glitchNoise;
        [Range(0f, 1f)] public float glitchNoiseVolume = 0.6f;
        [Tooltip("Корабль рассыпается в пыль")]
        public AudioClip shipDust;
        [Range(0f, 1f)] public float shipDustVolume = 0.6f;

        AudioSource source;
        AudioSource typingSource;
        AudioSource musicSource;
        AudioSource noiseSource;
        float lastTypeTick = -1f;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true; // пауза глушит игру, но кнопки паузы должны щёлкать

            typingSource = gameObject.AddComponent<AudioSource>();
            typingSource.playOnAwake = false;
            typingSource.loop = true;
            typingSource.clip = typing;
            typingSource.volume = typingVolume;
            typingSource.pitch = typingPitch;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.ignoreListenerPause = true;
            musicSource.clip = victoryMusic;
            musicSource.volume = musicVolume;

            noiseSource = gameObject.AddComponent<AudioSource>();
            noiseSource.playOnAwake = false;
            noiseSource.loop = true;
            noiseSource.ignoreListenerPause = true;
            noiseSource.clip = glitchNoise;
            noiseSource.volume = glitchNoiseVolume;
        }

        public static void GlitchNoise(bool on)
        {
            if (instance == null || instance.glitchNoise == null) return;
            if (on && !instance.noiseSource.isPlaying) instance.noiseSource.Play();
            else if (!on) instance.noiseSource.Stop();
        }

        public static void ShipDust()
        {
            if (instance != null && instance.shipDust != null)
                instance.source.PlayOneShot(instance.shipDust, instance.shipDustVolume * SpinvAudioSettings.SfxVolume);
        }

        public static void PlayVictoryMusic()
        {
            if (instance == null || instance.victoryMusic == null) return;
            if (!instance.musicSource.isPlaying) instance.musicSource.Play();
        }

        public static void StopMusic()
        {
            if (instance != null) instance.musicSource.Stop();
        }

        void OnEnable() => instance = this;

        void OnDisable()
        {
            if (instance == this) instance = null;
            if (typingSource != null) typingSource.Stop();
            if (musicSource != null) musicSource.Stop();
            if (noiseSource != null) noiseSource.Stop();
        }

        void Update()
        {
            // Длинные звуки идут на ползунок SFX
            float sfx = SpinvAudioSettings.SfxVolume;
            typingSource.volume = typingVolume * sfx;
            musicSource.volume = musicVolume * sfx;
            noiseSource.volume = glitchNoiseVolume * sfx;

            // Печать оборвалась (текст допечатан, окно закрыли, корутину остановили) - звук смолкает сам
            if (typingSource.isPlaying && Time.unscaledTime - lastTypeTick > typingTimeout)
                typingSource.Stop();
        }

        public static void Achievement()
        {
            if (instance != null && instance.achievement != null)
                instance.source.PlayOneShot(instance.achievement, instance.achievementVolume * SpinvAudioSettings.SfxVolume);
        }

        public static void Click()
        {
            if (instance != null && instance.click != null)
                instance.source.PlayOneShot(instance.click, instance.volume * SpinvAudioSettings.SfxVolume);
        }

        // Вызывается на каждую напечатанную букву: держит звук печати включённым.
        // На пробеле звук смолкает - каждое слово звучит отдельной короткой очередью с начала файла.
        // continuous: пробелы звук не рвут (подсказки обучения печатаются быстро,
        // и слова по 2-3 буквы иначе превращаются в треск)
        public static void TypeTick(char typed, bool continuous = false)
        {
            if (instance == null || instance.typing == null) return;
            float now = Time.unscaledTime;
            if (char.IsWhiteSpace(typed) && !continuous)
            {
                instance.typingSource.Stop();
                instance.lastTypeTick = now;
                return;
            }
            instance.lastTypeTick = now;
            if (!instance.typingSource.isPlaying) instance.typingSource.Play();
        }
    }
}
