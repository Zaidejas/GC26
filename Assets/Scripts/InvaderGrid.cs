using System.Collections.Generic;
using UnityEngine;

// Сетка пришельцев: создание по WaveData, движение «змейкой», ускорение, стрельба
public class InvaderGrid : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("0 - верхний ряд, 1 - средние ряды, 2 - нижние ряды")]
    public Invader[] invaderPrefabs;
    public Invader eliteInvaderPrefab;
    [Tooltip("Усиленные ставятся в нижние ряды: стреляют только нижние пришельцы")]
    public int eliteBottomRows = 2;
    public Projectile missilePrefab;
    public PlayerController player;

    [Header("Сетка")]
    public float spacingX = 1.4f;
    public float spacingY = 1.07f;

    [Header("Движение")]
    public float stepDown = 0.35f;
    public float edgePadding = 0.8f;
    [Tooltip("Во сколько раз (дополнительно) ускоряется сетка, когда остаётся последний пришелец")]
    public float speedUpFactor = 0.75f;
    [Tooltip("Если пришельцы опустятся ниже этой линии - игра окончена")]
    public float invadeLineY = -7.2f;

    public event System.Action Cleared;
    public event System.Action ReachedBottom;

    WaveData wave;
    Invader[,] invaders;
    int total;
    int alive;
    Vector3 direction = Vector3.right;
    Vector3 startPosition;
    float minX, maxX;
    float shootTimer;
    bool active;

    void Awake()
    {
        startPosition = transform.position;
    }

    public void Build(WaveData data)
    {
        wave = data;

        foreach (Transform child in transform)
            Destroy(child.gameObject);

        transform.position = startPosition;
        direction = Vector3.right;

        Camera cam = Camera.main;
        minX = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f)).x + edgePadding;
        maxX = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x - edgePadding;

        invaders = new Invader[data.rows, data.columns];
        float startX = -(data.columns - 1) * spacingX / 2f;

        HashSet<int> eliteCells = PickEliteCells(data);

        for (int row = 0; row < data.rows; row++)
        {
            Invader rowPrefab = invaderPrefabs[Mathf.Min(RowType(row, data.rows), invaderPrefabs.Length - 1)];

            for (int col = 0; col < data.columns; col++)
            {
                Invader prefab = eliteCells.Contains(row * data.columns + col) ? eliteInvaderPrefab : rowPrefab;
                Invader invader = Instantiate(prefab, transform);
                invader.transform.localPosition = new Vector3(startX + col * spacingX, -row * spacingY, 0f);
                invader.Init(this, row, col, data.coinDropChance);
                invaders[row, col] = invader;
            }
        }

        total = alive = data.rows * data.columns;
        shootTimer = NextShootDelay();
        active = true;
    }

    HashSet<int> PickEliteCells(WaveData data)
    {
        var cells = new List<int>();
        for (int row = Mathf.Max(0, data.rows - eliteBottomRows); row < data.rows; row++)
        {
            for (int col = 0; col < data.columns; col++)
                cells.Add(row * data.columns + col);
        }

        var picked = new HashSet<int>();
        int count = Mathf.Min(cells.Count, Random.Range(data.eliteCountMin, data.eliteCountMax + 1));
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, cells.Count);
            picked.Add(cells[index]);
            cells.RemoveAt(index);
        }
        return picked;
    }

    // Верхний ряд - самые ценные, затем средние, внизу - самые простые
    static int RowType(int row, int rows)
    {
        if (row == 0) return 0;
        if (row <= (rows - 1) / 2) return 1;
        return 2;
    }

    void Update()
    {
        // invaders пропадает, если скрипты перекомпилировались прямо во время Play
        if (!active || invaders == null) return;

        Move();
        if (!active) return;

        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            Shoot();
            shootTimer = NextShootDelay();
        }
    }

    void Move()
    {
        // Чем меньше осталось пришельцев, тем быстрее они двигаются
        float progress = 1f - (float)alive / total;
        float speed = wave.speed * (1f + progress * speedUpFactor);
        transform.position += direction * speed * Time.deltaTime;

        bool hitEdge = false;
        float lowestY = float.MaxValue;

        foreach (Invader invader in invaders)
        {
            if (invader == null) continue;

            Vector3 pos = invader.transform.position;
            if ((direction.x > 0f && pos.x > maxX) || (direction.x < 0f && pos.x < minX))
                hitEdge = true;
            lowestY = Mathf.Min(lowestY, pos.y);
        }

        if (hitEdge)
        {
            direction = -direction;
            transform.position += Vector3.down * stepDown;
        }

        if (lowestY < invadeLineY)
        {
            active = false;
            ReachedBottom?.Invoke();
        }
    }

    // Стреляет самый нижний пришелец в случайной колонке
    void Shoot()
    {
        int rows = invaders.GetLength(0);
        int cols = invaders.GetLength(1);
        int startCol = Random.Range(0, cols);

        for (int i = 0; i < cols; i++)
        {
            int col = (startCol + i) % cols;
            for (int row = rows - 1; row >= 0; row--)
            {
                Invader invader = invaders[row, col];
                if (invader == null) continue;

                Projectile missile = Instantiate(missilePrefab, invader.transform.position + Vector3.down * 0.55f, Quaternion.identity);
                if (player != null && Random.value < invader.aimedShotChance)
                    AimAtPlayer(missile);
                return;
            }
        }
    }

    void AimAtPlayer(Projectile missile)
    {
        Vector2 toPlayer = (player.transform.position - missile.transform.position).normalized;
        missile.direction = toPlayer;
        missile.transform.rotation = Quaternion.FromToRotation(Vector3.down, toPlayer);
    }

    float NextShootDelay()
    {
        return wave.missileInterval * Random.Range(0.6f, 1.4f);
    }

    public void OnInvaderKilled(Invader invader)
    {
        invaders[invader.Row, invader.Column] = null;
        alive--;

        if (alive <= 0)
        {
            active = false;
            Cleared?.Invoke();
        }
    }
}
