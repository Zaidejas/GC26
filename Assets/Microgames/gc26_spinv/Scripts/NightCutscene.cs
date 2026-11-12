using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Сценка перед последней волной: экран темнеет, крупные часы рывками докручиваются до ночи,
    // долгое моргание (часы сменяются окном), печатается «???: It's time to lie down.»,
    // по касанию - второе долгое моргание, за закрытыми веками начинается бой с финальным боссом.
    // Запускает WaveManager. Время игровое: на паузе сценка стоит
    public class NightCutscene : MonoBehaviour, IPointerClickHandler
    {
        public CanvasGroup group;
        public DayClock bigClock;
        [Tooltip("Часы в HUD: после сценки тоже показывают ночь")]
        public DayClock hudClock;
        public BlinkEffect blink;
        [Tooltip("Пока идёт сценка, игрок не стреляет")]
        public PlayerController player;

        [Header("Окно")]
        public GameObject dialog;
        public TMP_Text line;
        public TMP_Text tapHint;
        public string speaker = "???:";
        public string message = "It's time to lie down.";
        public string speakerColor = "#48D46C";

        [Header("Время")]
        public float fadeTime = 0.6f;
        public float nightAngle = 180f;
        public int ticks = 6;
        public float blinkLength = 1.2f;
        public float speakerCharTime = 0.11f;
        public float messageCharTime = 0.095f;

        bool tapped;

        public void OnPointerClick(PointerEventData eventData)
        {
            tapped = true;
        }

        public IEnumerator Play(System.Action startBossWave)
        {
            gameObject.SetActive(true);
            if (player != null) player.canShoot = false;
            group.alpha = 0f;
            bigClock.gameObject.SetActive(true);
            dialog.SetActive(false);
            line.text = "";
            tapHint.text = "";
            float start = hudClock != null ? hudClock.Angle : 60f;
            bigClock.SetAngle(start);

            yield return Fade(0f, 1f); // экран темнеет, часы проявляются
            yield return new WaitForSeconds(0.4f);
            yield return TickTo(nightAngle, ticks);
            yield return new WaitForSeconds(0.5f);

            // Долгое моргание: пока глаза закрыты, часы сменяются окном
            yield return blink.LongBlink(blinkLength, () =>
            {
                bigClock.gameObject.SetActive(false);
                dialog.SetActive(true);
            });

            yield return TypeLine();

            // Ждём касания (или Space/Enter), мигает TAP
            tapped = false;
            while (!tapped && !KeyPressed())
            {
                tapHint.text = Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.35f ? "TAP" : "";
                yield return null;
            }
            SpinvUiSound.Click(); // TAP - щелчок, как у кнопки
            tapHint.text = "";

            // Второе долгое моргание: за закрытыми веками сценка исчезает и начинается бой
            yield return blink.LongBlink(blinkLength, () =>
            {
                group.alpha = 0f;
                gameObject.SetActive(false);
                startBossWave?.Invoke();
            });

            // Глаза открылись - бой начался, можно стрелять
            if (player != null) player.canShoot = true;
        }

        // Между обычными волнами: та же первая часть сценки - экран темнеет, крупные часы рывками
        // докручиваются до времени следующей волны, - потом всё гаснет обратно. Без моргания и реплики
        public IEnumerator PlayClockTurn(float target)
        {
            gameObject.SetActive(true);
            if (player != null) player.canShoot = false;
            group.alpha = 0f;
            // Касаний здесь не ждём: затемнение не должно перехватывать палец, которым ведут корабль
            group.blocksRaycasts = false;
            bigClock.gameObject.SetActive(true);
            dialog.SetActive(false);
            line.text = "";
            tapHint.text = "";
            float start = hudClock != null ? hudClock.Angle : target;
            bigClock.SetAngle(start);

            yield return Fade(0f, 1f);
            yield return new WaitForSeconds(0.4f);
            // Рывков столько же на градус, сколько в ночной сценке (~20° за рывок), но не меньше двух
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(target - start) / 20f), 2, ticks);
            yield return TickTo(target, count);
            yield return new WaitForSeconds(0.5f);
            yield return Fade(1f, 0f);

            gameObject.SetActive(false);
            group.blocksRaycasts = true; // ночной сценке касание нужно (TAP)
            if (player != null) player.canShoot = true;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = to;
        }

        // Диск рывками докручивается до цели: каждый рывок чуть перелетает и возвращается.
        // Часы в HUD в конце показывают то же время
        IEnumerator TickTo(float target, int count)
        {
            float step = (target - bigClock.Angle) / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                float from = bigClock.Angle, to = from + step;
                SpinvSoundManager.Play(Sfx.ClockTick); // щелчок на каждый рывок
                const float tickTime = 0.15f;
                for (float t = 0f; t < tickTime; t += Time.deltaTime)
                {
                    float k = t / tickTime;
                    float eased = k < 0.7f ? k / 0.7f * 1.1f : 1.1f - (k - 0.7f) / 0.3f * 0.1f;
                    bigClock.SetAngle(from + step * eased);
                    yield return null;
                }
                bigClock.SetAngle(to);
                yield return new WaitForSeconds(0.17f);
            }
            bigClock.SetAngle(target);
            if (hudClock != null) hudClock.SetAngle(target);
        }

        // «???:» цветом говорящего, затем реплика, с курсором «_» пока печатается
        IEnumerator TypeLine()
        {
            string open = $"<color={speakerColor}>", close = "</color> ";
            for (int k = 1; k <= speaker.Length; k++)
            {
                line.text = open + speaker.Substring(0, k) + "</color>";
                SpinvUiSound.TypeTick(speaker[k - 1]);
                yield return new WaitForSeconds(speakerCharTime);
            }
            yield return new WaitForSeconds(0.35f);
            for (int k = 1; k <= message.Length; k++)
            {
                line.text = open + speaker + close + message.Substring(0, k) + (k < message.Length ? "_" : "");
                SpinvUiSound.TypeTick(message[k - 1]);
                yield return new WaitForSeconds(messageCharTime);
            }
        }

        static bool KeyPressed()
        {
    #if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    #elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    #else
            return false;
    #endif
        }
    }
}
