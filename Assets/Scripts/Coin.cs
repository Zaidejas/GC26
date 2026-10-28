using UnityEngine;

// Монетка: выпадает из пришельца, падает вниз, игрок ловит её кораблём
public class Coin : MonoBehaviour
{
    public int value = 1;
    public float fallSpeed = 2.5f;
    public float spinSpeed = 5f;
    public float destroyBelowY = -7f;

    Vector3 baseScale;
    bool collected;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        // «Вращение» - сжимаем спрайт по X
        float spin = Mathf.Max(0.15f, Mathf.Abs(Mathf.Cos(Time.time * spinSpeed)));
        transform.localScale = new Vector3(baseScale.x * spin, baseScale.y, baseScale.z);

        if (transform.position.y < destroyBelowY)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !other.TryGetComponent(out PlayerController _)) return;

        collected = true;
        GameManager.Instance.AddCoins(value);
        SoundManager.Play(Sfx.Coin);
        Destroy(gameObject);
    }
}
