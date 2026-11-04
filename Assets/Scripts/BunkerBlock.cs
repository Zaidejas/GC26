using UnityEngine;

// Один кирпичик бункера. Ломается от ракет пришельцев и от самих пришельцев
public class BunkerBlock : MonoBehaviour
{
    public void Break()
    {
        gameObject.SetActive(false);
    }

    // Способность восстановления возвращает сломанные блоки
    public void Repair()
    {
        gameObject.SetActive(true);
    }
}
