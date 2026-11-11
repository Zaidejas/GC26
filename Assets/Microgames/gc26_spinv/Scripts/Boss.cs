using System.Collections;
using UnityEngine;

namespace GC26.Spinv
{
    public enum BossAttack { Fan, Burst, Dash, Wall, Spiral, Laser, Summon, Mines, Carpet, Teleport, Decoys, Scream, Wisps, Grab, Drain, Cluster }

    // Босс: двигается вбок и чередует атаки. При половине здоровья может войти в ярость
    public class Boss : MonoBehaviour
    {
        [Header("Здоровье и награда")]
        public int maxHealth = 100;
        public int points = 500;
        public Coin coinPrefab;
        [Tooltip("Монетка выпадает каждые N попаданий")]
        public int coinEveryHits = 8;
        public GameObject explosionPrefab;
        [Tooltip("За победу над этим боссом выдаётся достижение")]
        public bool grantsAchievement = false;
        public AchievementId achievement;

        [Header("Движение")]
        public float moveSpeed = 3f;
        public float edgePadding = 0.3f;
        public float dashSpeed = 20f;
        [Tooltip("Рывками: резко перелетает в случайную точку и замирает (Bomber)")]
        public bool jerkyMovement = false;
        public float hopSpeed = 16f;
        [Tooltip("Сколько плавно парит между рывками: случайно от X до Y")]
        public Vector2 hopPause = new Vector2(1.6f, 2.4f);
        [Tooltip("Рывок не короче этого, чтобы было заметно")]
        public float hopMinDistance = 2f;

        [Header("Атаки")]
        public BossAttack[] attacks = { BossAttack.Fan };
        public float firstAttackDelay = 1f;
        [Tooltip("Пауза между атаками")]
        public float attackPause = 1f;
        public Projectile missilePrefab;
        public float missileSpeed = 9f;

        [Header("Веер")]
        public int fanCount = 5;
        public float fanAngle = 30f;
        public int fanVolleys = 3;
        public float fanInterval = 0.8f;

        [Header("Очередь")]
        public int burstCount = 6;
        public float burstInterval = 0.15f;

        [Header("Рывок")]
        public int dashVolleyCount = 5;
        public float dashVolleyAngle = 12f;

        [Header("Стена с дырой")]
        public float wallSpacing = 0.9f;
        [Tooltip("Запас прохода с каждой стороны корабля, юнитов (включая полуширину ракеты)")]
        public float wallGapMargin = 0.75f;
        public int wallCount = 2;
        public float wallInterval = 1.2f;

        [Header("Спираль")]
        public float spiralDuration = 3f;
        public float spiralInterval = 0.12f;
        public float spiralStep = 22f;
        public float spiralMissileSpeed = 7f;

        [Header("Лазер")]
        public LaserBeam laserPrefab;
        public float laserBottomY = -14f;

        [Header("Призыв")]
        public Invader minionPrefab;
        public int minionsPerSummon = 2;
        public float minionFallSpeed = 2.5f;

        [Header("Мины (Bomber)")]
        public BossMine minePrefab;
        public int mineCount = 3;
        public int rageMineCount = 5;
        [Tooltip("Мины падают по одной, с такой паузой, каждая в случайное место")]
        public float mineDropInterval = 0.7f;
        public float mineFuse = 2f;

        [Header("Бомбы вразнобой (Bomber)")]
        public Projectile bombPrefab;
        [Tooltip("Сколько секунд босс сыплет бомбы, летая как обычно")]
        public float bombRainTime = 3f;
        [Tooltip("Пауза между бомбами: случайно от X до Y")]
        public Vector2 bombDropDelay = new Vector2(0.12f, 0.35f);
        [Tooltip("Начальная скорость бомбы вниз; дальше её разгоняет гравитация")]
        public float bombSpeed = 1.5f;
        [Tooltip("Ускорение падения бомбы, юнитов/с²")]
        public float bombGravity = 10f;
        [Tooltip("Какую часть скорости босса бомба получает вбок: падает по дуге")]
        public float bombInherit = 0.6f;
        [Tooltip("Рывковый босс: сколько остановок за атаку и сколько бомб на каждой")]
        public int bombHops = 4;
        public int rageBombHops = 6;
        public Vector2Int bombsPerStop = new Vector2Int(2, 3);

        [Header("Гроздь (Bomber)")]
        public Projectile clusterPrefab;
        public int clusterCount = 2;
        public int rageClusterCount = 3;
        [Tooltip("На какой высоте над кораблём гроздь лопается")]
        public float clusterSplitHeight = 5f;
        [Tooltip("Насколько далеко от корабля по горизонтали падают грозди (первая - над ним)")]
        public float clusterSpread = 3f;
        public float rageBombSpeed = 1.3f;

        [Header("Телепорт и двойники (Phantom)")]
        public float vanishTime = 0.35f;
        public float hiddenTime = 0.7f;
        [Tooltip("Сколько мерцает силуэт на новом месте перед появлением")]
        public float warnTime = 0.5f;
        public float teleportYMin = 6f;
        public float teleportYMax = 8.5f;
        [Tooltip("Сразу после телепорта - веер в игрока")]
        public bool ambushAfterTeleport = true;
        public int decoyCount = 2;
        public int rageDecoyCount = 3;
        public float decoyLifetime = 7f;
        public float decoyFireInterval = 1.8f;
        [Tooltip("ХП двойника: 1 - лопается от одного попадания")]
        public int decoyHealth = 1;
        [Header("Крик и огоньки (Phantom)")]
        [Tooltip("Пуль в кольце крика (с дырой)")]
        public int screamCount = 24;
        [Tooltip("Сколько пуль пропущено подряд: дыра, в которую надо встать")]
        public int screamGap = 4;
        public float screamSpeed = 6f;
        [Tooltip("Колец за крик в ярости (обычно одно)")]
        public int rageScreamRings = 2;
        public float screamRingDelay = 0.6f;
        public Wisp wispPrefab;
        public int wispCount = 3;
        public int rageWispCount = 5;

        [Header("Хватка (Phantom)")]
        public Sprite grabMarkSprite;
        public Sprite grabHandSprite;
        [Tooltip("Сколько рук за раз: метки в случайных местах по полю")]
        public int grabCount = 3;
        public int rageGrabCount = 4;
        [Tooltip("Метки не ближе друг к другу, чем столько радиусов хватки")]
        public float grabMinGap = 1.6f;
        [Tooltip("Сколько мерцает метка, прежде чем вырастет рука")]
        public float grabWarn = 1f;
        public float grabHold = 0.45f;
        public float grabRadius = 0.9f;
        public float grabHandScale = 1.6f;
        [Tooltip("Насколько ниже центра корабля пол, из которого растут руки")]
        public float grabFloorOffset = 0.5f;

        [Header("Луч-высасывание (Phantom)")]
        public Sprite drainBeamSprite;
        public float drainTime = 3f;
        [Tooltip("Корабль в луче вязнет: доля обычной скорости (0.35 - медленнее на 65%)")]
        [Range(0.1f, 1f)] public float drainSlow = 0.35f;
        [Tooltip("Пока держит луч, босс стреляет веером с такой паузой")]
        public float drainFanInterval = 0.9f;
        public float drainBeamWidth = 0.3f;

        [Tooltip("В ярости пули почти прозрачные и проявляются только у игрока")]
        public bool ghostShotsInRage = false;
        public float ghostRevealHeight = 4f;

        [Tooltip("Финальный босс: игрок «устаёт» и время от времени моргает")]
        public bool playerBlinks = false;

        [Header("Эффектная смерть (финальный босс)")]
        [Tooltip("Вместо одного взрыва - анимация ~5 с: цепочка взрывов, нарастающая тряска, большой взрыв")]
        public bool dramaticDeath = false;
        public float deathChainTime = 3.2f;
        public float deathAfterTime = 1.8f;

        [Header("Ярость при половине здоровья")]
        public bool rageAtHalfHealth = false;
        [Tooltip("Во сколько раз чаще идут атаки в ярости")]
        public float rageFireRate = 1.5f;
        [Range(0f, 1f)]
        [Tooltip("Шанс запустить сразу две атаки")]
        public float doubleAttackChance = 0.35f;
        public Color rageColor = new Color(0.55f, 0.75f, 0.55f);

        [Header("Эффект попадания")]
        public Color hitColor = new Color(0.3f, 0.45f, 0.3f);
        public float flashTime = 0.1f;

        public event System.Action Died;

        public float HealthFraction => (float)health / maxHealth;
        // Убит: идёт анимация смерти или босс уже уничтожен
        public bool IsDead => dead;
        bool Raging => rageAtHalfHealth && health * 2 <= maxHealth;
        Vector3 Muzzle => transform.position + Vector3.down * spriteRenderer.bounds.extents.y;

        PlayerController player;
        [Tooltip("Картинка босса. Лежит в дочернем Visual, чтобы тряска не двигала коллайдер")]
        [SerializeField] SpriteRenderer spriteRenderer;
        int health;
        int hits;
        float minX, maxX;
        float leftEdge, rightEdge;
        float direction = 1f;
        float flashTimer;
        int movementHolds;
        int lastAttack = -1;
        bool dead;

        public void Init(PlayerController target)
        {
            player = target;
        }

        void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            health = maxHealth;
        }

        void Start()
        {
            Camera cam = Camera.main;
            leftEdge = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x;
            rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x;
            float halfWidth = spriteRenderer.bounds.extents.x + edgePadding;
            minX = leftEdge + halfWidth;
            maxX = rightEdge - halfWidth;

            StartCoroutine(AttackLoop());
        }

        void Update()
        {
            if (dead) return; // во время анимации смерти босс стоит, цветом управляет BossAnger
            if (movementHolds == 0)
                Move();
            UpdateColor();
        }

        float hopWait;
        float hopTarget = float.NaN;

        // Случайная точка не ближе hopMinDistance к текущей (если экран позволяет)
        float PickHopTarget()
        {
            float x = transform.position.x, target = x;
            for (int i = 0; i < 8; i++)
            {
                target = Random.Range(minX, maxX);
                if (Mathf.Abs(target - x) >= hopMinDistance) break;
            }
            return target;
        }

        // Рывок к точке: резкий старт, торможение у цели. true - долетел
        bool HopTowards(float targetX)
        {
            Vector3 pos = transform.position;
            float left = Mathf.Abs(targetX - pos.x);
            float speed = hopSpeed * Mathf.Clamp(left / 1.2f, 0.25f, 1f);
            pos.x = Mathf.MoveTowards(pos.x, targetX, speed * Time.deltaTime);
            transform.position = pos;
            return Mathf.Approximately(pos.x, targetX);
        }

        // Рывковый босс: плавно парит hopPause секунд, потом резко рвёт в случайную точку, и снова парит
        void Move()
        {
            if (jerkyMovement)
            {
                if (hopWait > 0f)
                {
                    hopWait -= Time.deltaTime;
                    Drift();
                    return;
                }
                if (float.IsNaN(hopTarget)) hopTarget = PickHopTarget();
                if (HopTowards(hopTarget))
                {
                    hopTarget = float.NaN;
                    hopWait = Random.Range(hopPause.x, hopPause.y);
                    direction = Random.value < 0.5f ? -1f : 1f;
                }
                return;
            }
            Drift();
        }

        // Обычное плавное движение вбок от края до края
        void Drift()
        {
            Vector3 pos = transform.position;
            pos.x += direction * moveSpeed * Time.deltaTime;

            if (pos.x > maxX) direction = -1f;
            else if (pos.x < minX) direction = 1f;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            transform.position = pos;
        }

        void UpdateColor()
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer > 0f)
                spriteRenderer.color = hitColor;
            else
                spriteRenderer.color = Raging ? rageColor : Color.white;
        }

        IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(firstAttackDelay);

            while (true)
            {
                BossAttack attack = NextAttack();

                // Телепорт и двойники прячут самого босса - их нельзя смешивать с другой атакой; мины тоже идут одни
                if (Raging && attacks.Length > 1 && Random.value < doubleAttackChance)
                {
                    BossAttack extra = NextAttack();
                    // Две атаки, которые обе таскают босса (ковёр + грозди у Bomber-а), тянули бы его каждая
                    // к своей точке: он замирал между ними навсегда, и атаки больше не шли
                    if (!IsSolo(attack) && !IsSolo(extra) && !(MovesBoss(attack) && MovesBoss(extra)))
                        StartCoroutine(TrackAttack(extra));
                }

                yield return StartCoroutine(TrackAttack(attack));

                // Не больше двух атак одновременно: следующая начнётся, когда закончатся обе
                while (runningAttacks > 0)
                    yield return null;

                yield return new WaitForSeconds(Raging ? attackPause / rageFireRate : attackPause);
            }
        }

        int runningAttacks;

        // Мины тоже идут без второй атаки: в ярости мин и так много, вместе с ковром или гроздьями - почти верная смерть
        static bool IsSolo(BossAttack attack) => attack == BossAttack.Teleport || attack == BossAttack.Decoys || attack == BossAttack.Mines;

        // Атаки, которые сами двигают босса (рывки к точке, подлёт к кораблю)
        static bool MovesBoss(BossAttack attack) => attack == BossAttack.Carpet || attack == BossAttack.Cluster
            || attack == BossAttack.Dash || attack == BossAttack.Laser;

        IEnumerator TrackAttack(BossAttack attack)
        {
            runningAttacks++;
            yield return StartCoroutine(RunAttack(attack));
            runningAttacks--;
        }

        // Случайная атака, но не та же, что была только что
        BossAttack NextAttack()
        {
            int index = Random.Range(0, attacks.Length);
            if (attacks.Length > 1 && index == lastAttack)
                index = (index + 1) % attacks.Length;
            lastAttack = index;
            return attacks[index];
        }

        IEnumerator RunAttack(BossAttack attack)
        {
            switch (attack)
            {
                case BossAttack.Fan: return FanAttack();
                case BossAttack.Burst: return BurstAttack();
                case BossAttack.Dash: return DashAttack();
                case BossAttack.Wall: return WallAttack();
                case BossAttack.Spiral: return SpiralAttack();
                case BossAttack.Laser: return LaserAttack();
                case BossAttack.Mines: return MinesAttack();
                case BossAttack.Carpet: return CarpetAttack();
                case BossAttack.Teleport: return TeleportAttack();
                case BossAttack.Decoys: return DecoysAttack();
                case BossAttack.Scream: return ScreamAttack();
                case BossAttack.Wisps: return WispsAttack();
                case BossAttack.Grab: return GrabAttack();
                case BossAttack.Cluster: return ClusterAttack();
                case BossAttack.Drain: return DrainAttack();
                default: return SummonAttack();
            }
        }

        float Wait(float seconds) => Raging ? seconds / rageFireRate : seconds;

        void SpawnMissile(Vector3 position, Vector2 shotDirection, float speed)
        {
            Projectile missile = Instantiate(missilePrefab, position, Quaternion.identity);
            missile.speed = speed;
            missile.SetDirection(shotDirection);
            if (ghostShotsInRage && Raging && player != null)
                missile.gameObject.AddComponent<GhostShot>().revealY = player.transform.position.y + ghostRevealHeight;
        }

        void FireFan(int count, float angle)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                Vector2 shot = Quaternion.Euler(0f, 0f, Mathf.Lerp(-angle, angle, t)) * Vector3.down;
                SpawnMissile(Muzzle, shot, missileSpeed);
            }
        }

        IEnumerator FanAttack()
        {
            for (int i = 0; i < fanVolleys; i++)
            {
                FireFan(fanCount, fanAngle);
                yield return new WaitForSeconds(Wait(fanInterval));
            }
        }

        IEnumerator BurstAttack()
        {
            for (int i = 0; i < burstCount; i++)
            {
                if (player != null)
                    SpawnMissile(Muzzle, player.transform.position - Muzzle, missileSpeed);
                yield return new WaitForSeconds(Wait(burstInterval));
            }
        }

        IEnumerator MoveAbovePlayer()
        {
            if (player == null) yield break;

            float targetX = Mathf.Clamp(player.transform.position.x, minX, maxX);
            while (!Mathf.Approximately(transform.position.x, targetX))
            {
                Vector3 pos = transform.position;
                pos.x = Mathf.MoveTowards(pos.x, targetX, dashSpeed * Time.deltaTime);
                transform.position = pos;
                yield return null;
            }
        }

        IEnumerator DashAttack()
        {
            movementHolds++;
            yield return MoveAbovePlayer();
            FireFan(dashVolleyCount, dashVolleyAngle);
            yield return new WaitForSeconds(0.2f);
            movementHolds--;
        }

        IEnumerator WallAttack()
        {
            for (int wall = 0; wall < wallCount; wall++)
            {
                // Проход ставим туда, куда корабль реально может встать, и шириной от самого корабля.
                // Раньше дыра считалась в клетках от края экрана и у краёв была уже корабля
                float gapCenter = Random.Range(player.MinX, player.MaxX);
                float gapHalf = player.HalfWidth + wallGapMargin;

                int cells = Mathf.CeilToInt((rightEdge - leftEdge) / wallSpacing) + 1;
                for (int i = 0; i < cells; i++)
                {
                    float x = leftEdge + i * wallSpacing;
                    if (Mathf.Abs(x - gapCenter) < gapHalf) continue;
                    SpawnMissile(new Vector3(x, Muzzle.y, 0f), Vector2.down, missileSpeed);
                }
                yield return new WaitForSeconds(Wait(wallInterval));
            }
        }

        IEnumerator SpiralAttack()
        {
            float angle = Random.Range(0f, 360f);
            for (float elapsed = 0f; elapsed < spiralDuration; elapsed += spiralInterval)
            {
                SpawnMissile(Muzzle, Quaternion.Euler(0f, 0f, angle) * Vector3.down, spiralMissileSpeed);
                SpawnMissile(Muzzle, Quaternion.Euler(0f, 0f, angle + 180f) * Vector3.down, spiralMissileSpeed);
                angle += spiralStep;
                yield return new WaitForSeconds(spiralInterval);
            }
        }

        // Босс встаёт над игроком и замирает, пока идёт предупреждение и работает луч
        IEnumerator LaserAttack()
        {
            movementHolds++;
            yield return MoveAbovePlayer();

            LaserBeam beam = Instantiate(laserPrefab);
            beam.Init(player, transform.position.x, Muzzle.y, laserBottomY);
            yield return new WaitForSeconds(beam.TotalTime);

            movementHolds--;
        }

        IEnumerator SummonAttack()
        {
            for (int i = 0; i < minionsPerSummon; i++)
            {
                float x = Random.Range(minX, maxX);
                Invader minion = Instantiate(minionPrefab, new Vector3(x, transform.position.y, 0f), Quaternion.identity);
                minion.Init(null, 0, 0, 0f);

                FallingMinion falling = minion.gameObject.AddComponent<FallingMinion>();
                falling.fallSpeed = minionFallSpeed;
                falling.player = player;
            }
            yield return new WaitForSeconds(Wait(1f));
        }

        // ---------- Bomber ----------

        // Мины по одной, каждая в случайное место по ширине экрана; ложатся на высоте корабля.
        // В ярости их больше
        IEnumerator MinesAttack()
        {
            if (minePrefab == null || player == null) yield break;
            int count = Raging ? rageMineCount : mineCount;
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(player.MinX, player.MaxX);
                BossMine mine = Instantiate(minePrefab, new Vector3(transform.position.x, Muzzle.y, 0f), Quaternion.identity);
                mine.explosionPrefab = explosionPrefab;
                mine.Init(player, new Vector2(x, player.transform.position.y), mineFuse);
                yield return new WaitForSeconds(Wait(mineDropInterval));
            }
        }

        // Летает как обычно и сыплет бомбы вразнобой: в случайные моменты и из разных мест под корпусом.
        // Бомба падает с ускорением и по дуге (сносит вбок по ходу босса), выбивает из бункеров большие куски
        IEnumerator CarpetAttack()
        {
            if (bombPrefab == null) yield break;
            float rage = Raging ? rageBombSpeed : 1f;
            float halfWidth = spriteRenderer.bounds.extents.x * 0.8f;

            // Рывковый: рванул - сбросил пачку - рванул дальше (бомбёжка - серия рывков подряд,
            // между ними паузы короткие). Потом снова плавно парит
            if (jerkyMovement)
            {
                movementHolds++;
                int hops = Raging ? rageBombHops : bombHops;
                for (int hop = 0; hop < hops; hop++)
                {
                    float target = PickHopTarget();
                    while (!HopTowards(target)) yield return null;

                    int bombs = Random.Range(bombsPerStop.x, bombsPerStop.y + 1);
                    for (int i = 0; i < bombs; i++)
                    {
                        Projectile bomb = Instantiate(bombPrefab, Muzzle + Vector3.right * Random.Range(-halfWidth, halfWidth) * 0.6f, Quaternion.identity);
                        bomb.gravity = bombGravity * rage;
                        bomb.Throw(new Vector2(Random.Range(-0.6f, 0.6f), -bombSpeed * rage));
                        yield return new WaitForSeconds(0.08f);
                    }
                    yield return new WaitForSeconds(Wait(Random.Range(0.15f, 0.35f)));
                }
                hopTarget = float.NaN;
                hopWait = Random.Range(hopPause.x, hopPause.y);
                movementHolds--;
                yield break;
            }

            for (float t = 0f; t < bombRainTime; )
            {
                Vector3 drop = Muzzle + Vector3.right * Random.Range(-halfWidth, halfWidth);
                Projectile bomb = Instantiate(bombPrefab, drop, Quaternion.identity);
                float sideways = movementHolds == 0 ? direction * moveSpeed * bombInherit : 0f;
                bomb.gravity = bombGravity * rage;
                bomb.Throw(new Vector2(sideways, -bombSpeed * rage));

                float wait = Wait(Random.Range(bombDropDelay.x, bombDropDelay.y));
                yield return new WaitForSeconds(wait);
                t += wait;
            }
        }

        // Грозди: рывок к точке над кораблём (или рядом), сброс красной грозди; она лопается
        // над бункерами веером маленьких бомб
        IEnumerator ClusterAttack()
        {
            if (clusterPrefab == null || player == null) yield break;
            movementHolds++;
            int count = Raging ? rageClusterCount : clusterCount;
            float rage = Raging ? rageBombSpeed : 1f;
            for (int i = 0; i < count; i++)
            {
                float x = player.transform.position.x + (i == 0 ? 0f : Random.Range(-clusterSpread, clusterSpread));
                x = Mathf.Clamp(x, minX, maxX);
                if (jerkyMovement)
                    while (!HopTowards(x)) yield return null;

                Projectile cluster = Instantiate(clusterPrefab, Muzzle, Quaternion.identity);
                cluster.gravity = bombGravity * rage;
                cluster.Throw(new Vector2(0f, -bombSpeed * rage));
                if (cluster.TryGetComponent(out ClusterBomb split))
                    split.splitY = player.transform.position.y + clusterSplitHeight;
                yield return new WaitForSeconds(Wait(0.6f));
            }
            hopTarget = float.NaN;
            hopWait = Random.Range(hopPause.x, hopPause.y);
            movementHolds--;
        }

        // ---------- Phantom ----------

        Collider2D Hitbox => GetComponent<Collider2D>();
        GameObject Visual => spriteRenderer.gameObject;

        // Глючит и пропадает: пули проходят насквозь, урона нет
        IEnumerator Vanish()
        {
            movementHolds++;
            if (Hitbox != null) Hitbox.enabled = false;
            for (float t = 0f; t < vanishTime; )
            {
                Visual.SetActive(!Visual.activeSelf);
                float step = Random.Range(0.03f, 0.08f);
                yield return new WaitForSeconds(step);
                t += step;
            }
            Visual.SetActive(false);
            yield return new WaitForSeconds(hiddenTime);
        }

        // Мерцающий силуэт на новом месте, затем проявляется целиком и снова уязвим
        IEnumerator Appear(Vector3 position)
        {
            transform.position = position;
            for (float t = 0f; t < warnTime; t += 0.07f)
            {
                Visual.SetActive(!Visual.activeSelf);
                yield return new WaitForSeconds(0.07f);
            }
            Visual.SetActive(true);
            if (Hitbox != null) Hitbox.enabled = true;
            movementHolds--;
        }

        Vector3 RandomTeleportPoint() =>
            new Vector3(Random.Range(minX, maxX), Random.Range(teleportYMin, teleportYMax), 0f);

        IEnumerator TeleportAttack()
        {
            yield return Vanish();
            yield return Appear(RandomTeleportPoint());
            if (ambushAfterTeleport) FireFan(fanCount, fanAngle);
            yield return new WaitForSeconds(Wait(0.4f));
        }

        // Исчезает, и в нескольких местах проявляются одинаковые силуэты: один настоящий, остальные - копии
        IEnumerator DecoysAttack()
        {
            yield return Vanish();

            int count = (Raging ? rageDecoyCount : decoyCount) + 1;
            var points = new System.Collections.Generic.List<Vector3>();
            for (int attempt = 0; points.Count < count && attempt < 60; attempt++)
            {
                Vector3 p = RandomTeleportPoint();
                bool farEnough = true;
                foreach (Vector3 other in points)
                    if (Mathf.Abs(other.x - p.x) < spriteRenderer.bounds.size.x * 0.8f) farEnough = false;
                if (farEnough || attempt > 40) points.Add(p);
            }

            int real = Random.Range(0, points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                if (i == real) continue;
                PhantomDecoy.Create(this, spriteRenderer, points[i], player, decoyLifetime, decoyFireInterval, warnTime, minX, maxX, decoyHealth);
            }
            yield return Appear(points[real]);
            yield return new WaitForSeconds(Wait(1f));
        }

        // Крик: замирает, вздрагивает и выпускает кольцо пуль во все стороны. В кольце дыра - примерно
        // в сторону корабля, но со сдвигом, надо найти и встать. В ярости колец два, дыра смещается
        IEnumerator ScreamAttack()
        {
            movementHolds++;
            yield return new WaitForSeconds(Wait(0.35f));

            int rings = Raging ? rageScreamRings : 1;
            Vector3 center = spriteRenderer.bounds.center;
            for (int ring = 0; ring < rings; ring++)
            {
                float toPlayer = player != null
                    ? Vector2.SignedAngle(Vector2.down, player.transform.position - center)
                    : 0f;
                float gapAngle = toPlayer + Random.Range(-35f, 35f);
                float step = 360f / Mathf.Max(1, screamCount);
                int gapStart = Mathf.RoundToInt(Mathf.Repeat(gapAngle, 360f) / step) - screamGap / 2;

                for (int i = 0; i < screamCount; i++)
                {
                    int fromGap = (int)Mathf.Repeat(i - gapStart, screamCount);
                    if (fromGap < screamGap) continue;
                    SpawnMissile(center, Quaternion.Euler(0f, 0f, i * step) * Vector3.down, screamSpeed);
                }
                if (ring < rings - 1) yield return new WaitForSeconds(screamRingDelay);
            }
            movementHolds--;
            yield return new WaitForSeconds(Wait(0.5f));
        }

        // Огоньки вылетают веером из босса и медленно преследуют корабль; их можно сбить
        IEnumerator WispsAttack()
        {
            if (wispPrefab == null) yield break;
            int count = Raging ? rageWispCount : wispCount;
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                Vector2 heading = Quaternion.Euler(0f, 0f, Mathf.Lerp(-70f, 70f, t)) * Vector3.down;
                Wisp wisp = Instantiate(wispPrefab, Muzzle, Quaternion.identity);
                wisp.explosionPrefab = explosionPrefab;
                wisp.Init(player, heading);
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(Wait(1f));
        }

        // HANDS OFF: за бой были руки, и ни одна не поймала корабль
        int grabsMade;
        bool grabCaught;
        public void OnGrabCaughtPlayer() => grabCaught = true;

        // Хватка: в случайных местах на полу мерцают метки (не вплотную друг к другу),
        // через секунду из них растут руки
        IEnumerator GrabAttack()
        {
            if (player == null || grabHandSprite == null) yield break;
            int count = Raging ? rageGrabCount : grabCount;
            float floorY = player.transform.position.y - grabFloorOffset;
            var placed = new System.Collections.Generic.List<float>();
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(player.MinX, player.MaxX);
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    bool free = true;
                    foreach (float other in placed)
                        if (Mathf.Abs(other - x) < grabRadius * grabMinGap) free = false;
                    if (free) break;
                    x = Random.Range(player.MinX, player.MaxX);
                }
                placed.Add(x);
                grabsMade++;
                GhostGrab.Create(this, grabMarkSprite, grabHandSprite, new Vector2(x, floorY), player,
                    grabWarn, grabHold, grabRadius, grabHandScale);
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(Wait(grabWarn + 0.5f));
        }

        GameObject drainBeam;

        // Луч от босса к кораблю: урона нет, корабль вязнет, а босс в это время стреляет веерами
        IEnumerator DrainAttack()
        {
            if (player == null) yield break;
            movementHolds++;

            drainBeam = new GameObject("DrainBeam", typeof(SpriteRenderer));
            var beam = drainBeam.GetComponent<SpriteRenderer>();
            beam.sprite = drainBeamSprite;
            beam.sortingOrder = 4;
            float spriteSize = drainBeamSprite != null ? Mathf.Max(0.0001f, drainBeamSprite.bounds.size.x) : 1f;

            float nextFan = 0.3f;
            for (float t = 0f; t < drainTime; t += Time.deltaTime)
            {
                if (t >= nextFan)
                {
                    FireFan(fanCount, fanAngle);
                    nextFan += Wait(drainFanInterval);
                }

                Vector3 from = Muzzle, to = player.transform.position;
                Vector3 delta = to - from;
                drainBeam.transform.position = (from + to) * 0.5f;
                drainBeam.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
                float wobble = 1f + 0.35f * Mathf.Sin(t * 30f);
                drainBeam.transform.localScale = new Vector3(drainBeamWidth * wobble / spriteSize, delta.magnitude / spriteSize, 1f);
                beam.color = new Color(0.66f, 0.96f, 0.74f, Random.Range(0.45f, 0.8f));

                player.moveMultiplier = drainSlow;
                yield return null;
            }

            EndDrain();
            movementHolds--;
            yield return new WaitForSeconds(Wait(0.4f));
        }

        void EndDrain()
        {
            if (player != null) player.moveMultiplier = 1f;
            if (drainBeam != null) Destroy(drainBeam);
        }

        void OnDestroy()
        {
            EndDrain(); // босса убрали посреди луча - корабль не должен остаться «вязким»
        }

        // Вызывается пулей игрока
        public void Hit()
        {
            if (dead) return;

            health--;
            hits++;
            flashTimer = flashTime;

            if (coinPrefab != null && hits % coinEveryHits == 0)
                Instantiate(coinPrefab, transform.position, Quaternion.identity);

            if (health <= 0)
                Die();
        }

        void Die()
        {
            dead = true;
            StopAllCoroutines();
            SpinvGameManager.Instance.AddScore(points);

            if (grantsAchievement) Achievements.Award(achievement);
            if (grabsMade > 0 && !grabCaught) Achievements.Award(AchievementId.HandsOff);

            foreach (FallingMinion minion in Object.FindObjectsByType<FallingMinion>())
                Destroy(minion.gameObject);
            foreach (LaserBeam beam in Object.FindObjectsByType<LaserBeam>())
                Destroy(beam.gameObject);
            foreach (BossMine mine in Object.FindObjectsByType<BossMine>())
                Destroy(mine.gameObject);
            foreach (PhantomDecoy decoy in Object.FindObjectsByType<PhantomDecoy>())
                Destroy(decoy.gameObject);
            foreach (Wisp wisp in Object.FindObjectsByType<Wisp>())
                Destroy(wisp.gameObject);
            foreach (GhostGrab grab in Object.FindObjectsByType<GhostGrab>())
                Destroy(grab.gameObject);
            foreach (ClusterBomb cluster in Object.FindObjectsByType<ClusterBomb>())
                Destroy(cluster.gameObject);
            EndDrain();
            Visual.SetActive(true);

            if (dramaticDeath)
            {
                StartCoroutine(DramaticDeath());
                return;
            }

            SpinvSoundManager.Play(Sfx.Explosion);
            Explode(transform.position, 4f);
            Died?.Invoke();
            Destroy(gameObject);
        }

        void Explode(Vector3 position, float scale)
        {
            if (explosionPrefab == null) return;
            GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * scale;
        }

        // Финальный босс: замирает и краснеет, пули исчезают, по телу всё чаще рвутся мелкие взрывы
        // и нарастает тряска, затем большой взрыв с тряской экрана и короткая пауза перед победой.
        // Игрок в это время не стреляет
        IEnumerator DramaticDeath()
        {
            if (player != null) player.canShoot = false;
            var hitbox = GetComponent<Collider2D>();
            if (hitbox != null) hitbox.enabled = false;
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
                Destroy(projectile.gameObject);

            var anger = GetComponent<BossAnger>();
            spriteRenderer.color = Color.white; // убрать затемнение от последнего попадания
            Bounds bounds = spriteRenderer.bounds;
            int sounds = 0;

            for (float t = 0f; t < deathChainTime; )
            {
                float k = t / deathChainTime;
                if (anger != null) anger.dyingShake = Mathf.Lerp(0.6f, 2f, k);

                Vector3 point = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x) * 0.85f + bounds.center.x * 0.15f,
                    Random.Range(bounds.min.y, bounds.max.y) * 0.85f + bounds.center.y * 0.15f, 0f);
                Explode(point, Random.Range(1.2f, 2.2f));
                if (sounds++ % 3 == 0) SpinvSoundManager.Play(Sfx.Explosion);

                // Взрывы всё чаще: от 0.28 до 0.07 с
                float wait = Mathf.Lerp(0.28f, 0.07f, k);
                yield return new WaitForSeconds(wait);
                t += wait;
            }

            // Большой взрыв: в центре и вспышки вокруг, босс исчезает, трясётся весь экран
            SpinvSoundManager.Play(Sfx.Explosion);
            Explode(bounds.center, 9f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Explode(bounds.center + new Vector3(Mathf.Cos(a) * bounds.extents.x * 0.8f, Mathf.Sin(a) * bounds.extents.y, 0f), 4f);
            }
            if (anger != null) anger.dyingShake = -1f;
            spriteRenderer.gameObject.SetActive(false);
            yield return ShakeCamera(0.5f, 0.35f);

            yield return new WaitForSeconds(Mathf.Max(0f, deathAfterTime - 0.5f));
            Died?.Invoke();
            Destroy(gameObject);
        }

        static IEnumerator ShakeCamera(float duration, float strength)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            Vector3 home = cam.transform.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float s = strength * (1f - t / duration);
                cam.transform.position = home + new Vector3(Random.Range(-s, s), Random.Range(-s, s), 0f);
                yield return null;
            }
            cam.transform.position = home;
        }
    }
}
