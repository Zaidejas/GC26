using UnityEngine;

namespace GC26.Spinv
{
    // Двойник Phantom-а: такой же с виду, но рябит, гибнет от одного попадания (decoyHealth у босса), урона боссу не наносит.
    // Проявляется мерцая, ходит вбок, стреляет в игрока, через lifetime с глюком исчезает
    public class PhantomDecoy : MonoBehaviour
    {
        SpriteRenderer sprite;
        PlayerController player;
        Projectile missilePrefab;
        GameObject explosionPrefab;
        float missileSpeed, moveSpeed, minX, maxX;
        float fireInterval, lifeLeft, appearLeft, fireTimer, rippleTimer;
        float direction = 1f;
        Collider2D hitbox;
        int health;
        float flashTimer;
        Color hitColor = new Color(0.3f, 0.45f, 0.3f);

        public static PhantomDecoy Create(Boss boss, SpriteRenderer bossSprite, Vector3 position, PlayerController target,
            float lifetime, float fireInterval, float appearTime, float minX, float maxX, int health)
        {
            var go = new GameObject("PhantomDecoy", typeof(SpriteRenderer), typeof(BoxCollider2D));
            go.transform.position = position;
            go.transform.localScale = boss.transform.localScale;

            var decoy = go.AddComponent<PhantomDecoy>();
            decoy.sprite = go.GetComponent<SpriteRenderer>();
            decoy.sprite.sprite = bossSprite.sprite;
            decoy.sprite.sortingOrder = bossSprite.sortingOrder;
            decoy.sprite.enabled = false;

            var box = go.GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = bossSprite.sprite.bounds.size;
            box.enabled = false;
            decoy.hitbox = box;

            decoy.player = target;
            decoy.missilePrefab = boss.missilePrefab;
            decoy.missileSpeed = boss.missileSpeed;
            decoy.explosionPrefab = boss.explosionPrefab;
            decoy.moveSpeed = boss.moveSpeed;
            decoy.minX = minX;
            decoy.maxX = maxX;
            decoy.fireInterval = fireInterval;
            decoy.lifeLeft = lifetime;
            decoy.appearLeft = appearTime;
            decoy.fireTimer = fireInterval * Random.Range(0.5f, 1f);
            decoy.direction = Random.value < 0.5f ? -1f : 1f;
            decoy.health = Mathf.Max(1, health);
            decoy.hitColor = boss.hitColor;
            return decoy;
        }

        void Update()
        {
            // Проявляется, мерцая, как настоящий после телепорта
            if (appearLeft > 0f)
            {
                appearLeft -= Time.deltaTime;
                sprite.enabled = Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
                if (appearLeft <= 0f)
                {
                    sprite.enabled = true;
                    hitbox.enabled = true;
                }
                return;
            }

            lifeLeft -= Time.deltaTime;
            if (lifeLeft <= 0f)
            {
                Destroy(gameObject); // время вышло - просто растворяется
                return;
            }

            Vector3 pos = transform.position;
            pos.x += direction * moveSpeed * Time.deltaTime;
            if (pos.x > maxX) direction = -1f;
            else if (pos.x < minX) direction = 1f;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            transform.position = pos;

            // Рябь: копия чуть мерцает, настоящий - нет. При попадании темнеет, как босс
            flashTimer -= Time.deltaTime;
            rippleTimer -= Time.deltaTime;
            if (rippleTimer <= 0f || flashTimer > 0f)
            {
                if (rippleTimer <= 0f) rippleTimer = 0.06f;
                Color baseColor = flashTimer > 0f ? hitColor : Color.white;
                sprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, Random.Range(0.55f, 0.95f));
            }

            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f && player != null && missilePrefab != null)
            {
                fireTimer = fireInterval;
                Vector3 muzzle = transform.position + Vector3.down * sprite.bounds.extents.y;
                Projectile missile = Instantiate(missilePrefab, muzzle, Quaternion.identity);
                missile.speed = missileSpeed;
                missile.SetDirection(player.transform.position - muzzle);
            }
        }

        // Вызывается пулей игрока: копия темнеет, а когда ХП кончится - лопается с маленьким взрывом
        public void Hit()
        {
            health--;
            flashTimer = 0.1f;
            if (health > 0) return;

            if (explosionPrefab != null)
            {
                GameObject boom = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                boom.transform.localScale = Vector3.one * 2f;
            }
            SpinvSoundManager.Play(Sfx.Explosion);
            Destroy(gameObject);
        }
    }
}
