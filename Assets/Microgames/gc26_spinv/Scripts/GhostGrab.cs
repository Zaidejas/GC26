using UnityEngine;

namespace GC26.Spinv
{
    // Хватка Phantom-а: на полу у корабля мерцает метка, через warnTime из неё вырастает призрачная рука.
    // Корабль над меткой в момент хватки - получает урон. Рука держится и уходит обратно в пол
    public class GhostGrab : MonoBehaviour
    {
        SpriteRenderer mark, hand;
        PlayerController player;
        Boss owner;
        float warnTime, riseTime = 0.15f, holdTime, sinkTime = 0.25f;
        float radius, age, handHeight;
        float floorY;
        bool struck;

        public static GhostGrab Create(Boss boss, Sprite markSprite, Sprite handSprite, Vector2 floorPoint, PlayerController target,
            float warn, float hold, float radius, float handScale)
        {
            var go = new GameObject("GhostGrab");
            go.transform.position = floorPoint;
            var grab = go.AddComponent<GhostGrab>();

            var markGo = new GameObject("Mark", typeof(SpriteRenderer));
            markGo.transform.SetParent(go.transform, false);
            grab.mark = markGo.GetComponent<SpriteRenderer>();
            grab.mark.sprite = markSprite;
            grab.mark.sortingOrder = 1;
            // Метка шириной в два радиуса хватки: видно, откуда надо уйти
            if (markSprite != null)
                markGo.transform.localScale = Vector3.one * (radius * 2f / markSprite.bounds.size.x);

            var handGo = new GameObject("Hand", typeof(SpriteRenderer));
            handGo.transform.SetParent(go.transform, false);
            grab.hand = handGo.GetComponent<SpriteRenderer>();
            grab.hand.sprite = handSprite;
            grab.hand.sortingOrder = 6;
            grab.hand.color = new Color(1f, 1f, 1f, 0.85f);
            handGo.transform.localScale = new Vector3(handScale, 0f, 1f);
            grab.handHeight = handSprite != null ? handSprite.bounds.size.y * handScale : 1f;
            grab.hand.enabled = false;

            grab.player = target;
            grab.owner = boss;
            grab.floorY = floorPoint.y;
            grab.warnTime = warn;
            grab.holdTime = hold;
            grab.radius = radius;
            return grab;
        }

        void Update()
        {
            age += Time.deltaTime;

            if (age < warnTime)
            {
                // Метка мерцает всё быстрее
                float rate = Mathf.Lerp(5f, 16f, age / warnTime);
                mark.color = new Color(1f, 1f, 1f, Mathf.Repeat(age * rate, 1f) < 0.5f ? 1f : 0.35f);
                return;
            }

            float t = age - warnTime;
            float grow;
            if (t < riseTime) grow = t / riseTime;
            else if (t < riseTime + holdTime) grow = 1f;
            else if (t < riseTime + holdTime + sinkTime) grow = 1f - (t - riseTime - holdTime) / sinkTime;
            else
            {
                Destroy(gameObject);
                return;
            }

            mark.color = new Color(1f, 1f, 1f, 0.5f);
            hand.enabled = true;
            Vector3 scale = hand.transform.localScale;
            scale.y = scale.x * grow;
            hand.transform.localScale = scale;
            // Рука растёт из пола: низ картинки стоит на метке
            hand.transform.position = new Vector3(transform.position.x, floorY + handHeight * grow * 0.5f, 0f);

            if (!struck && grow >= 1f)
            {
                struck = true;
                if (player != null && Mathf.Abs(player.transform.position.x - transform.position.x) < radius)
                {
                    if (owner != null) owner.OnGrabCaughtPlayer();
                    player.Hit();
                }
            }
        }
    }
}
