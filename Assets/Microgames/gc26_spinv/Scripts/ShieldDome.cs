using UnityEngine;

namespace GC26.Spinv
{
    // Щит-купол над кораблём: по нему бегут сканлайны вниз, край пульсирует,
    // в последние полсекунды купол мигает - щит вот-вот кончится
    public class ShieldDome : MonoBehaviour
    {
        public SpriteRenderer fill;
        public SpriteRenderer edge;
        public SpriteRenderer scanlines;
        [Tooltip("Шаг сканлайнов в локальных единицах: на столько они сдвигаются и начинают сначала")]
        public float scanPeriod = 0.097f;
        public float scanSpeed = 0.2f;
        public float pulseSpeed = 6f;
        [Range(0f, 1f)] public float edgeMinAlpha = 0.55f;

        [Header("Мигание перед концом")]
        public float blinkTime = 0.5f;
        public float blinkInterval = 0.08f;

        PlayerController player;
        float scroll, pulse;

        void Awake()
        {
            player = GetComponentInParent<PlayerController>();
        }

        void OnEnable()
        {
            scroll = 0f;
            pulse = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            scroll = Mathf.Repeat(scroll + scanSpeed * dt, scanPeriod);
            scanlines.transform.localPosition = new Vector3(0f, -scroll, 0f);

            pulse += pulseSpeed * dt;
            Color edgeColor = edge.color;
            edgeColor.a = Mathf.Lerp(edgeMinAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(pulse));
            edge.color = edgeColor;

            float left = player != null ? player.shieldTimeLeft : 0f;
            bool visible = left <= 0f || left > blinkTime || Mathf.Repeat(left, blinkInterval * 2f) > blinkInterval;
            fill.enabled = edge.enabled = scanlines.enabled = visible;
        }
    }
}
