using UnityEngine;

namespace GC26.Spinv
{
    // Пришелец, вызванный боссом: падает вниз и ранит игрока, если долетит до него
    public class FallingMinion : MonoBehaviour
    {
        public float fallSpeed = 2.5f;
        public PlayerController player;
        public float hitDistanceX = 1.4f;
        public float hitDistanceY = 1f;
        public float destroyBelowY = -14.5f;

        void Update()
        {
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;

            if (transform.position.y < destroyBelowY)
            {
                Destroy(gameObject);
                return;
            }

            if (player == null) return;

            Vector3 offset = transform.position - player.transform.position;
            if (Mathf.Abs(offset.x) < hitDistanceX && Mathf.Abs(offset.y) < hitDistanceY)
            {
                player.Hit();
                Destroy(gameObject);
            }
        }
    }
}
