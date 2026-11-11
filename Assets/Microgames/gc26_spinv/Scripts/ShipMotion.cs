using UnityEngine;

namespace GC26.Spinv
{
    // Живой корабль: наклоняется в сторону движения, снизу пульсирует выхлоп (на ходу длиннее),
    // по переднему краю диска бегут огоньки. Мигает вместе с кораблём при попадании
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShipMotion : MonoBehaviour
    {
        [Header("Наклон")]
        [Tooltip("Градусов наклона на 1 юнит/с скорости")]
        public float tiltPerSpeed = 1.1f;
        public float maxTilt = 12f;
        public float tiltSharpness = 10f;

        [Header("Выхлоп")]
        public SpriteRenderer flame;
        public SpriteRenderer flameCore;
        public float flickerSpeed = 38f;
        [Range(0f, 1f)] public float flicker = 0.18f;
        [Tooltip("Насколько удлиняется пламя на полной скорости")]
        public float speedStretch = 0.7f;
        public float fullSpeed = 20f;

        [Header("Бегущие огни")]
        public Sprite lightSprite;
        public int lightCount = 6;
        public float lightSize = 0.045f;
        public Vector2 lightCenter = new Vector2(0f, -0.115f);
        public Vector2 lightRadius = new Vector2(0.4f, 0.045f);
        [Tooltip("Смена кадра огней, как у пришельцев: горят то чётные, то нечётные")]
        public float lightFrameTime = 0.5f;
        public Color lightColor = Color.white;
        public int lightOrder = 1;

        SpriteRenderer ship;
        SpriteRenderer[] lights;
        Vector3 flameScale, coreScale;
        float lastX, tilt, time, speedSmooth, lightTimer;
        int lightFrame;

        void Awake()
        {
            ship = GetComponent<SpriteRenderer>();
            lastX = transform.position.x;
            if (flame != null) flameScale = flame.transform.localScale;
            if (flameCore != null) coreScale = flameCore.transform.localScale;

            lights = new SpriteRenderer[lightSprite != null ? lightCount : 0];
            float unit = lightSprite != null && lightSprite.bounds.size.x > 0f ? 1f / lightSprite.bounds.size.x : 1f;
            for (int i = 0; i < lights.Length; i++)
            {
                var go = new GameObject("RunningLight", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * lightSize * unit;
                lights[i] = go.GetComponent<SpriteRenderer>();
                lights[i].sprite = lightSprite;
                lights[i].sortingOrder = lightOrder;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool visible = ship.enabled;
            if (dt > 0f)
            {
                float x = transform.position.x;
                float speed = (x - lastX) / dt;
                lastX = x;
                speedSmooth = Mathf.Lerp(speedSmooth, Mathf.Abs(speed), 1f - Mathf.Exp(-12f * dt));

                // Едет вправо - наклон по часовой стрелке
                float want = Mathf.Clamp(-speed * tiltPerSpeed, -maxTilt, maxTilt);
                tilt = Mathf.Lerp(tilt, want, 1f - Mathf.Exp(-tiltSharpness * dt));
                transform.rotation = Quaternion.Euler(0f, 0f, tilt);

                time += dt;
                lightTimer += dt;
                if (lightTimer >= lightFrameTime)
                {
                    lightTimer = 0f;
                    lightFrame = 1 - lightFrame;
                }
            }

            float pulse = 1f + flicker * Mathf.Sin(time * flickerSpeed) + speedStretch * Mathf.Clamp01(speedSmooth / fullSpeed);
            if (flame != null)
            {
                flame.transform.localScale = new Vector3(flameScale.x, flameScale.y * pulse, flameScale.z);
                flame.enabled = visible;
            }
            if (flameCore != null)
            {
                flameCore.transform.localScale = new Vector3(coreScale.x, coreScale.y * (pulse * 0.9f + flicker * Mathf.Sin(time * flickerSpeed * 1.7f)), coreScale.z);
                flameCore.enabled = visible;
            }

            // Огни стоят на местах по передней дуге диска и мигают в два кадра, как пришельцы
            for (int i = 0; i < lights.Length; i++)
            {
                float k = lights.Length > 1 ? (float)i / (lights.Length - 1) : 0.5f;
                float angle = Mathf.Lerp(200f, 340f, k) * Mathf.Deg2Rad;
                lights[i].transform.localPosition = new Vector3(
                    lightCenter.x + Mathf.Cos(angle) * lightRadius.x, lightCenter.y + Mathf.Sin(angle) * lightRadius.y, 0f);
                lights[i].color = lightColor;
                lights[i].enabled = visible && i % 2 == lightFrame;
            }
        }

        void OnDisable()
        {
            transform.rotation = Quaternion.identity;
        }
    }
}
