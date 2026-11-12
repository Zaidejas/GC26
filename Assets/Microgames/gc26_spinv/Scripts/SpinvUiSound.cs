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

        AudioSource source;
        AudioSource typingSource;
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
        }

        void OnEnable() => instance = this;

        void OnDisable()
        {
            if (instance == this) instance = null;
            if (typingSource != null) typingSource.Stop();
        }

        void Update()
        {
            // Печать оборвалась (текст допечатан, окно закрыли, корутину остановили) - звук смолкает сам
            if (typingSource.isPlaying && Time.unscaledTime - lastTypeTick > typingTimeout)
                typingSource.Stop();
        }

        public static void Achievement()
        {
            if (instance != null && instance.achievement != null)
                instance.source.PlayOneShot(instance.achievement, instance.achievementVolume);
        }

        public static void Click()
        {
            if (instance != null && instance.click != null)
                instance.source.PlayOneShot(instance.click, instance.volume);
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
