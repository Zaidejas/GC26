using UnityEngine;

namespace GC26.Spinv
{
    // Хилка: банка падает вниз, игрок ловит её кораблём и получает жизнь
    public class HealthPickup : MonoBehaviour
    {
        public int lives = 1;
        public float fallSpeed = 5f;
        public float destroyBelowY = -14.5f;

        bool collected;

        void Update()
        {
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            if (transform.position.y < destroyBelowY)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !other.TryGetComponent(out PlayerController _)) return;

            collected = true;
            SpinvGameManager.Instance.AddLives(lives);
            SpinvSoundManager.Play(Sfx.Heal);
            Destroy(gameObject);
        }
    }
}
