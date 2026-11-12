using UnityEngine;

namespace GC26.Spinv
{
    // Красная «гроздь» Bomber-а: падает как бомба, на высоте splitY лопается веером на маленькие бомбы.
    // Маленькие тоже падают по дуге, ранят корабль и откусывают бункеры
    [RequireComponent(typeof(Projectile))]
    public class ClusterBomb : MonoBehaviour
    {
        public Projectile bombletPrefab;
        public int bomblets = 5;
        [Tooltip("Раскрытие веера: от -spread до +spread градусов от вертикали")]
        public float spread = 55f;
        public float bombletSpeed = 4f;
        public float bombletGravity = 10f;
        public GameObject burstEffect;

        [HideInInspector] public float splitY = -3f;

        void Update()
        {
            if (transform.position.y > splitY) return;

            for (int i = 0; i < bomblets; i++)
            {
                float t = bomblets > 1 ? (float)i / (bomblets - 1) : 0.5f;
                Vector2 dir = Quaternion.Euler(0f, 0f, Mathf.Lerp(-spread, spread, t)) * Vector3.down;
                Projectile bomblet = Instantiate(bombletPrefab, transform.position, Quaternion.identity);
                bomblet.gravity = bombletGravity;
                bomblet.Throw(dir * bombletSpeed);
            }
            SpinvSoundManager.Play(Sfx.BombExplosion);
            if (burstEffect != null)
            {
                GameObject burst = Instantiate(burstEffect, transform.position, Quaternion.identity);
                burst.transform.localScale = Vector3.one * 1.5f;
            }
            Destroy(gameObject);
        }
    }
}
