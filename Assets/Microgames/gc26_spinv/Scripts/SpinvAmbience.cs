using UnityEngine;

namespace GC26.Spinv
{
    // Фоновый звук: играет всё время - и в меню, и в игре (висит на корневом объекте игры, его не выключают).
    // Луп не на весь файл: кусок 0..loopEnd повторяется бесконечно.
    // Без щелчка и паузы на стыке: два источника по очереди, следующий заранее ставится в очередь
    // по часам звуковой системы (PlayScheduled - точно до сэмпла, а не по кадрам), и на стыке
    // короткий перекрёстный переход. Громкость при включении автомата плавно растёт с quietVolume
    // до volume, при выключении - падает обратно
    [RequireComponent(typeof(AudioSource))]
    public class SpinvAmbience : MonoBehaviour
    {
        static SpinvAmbience current;

        [Range(0f, 1f)] public float volume = 0.27f;
        [Tooltip("Громкость в начале включения и в конце выключения автомата")]
        [Range(0f, 1f)] public float quietVolume = 0.054f;
        [Tooltip("На какой секунде звук начинается сначала. 0 - весь файл")]
        public float loopEnd = 19f;
        [Tooltip("Длина перекрёстного перехода на стыке, секунд")]
        public float crossfade = 0.25f;

        const double ScheduleAhead = 1.0; // за сколько секунд до стыка ставим следующий кусок в очередь

        AudioSource[] sources;
        int playing;            // какой источник сейчас главный
        double segmentStart;    // когда (по часам звука) начался его кусок
        double nextStart = -1;  // когда начнётся следующий кусок; -1 - ещё не поставлен
        float level;            // общая громкость с учётом включения/выключения
        float target;
        float fadeSpeed;        // громкости в секунду, 0 - без плавного перехода

        float SegmentLength
        {
            get
            {
                AudioClip clip = sources[0].clip;
                float length = clip != null ? clip.length : 0f;
                return loopEnd > 0f && loopEnd < length ? loopEnd : length;
            }
        }

        void Awake()
        {
            var first = GetComponent<AudioSource>();
            var second = gameObject.AddComponent<AudioSource>();
            second.clip = first.clip;
            sources = new[] { first, second };
            foreach (AudioSource s in sources)
            {
                s.loop = false;
                s.playOnAwake = false;
                s.ignoreListenerPause = true; // на паузе фон играет дальше
                s.volume = 0f;
            }
        }

        void OnEnable()
        {
            current = this;
            level = target = volume;
            fadeSpeed = 0f;
            if (sources[0].clip == null) return;

            playing = 0;
            segmentStart = AudioSettings.dspTime + 0.05;
            nextStart = -1;
            sources[0].time = 0f;
            sources[0].PlayScheduled(segmentStart);
        }

        void OnDisable()
        {
            if (current == this) current = null;
            foreach (AudioSource s in sources) s.Stop();
        }

        void Update()
        {
            // Реальное время: выключение автомата идёт и на паузе
            if (fadeSpeed > 0f)
                level = Mathf.MoveTowards(level, target, fadeSpeed * Time.unscaledDeltaTime);

            if (sources[0].clip == null) return;
            double now = AudioSettings.dspTime;
            double length = SegmentLength;
            double fade = Mathf.Clamp(crossfade, 0f, (float)length * 0.5f);

            // Заранее ставим следующий кусок: он стартует за fade до конца текущего, текущий обрывается ровно в конце
            if (nextStart < 0 && now >= segmentStart + length - fade - ScheduleAhead)
            {
                int other = 1 - playing;
                nextStart = segmentStart + length - fade;
                sources[other].Stop();
                sources[other].time = 0f;
                sources[other].PlayScheduled(nextStart);
                sources[playing].SetScheduledEndTime(segmentStart + length);
            }

            // Следующий кусок начался - он становится главным
            if (nextStart >= 0 && now >= nextStart + fade)
            {
                playing = 1 - playing;
                segmentStart = nextStart;
                nextStart = -1;
            }

            // Громкости: на стыке старый плавно уходит, новый приходит (равная мощность - без провала громкости)
            float newGain = 0f;
            if (nextStart >= 0 && now >= nextStart)
                newGain = fade > 0 ? Mathf.Clamp01((float)((now - nextStart) / fade)) : 1f;
            sources[playing].volume = level * Mathf.Cos(newGain * Mathf.PI * 0.5f);
            sources[1 - playing].volume = level * Mathf.Sin(newGain * Mathf.PI * 0.5f);
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
