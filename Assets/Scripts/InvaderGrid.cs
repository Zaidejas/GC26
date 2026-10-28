using UnityEngine;

// Сетка пришельцев: создание по WaveData, движение «змейкой», ускорение, стрельба
public class InvaderGrid : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("0 - верхний ряд, 1 - средние ряды, 2 - нижние ряды")]
    public Invader[] invaderPrefabs;
    public Projectile missilePrefab;

    [Header("Сетка")]
    public float spacingX = 1.1f;
    public float spacingY = 0.8f;

    [Header("Движение")]
    public float stepDown = 0.35f;
    public float edgePadding = 0.6f;
    [Tooltip("Во сколько раз (дополнительно) ускоряется сетка, когда остаётся последний пришелец")]
    public float speedUpFactor = 2f;
    [Tooltip("Если пришельцы опустятся ниже этой линии - игра окончена")]
    public float invadeLineY = -4.3f;

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

        for (int row = 0; row < data.rows; row++)
        {
            Invader prefab = invaderPrefabs[Mathf.Min(RowType(row, data.rows), invaderPrefabs.Length - 1)];

            for (int col = 0; col < data.columns; col++)
            {
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

    // Верхний ряд - самые ценные, затем средние, внизу - самые простые
    static int RowType(int row, int rows)
    {
        if (row == 0) return 0;
        if (row <= (rows - 1) / 2) return 1;
        return 2;
    }

    void Update()
    {
        if (!active) return;

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

                Instantiate(missilePrefab, invader.transform.position + Vector3.down * 0.4f, Quaternion.identity);
                return;
            }
        }
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
