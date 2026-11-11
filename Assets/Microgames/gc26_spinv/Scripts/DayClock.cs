using System.Collections;
using UnityEngine;

namespace GC26.Spinv
{
    // Часы «утро-день-ночь»: в круглой рамке вращается диск неба (верх - день, низ - ночь),
    // сверху неподвижный указатель. Время суток - иконка под указателем.
    // В HUD часы поворачиваются между волнами (новая волна ждёт поворота); в сценке ими управляет NightCutscene
    public class DayClock : MonoBehaviour
    {
        public RectTransform disc;
        [Tooltip("Если задан - часы поворачиваются по волнам")]
        public WaveManager waveManager;
        [Tooltip("Угол диска для каждой волны: -55 утро, 0 полдень, 180 ночь (против часовой стрелки)")]
        public float[] waveAngles = { -55f, -27f, 0f, 60f, 180f };
        public float turnTime = 0.8f;

        public float Angle { get; private set; }

        Coroutine turning;

        void Start()
        {
            if (waveManager == null) return;
            // В аркаде часов нет: время суток там не идёт
            if (waveManager.IsArcade || waveManager.IsTutorial)
            {
                gameObject.SetActive(false);
                return;
            }
            waveManager.WaveStarted += OnWaveStarted;
            SetAngle(waveAngles[0]);
        }

        void OnDestroy()
        {
            if (waveManager != null)
                waveManager.WaveStarted -= OnWaveStarted;
        }

        // Запасной путь (F7 и т.п.): если часы ещё не повёрнуты к началу волны - поворачиваем сейчас
        void OnWaveStarted(int wave, int total)
        {
            float target = AngleForWave(wave);
            if (!Mathf.Approximately(target, Angle))
                TurnTo(target);
        }

        public float AngleForWave(int wave) => waveAngles[Mathf.Clamp(wave - 1, 0, waveAngles.Length - 1)];

        // Между волнами: WaveManager ждёт, пока часы довернутся до следующей волны
        public IEnumerator TurnForWave(int wave)
        {
            float target = AngleForWave(wave);
            if (Mathf.Approximately(target, Angle) || !isActiveAndEnabled) yield break;
            TurnTo(target);
            while (turning != null) yield return null;
        }

        // Плавный поворот с замедлением в конце
        public void TurnTo(float target)
        {
            if (turning != null) StopCoroutine(turning);
            turning = null;
            if (isActiveAndEnabled) turning = StartCoroutine(Turn(target));
            else Apply(target);
        }

        IEnumerator Turn(float target)
        {
            float from = Angle;
            for (float t = 0f; t < turnTime; t += Time.deltaTime)
            {
                float k = t / turnTime;
                Apply(Mathf.Lerp(from, target, 1f - (1f - k) * (1f - k) * (1f - k)));
                yield return null;
            }
            Apply(target);
            turning = null;
        }

        // Мгновенно ставит угол и отменяет плавный поворот, если он шёл
        public void SetAngle(float angle)
        {
            if (turning != null)
            {
                StopCoroutine(turning);
                turning = null;
            }
            Apply(angle);
        }

        void Apply(float angle)
        {
            Angle = angle;
            disc.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
