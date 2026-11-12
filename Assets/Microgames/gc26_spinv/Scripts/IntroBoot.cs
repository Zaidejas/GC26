using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Включение аркадного автомата при запуске игры: помехи, строки самопроверки и TAP TO START.
    // Меню открывается только после касания. Показывается один раз за загрузку сцены (вход в игру);
    // после выхода из партии в меню не повторяется: объект заставки к тому времени уже выключен
    public class IntroBoot : MonoBehaviour
    {
        [Tooltip("1 - как в предпросмотре на 100%, 0.5 - вдвое медленнее")]
        public float speed = 0.5f;
        public CanvasGroup group;
        public Image background;
        public Image[] bars;
        public RawImage noise;
        [Tooltip("Строки самопроверки, последняя - TAP TO START")]
        public TMP_Text[] lines;

        public Color blackColor = Color.black;
        public Color screenColor = new Color(0.012f, 0.063f, 0.024f);
        public Color barColor = new Color(0.22f, 1f, 0.42f);

        // Время этапов на скорости 1, в секундах
        const float StaticStart = 0.05f;
        const float BootStart = 0.55f;
        const float BootEnd = 1.9f;
        const float FadeTime = 0.35f;
        const float ScreenHeight = 2778f;

        enum Stage { Static, Boot, WaitTap, Fade }

        Stage stage = Stage.Static;
        float time;
        float fadeTime;
        bool wasPressed;
        Texture2D noiseTexture;
        Color32[] noisePixels;

        void Awake()
        {
            noiseTexture = new Texture2D(160, 346, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            noisePixels = new Color32[noiseTexture.width * noiseTexture.height];
            noise.texture = noiseTexture;
            ShowLines(0, false);
        }

        // Фон нарастает с тихой до обычной громкости, пока автомат включается (помехи и самопроверка).
        // В Start: фоновый звук к этому времени уже включился (его OnEnable идёт раньше любого Start)
        void Start()
        {
            SpinvAmbience.FadeIn(ambienceFadeIn);
        }

        [Tooltip("За сколько секунд фон нарастает с тихого до обычного при включении (сама загрузка ~3.8 с)")]
        public float ambienceFadeIn = 8f;

        void OnDestroy()
        {
            if (noiseTexture != null) Destroy(noiseTexture);
        }

        void OnDisable()
        {
            CrtBoost.Set(0f);
            SetNoiseSound(false);
        }

        [Tooltip("Щелчок лампочки на появление каждой строки самопроверки")]
        public AudioClip lineClick;
        [Range(0f, 1f)] public float lineClickVolume = 0.05f;
        AudioSource clickSource;
        int shownLines;

        void PlayLineClick()
        {
            if (lineClick == null) return;
            if (clickSource == null)
            {
                clickSource = gameObject.AddComponent<AudioSource>();
                clickSource.playOnAwake = false;
            }
            clickSource.PlayOneShot(lineClick, lineClickVolume * SpinvAudioSettings.SfxVolume);
        }

        [Tooltip("Шипение на время помех при включении")]
        public AudioClip noiseSound;
        [Range(0f, 1f)] public float noiseVolume = 0.3f;
        AudioSource noiseSource;

        void SetNoiseSound(bool on)
        {
            if (noiseSound == null) return;
            if (noiseSource == null)
            {
                if (!on) return;
                noiseSource = gameObject.AddComponent<AudioSource>();
                noiseSource.clip = noiseSound;
                noiseSource.loop = true;
                noiseSource.playOnAwake = false;
            }
            noiseSource.volume = noiseVolume * SpinvAudioSettings.SfxVolume;
            if (on && !noiseSource.isPlaying) noiseSource.Play();
            else if (!on && noiseSource.isPlaying) noiseSource.Stop();
        }

        void Update()
        {
            // Не больше 1/30 с за кадр: первый кадр после загрузки (в редакторе - перезагрузка скриптов
            // при входе в Play) бывает длиной в секунды, и заставка иначе сразу проскочила бы к TAP TO START
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) * speed;
            time += dt;
            bool tap = TapPressed();
            CrtBoost.Set(stage == Stage.Fade ? group.alpha : 1f);

            if (stage == Stage.Fade)
            {
                fadeTime += dt;
                group.alpha = 1f - Mathf.Clamp01(fadeTime / FadeTime);
                if (fadeTime >= FadeTime) gameObject.SetActive(false);
                return;
            }

            if (tap)
            {
                // Касание во время загрузки досматривает её до конца, касание на TAP TO START - запускает меню
                if (stage == Stage.WaitTap)
                {
                    SpinvUiSound.Click(); // TAP TO START щёлкает, как кнопка
                    // Надписи пропадают сразу, плавно уходит только тёмный экран
                    ShowLines(0, false);
                    stage = Stage.Fade;
                    return;
                }
                stage = Stage.WaitTap;
                time = BootEnd;
            }

            // Шипение звучит ровно пока на экране помехи
            SetNoiseSound(stage == Stage.Static && time >= StaticStart && time < BootStart);

            if (time < StaticStart) DrawBlack();
            else if (time < BootStart && stage == Stage.Static) DrawStatic((time - StaticStart) / (BootStart - StaticStart));
            else if (time < BootEnd && stage != Stage.WaitTap) DrawBoot((time - BootStart) / (BootEnd - BootStart));
            else
            {
                stage = Stage.WaitTap;
                DrawPrompt();
            }
        }

        bool TapPressed()
        {
            bool pressed = GameInput.PointerHeld(out _);
            bool tap = pressed && !wasPressed;
            wasPressed = pressed;
            return tap;
        }

        void DrawBlack()
        {
            background.color = blackColor;
            SetBars(false);
            noise.enabled = false;
        }

        // Полосы помех и шум, который постепенно гаснет
        void DrawStatic(float p)
        {
            background.color = blackColor;
            SetBars(true);
            for (int i = 0; i < bars.Length; i++)
            {
                var rect = bars[i].rectTransform;
                float y = (i * 137f + time * 900f) % 650f / 650f * ScreenHeight;
                rect.anchoredPosition = new Vector2(0f, -y);
                rect.sizeDelta = new Vector2(0f, (8f + i * 3f) / 650f * ScreenHeight);
                Color c = barColor;
                c.a = 0.25f + 0.25f * Mathf.Sin(i);
                bars[i].color = c;
            }

            noise.enabled = true;
            byte alpha = (byte)(255 * 0.5f * (1f - p));
            for (int i = 0; i < noisePixels.Length; i++)
                noisePixels[i] = Random.value < 0.08f ? new Color32(200, 255, 214, alpha) : new Color32(0, 0, 0, 0);
            noiseTexture.SetPixels32(noisePixels);
            noiseTexture.Apply();
        }

        // Строки самопроверки появляются по одной
        void DrawBoot(float p)
        {
            stage = Stage.Boot;
            background.color = screenColor;
            SetBars(false);
            noise.enabled = false;
            ShowLines(Mathf.FloorToInt(p * lines.Length * 1.2f), true);
        }

        void DrawPrompt()
        {
            background.color = screenColor;
            SetBars(false);
            noise.enabled = false;
            ShowLines(lines.Length, Mathf.FloorToInt(time * 4f) % 2 == 1);
        }

        // Последняя строка (TAP TO START) мигает
        void ShowLines(int count, bool promptVisible)
        {
            // Новая строка самопроверки появилась - щёлкает «лампочка»
            count = Mathf.Min(count, lines.Length);
            if (count > shownLines)
            {
                PlayLineClick();
                shownLines = count;
            }
            for (int i = 0; i < lines.Length; i++)
            {
                bool isPrompt = i == lines.Length - 1;
                lines[i].gameObject.SetActive(i < count && (!isPrompt || promptVisible));
            }
        }

        void SetBars(bool on)
        {
            foreach (Image bar in bars) bar.enabled = on;
        }
    }
}
