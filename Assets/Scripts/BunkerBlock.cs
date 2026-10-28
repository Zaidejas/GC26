using UnityEngine;

// Один кирпичик бункера. Ломается от ракет пришельцев и от самих пришельцев
public class BunkerBlock : MonoBehaviour
{
    public void Break()
    {
        Destroy(gameObject);
    }
}
