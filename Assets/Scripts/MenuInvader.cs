using UnityEngine;

// Пришелец-украшение в главном меню: машет «ногами» (два кадра), шагает влево-вправо
// рывками, как строй в игре, и слегка покачивается. Все считают время от Time.time,
// поэтому шагают синхронно без общего менеджера
[RequireComponent(typeof(SpriteRenderer))]
public class MenuInvader : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Время одного шага и смены кадра, секунд")]
    public float stepTime = 0.5f;
    [Tooltip("Длина шага, юнитов")]
    public float stepSize = 0.2f;
    [Tooltip("Сколько шагов в одну сторону")]
    public int stepsPerSide = 4;
    [Tooltip("Покачивание вверх-вниз, юнитов")]
    public float bobHeight = 0.08f;
    [Tooltip("Сдвиг фазы покачивания, чтобы пришельцы не качались в унисон")]
    public float bobPhase;

    SpriteRenderer spriteRenderer;
    Vector3 home;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        home = transform.position;
    }

    void Update()
    {
        int step = Mathf.FloorToInt(Time.time / stepTime);

        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[step % frames.Length];

        // Пила 0..2N превращается в «туда-обратно»: -N..N..-N
        int cycle = stepsPerSide * 2;
        int t = step % (cycle * 2);
        int offset = t < cycle ? t - stepsPerSide : 3 * stepsPerSide - t;

        float bob = Mathf.Sin(Time.time * 2.5f + bobPhase) * bobHeight;
        transform.position = home + new Vector3(offset * stepSize, bob, 0f);
    }
}
