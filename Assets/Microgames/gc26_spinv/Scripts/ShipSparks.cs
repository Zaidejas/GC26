using UnityEngine;

namespace GC26.Spinv
{
    // Оранжевые искры из корабля, пока работает ускоренная стрельба: вылетают с краёв диска, падают и гаснут
    [RequireComponent(typeof(PlayerController))]
    public class ShipSparks : MonoBehaviour
    {
        public Sprite sparkSprite;
        public Color startColor = new Color(1f, 0.78f, 0.3f);
        public Color endColor = new Color(1f, 0.42f, 0.05f);
        [Tooltip("Искр в секунду")]
        public float rate = 45f;
        public Vector2 lifetime = new Vector2(0.3f, 0.6f);
        public Vector2 size = new Vector2(0.1f, 0.17f);
        public Vector2 sideSpeed = new Vector2(1f, 3.2f);
        public Vector2 upSpeed = new Vector2(0.5f, 3.5f);
        public float gravity = 10f;
        public int poolSize = 48;
        public int sortingOrder = 5;

        struct Spark
        {
            public Vector2 velocity;
            public float age, life, size;
        }

        PlayerController player;
        SpriteRenderer shipRenderer;
        SpriteRenderer[] renderers;
        Spark[] sparks;
        float emitDebt;
        float unitScale = 1f; // размер в юнитах -> масштаб спрайта

        void Awake()
        {
            player = GetComponent<PlayerController>();
            shipRenderer = GetComponent<SpriteRenderer>();
            if (sparkSprite != null && sparkSprite.bounds.size.x > 0f)
                unitScale = 1f / sparkSprite.bounds.size.x;

            // Искры живут отдельно от корабля: улетевшие не тянутся за ним
            var holder = new GameObject("ShipSparks").transform;
            renderers = new SpriteRenderer[poolSize];
            sparks = new Spark[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var spark = new GameObject("Spark", typeof(SpriteRenderer));
                spark.transform.SetParent(holder, false);
                renderers[i] = spark.GetComponent<SpriteRenderer>();
                renderers[i].sprite = sparkSprite;
                renderers[i].sortingOrder = sortingOrder;
                spark.SetActive(false);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // пауза

            if (player.fireRateMultiplier > 1f && shipRenderer.enabled)
            {
                emitDebt += rate * dt;
                while (emitDebt >= 1f)
                {
                    emitDebt -= 1f;
                    Emit();
                }
            }
            else
            {
                emitDebt = 0f;
            }

            for (int i = 0; i < poolSize; i++)
            {
                if (!renderers[i].gameObject.activeSelf) continue;

                ref Spark spark = ref sparks[i];
                spark.age += dt;
                if (spark.age >= spark.life)
                {
                    renderers[i].gameObject.SetActive(false);
                    continue;
                }
                spark.velocity.y -= gravity * dt;

                Transform t = renderers[i].transform;
                t.position += (Vector3)(spark.velocity * dt);
                float k = spark.age / spark.life;
                t.localScale = Vector3.one * spark.size * unitScale * (1f - 0.6f * k);
                Color color = Color.Lerp(startColor, endColor, k);
                color.a = 1f - k * k;
                renderers[i].color = color;
            }
        }

        void Emit()
        {
            for (int i = 0; i < poolSize; i++)
            {
                if (renderers[i].gameObject.activeSelf) continue;

                Bounds bounds = shipRenderer.bounds;
                float side = Random.value < 0.5f ? -1f : 1f;
                // С краёв диска и немного с купола
                float x = bounds.center.x + side * Random.Range(0.2f, 0.85f) * bounds.extents.x;
                float y = bounds.min.y + Random.Range(0.15f, 0.45f) * bounds.size.y;

                sparks[i] = new Spark
                {
                    velocity = new Vector2(side * Random.Range(sideSpeed.x, sideSpeed.y), Random.Range(upSpeed.x, upSpeed.y)),
                    age = 0f,
                    life = Random.Range(lifetime.x, lifetime.y),
                    size = Random.Range(size.x, size.y),
                };
                Transform t = renderers[i].transform;
                t.position = new Vector3(x, y, transform.position.z);
                t.localScale = Vector3.one * sparks[i].size * unitScale;
                renderers[i].color = startColor;
                renderers[i].gameObject.SetActive(true);
                return;
            }
        }
    }
}
