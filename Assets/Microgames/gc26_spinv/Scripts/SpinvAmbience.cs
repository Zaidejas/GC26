using UnityEngine;

namespace GC26.Spinv
{
    // Фоновый звук: играет всё время - и в меню, и в игре (висит на корневом объекте игры, его не выключают).
    // Из файла при запуске собирается готовая петля: кусок loopStart..loopEnd, хвост которого плавно
    // перетекает в его же начало (перекрёстный переход вшит в сами сэмплы). Её играет ОДИН источник
    // со штатным loop - Unity повторяет её точно до сэмпла: без пауз, щелчков и провалов, и без
    // отложенных запусков, которые ломались на паузе. Сам mp3 не меняется.
    // Громкость при включении автомата плавно растёт с quietVolume до volume, при выключении - падает обратно
    [RequireComponent(typeof(AudioSource))]
    public class SpinvAmbience : MonoBehaviour
    {
        static SpinvAmbience current;

        [Range(0f, 1f)] public float volume = 0.27f;
        [Tooltip("Громкость в начале включения и в конце выключения автомата")]
        [Range(0f, 1f)] public float quietVolume = 0.054f;
        [Tooltip("До какой секунды файла идёт петля. 0 - до конца")]
        public float loopEnd = 19f;
        [Tooltip("С какой секунды начинается петля. В начале файла звук нарастает из тишины - его пропускаем")]
        public float loopStart = 1f;
        [Tooltip("Длина перекрёстного перехода на стыке, секунд")]
        public float crossfade = 0.25f;

        AudioSource source;
        AudioClip loopClip;
        bool stopped;
        float level;      // общая громкость с учётом включения/выключения
        float target;
        float fadeSpeed;  // громкости в секунду, 0 - без плавного перехода

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.ignoreListenerPause = true; // на паузе фон играет дальше
            source.priority = 0;               // фон не вытесняется, когда разом звучит много эффектов
            loopClip = BuildLoop(source.clip);
            if (loopClip != null) source.clip = loopClip;
        }

        void OnDestroy()
        {
            if (loopClip != null) Destroy(loopClip);
        }

        // Петля из куска файла: тело loopStart+fade..loopEnd, последние fade секунд плавно смешаны
        // с началом куска (loopStart..loopStart+fade) - конец петли ровно переходит в её начало
        AudioClip BuildLoop(AudioClip clip)
        {
            if (clip == null) return null;
            if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData()) return null;

            int ch = clip.channels, rate = clip.frequency, total = clip.samples;
            var data = new float[total * ch];
            if (!clip.GetData(data, 0)) return null; // файл не распакован в память - играем как есть

            int end = loopEnd > 0f ? Mathf.Min(total, Mathf.RoundToInt(loopEnd * rate)) : total;
            int start = Mathf.Clamp(Mathf.RoundToInt(loopStart * rate), 0, end / 2);
            int fade = Mathf.Clamp(Mathf.RoundToInt(crossfade * rate), 0, (end - start) / 3);
            int length = end - start - fade;
            if (length <= fade || length <= 0) return null;

            var loop = new float[length * ch];
            for (int i = 0; i < length; i++)
            {
                int src = start + fade + i;
                int tailIndex = i - (length - fade); // >= 0 - мы в хвосте, который перетекает в начало
                for (int c = 0; c < ch; c++)
                {
                    float v = data[src * ch + c];
                    if (tailIndex >= 0 && fade > 0)
                    {
                        float t = (tailIndex + 0.5f) / fade;
                        float head = data[(start + tailIndex) * ch + c];
                        v = v * Mathf.Cos(t * Mathf.PI * 0.5f) + head * Mathf.Sin(t * Mathf.PI * 0.5f);
                    }
                    loop[i * ch + c] = v;
                }
            }
            AudioClip result = AudioClip.Create(clip.name + "_loop", length, ch, rate, false);
            result.SetData(loop, 0);
            return result;
        }

        void OnEnable()
        {
            current = this;
            level = target = volume;
            fadeSpeed = 0f;
            StartLoop();
        }

        void OnDisable()
        {
            if (current == this) current = null;
            source.Stop();
        }

        void StartLoop()
        {
            stopped = false;
            source.volume = level * SpinvAudioSettings.SfxVolume;
            if (source.clip != null && !source.isPlaying) source.Play();
        }

        // Победа в классике: фон смолкает, вместо него играет музыка победы
        public static void Stop()
        {
            if (current == null || current.stopped) return;
            current.stopped = true;
            current.source.Stop();
        }

        // Фон снова играет (выключение автомата после победы, выход из партии)
        public static void Resume()
        {
            if (current == null || !current.stopped) return;
            current.StartLoop();
        }

        void Update()
        {
            // Реальное время: выключение автомата идёт и на паузе
            if (fadeSpeed > 0f)
                level = Mathf.MoveTowards(level, target, fadeSpeed * Time.unscaledDeltaTime);
            source.volume = level * SpinvAudioSettings.SfxVolume;

            // Страховка: фон не должен пропасть насовсем
            if (!stopped && source.clip != null && !source.isPlaying) source.Play();
        }

        // Включение автомата: с тихого до обычной громкости за seconds
        public static void FadeIn(float seconds) => current?.Fade(current.quietVolume, current.volume, seconds);

        // Выключение автомата: с текущей громкости до тихой за seconds
        public static void FadeOut(float seconds) => current?.Fade(current.level, current.quietVolume, seconds);

        void Fade(float from, float to, float seconds)
        {
            level = from;
            target = to;
            fadeSpeed = Mathf.Abs(to - from) / Mathf.Max(0.01f, seconds);
        }
    }
}
