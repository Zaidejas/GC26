using UnityEngine;

namespace GC26.Spinv
{
    // Один кирпичик бункера. Ломается от ракет пришельцев и от самих пришельцев.
    // Если над блоком пусто, он рисуется светлой кромкой - так у выбоин тоже появляется край
    public class BunkerBlock : MonoBehaviour
    {
        public BunkerBlock above;
        public BunkerBlock below;
        public Sprite bodySprite;
        public Sprite edgeSprite;

        public void Break()
        {
            gameObject.SetActive(false);
            if (below != null) below.RefreshLook();
        }

        // Способность восстановления возвращает сломанные блоки
        public void Repair()
        {
            gameObject.SetActive(true);
            RefreshLook();
            if (below != null) below.RefreshLook();
        }

        public void RefreshLook()
        {
            if (edgeSprite == null) return;
            bool open = above == null || !above.gameObject.activeSelf;
            GetComponent<SpriteRenderer>().sprite = open ? edgeSprite : bodySprite;
        }
    }
}
