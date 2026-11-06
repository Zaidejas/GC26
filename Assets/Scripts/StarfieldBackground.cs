using UnityEngine;

// «Звёзды в полёте»: три слоя звёзд плывут вниз с разной скоростью (дальние медленнее),
// будто корабль летит вперёд, и мерцают. Висит на объекте Background и прячет его
// статичную картинку. Звёзды создаются при запуске, в сцене их нет.
// Время игровое: на паузе и после конца игры звёзды замирают вместе с игрой
public class StarfieldBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public int count = 40;
        [Tooltip("Скорость, юнитов в секунду")]
        public float speed = 1f;
        [Tooltip("Размер звезды, юнитов")]
        public float size = 0.08f;
        [Range(0f, 1f)] public float alpha = 0.5f;
    }

    public Layer[] layers =
    {
        new Layer { count = 45, speed = 0.35f, size = 0.07f, alpha = 0.3f },
        new Layer { count = 30, speed = 0.9f, size = 0.1f, alpha = 0.5f },
        new Layer { count = 16, speed = 1.8f, size = 0.14f, alpha = 0.75f },
    };

    public Color starColor = new Color(0.28f, 0.83f, 0.42f); // #48D46C
    [Tooltip("Насколько сильно мерцают звёзды (доля яркости)")]
    [Range(0f, 1f)] public float twinkle = 0.35f;
    public float twinkleSpeed = 2f;
    public int sortingOrder = -99;
    public int seed = 7;

    struct Star
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float phase;
        public int layer;
    }

    Star[] stars;
    float top, bottom, halfWidth;

    void Start()
    {
        // Статичная картинка со звёздами больше не нужна: фон - цвет камеры
        var picture = GetComponent<SpriteRenderer>();
        if (picture != null) picture.enabled = false;

        Camera cam = Camera.main;
        float halfHeight = cam != null ? cam.orthographicSize : 13f;
        halfWidth = halfHeight * (cam != null ? cam.aspect : 0.5f);
        float centerY = cam != null ? cam.transform.position.y : 0f;
        top = centerY + halfHeight + 0.5f;
        bottom = centerY - halfHeight - 0.5f;

        Sprite dot = MakeDotSprite();
        var root = new GameObject("Starfield").transform; // не дочерний: фон растянут масштабом
        var random = new System.Random(seed);

        int total = 0;
        foreach (Layer layer in layers) total += layer.count;
        stars = new Star[total];

        int index = 0;
        for (int l = 0; l < layers.Length; l++)
        {
            for (int i = 0; i < layers[l].count; i++)
            {
                var go = new GameObject("Star");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(
                    Mathf.Lerp(-halfWidth, halfWidth, (float)random.NextDouble()),
                    Mathf.Lerp(bottom, top, (float)random.NextDouble()), 5f);
                go.transform.localScale = Vector3.one * layers[l].size;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = dot;
                renderer.sortingOrder = sortingOrder;

                stars[index++] = new Star
                {
                    transform = go.transform,
                    renderer = renderer,
                    phase = (float)random.NextDouble() * 6.28f,
                    layer = l,
                };
            }
        }
    }

    void Update()
    {
        if (stars == null) return;
        float dt = Time.deltaTime;
        float t = Time.time;

        for (int i = 0; i < stars.Length; i++)
        {
            Star star = stars[i];
            Layer layer = layers[star.layer];

            Vector3 pos = star.transform.position;
            pos.y -= layer.speed * dt;
            if (pos.y < bottom)
            {
                // Ушла вниз - появляется сверху в новом месте
                pos.y += top - bottom;
                pos.x = Random.Range(-halfWidth, halfWidth);
            }
            star.transform.position = pos;

            Color c = starColor;
            c.a = layer.alpha * (1f - twinkle * 0.5f + twinkle * 0.5f * Mathf.Sin(t * twinkleSpeed + star.phase));
            star.renderer.color = c;
        }
    }

    // Мягкая круглая точка, рисуется в коде: отдельный файл не нужен
    static Sprite MakeDotSprite()
    {
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float center = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
