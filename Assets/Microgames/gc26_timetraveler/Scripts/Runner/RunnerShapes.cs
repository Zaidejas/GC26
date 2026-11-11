using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Greybox helpers: coloured boxes with colliders, used until real art arrives.
    /// One shared white sprite and unlit material, created once per session object.
    /// </summary>
    public class RunnerShapes
    {
        public static readonly Color GroundColor = new Color(0.36f, 0.30f, 0.24f);
        public static readonly Color LedgeColor = new Color(0.45f, 0.38f, 0.28f);
        public static readonly Color HazardColor = new Color(0.85f, 0.25f, 0.2f);
        public static readonly Color TravelerColor = new Color(0.98f, 0.55f, 0.3f);

        readonly Sprite _sprite;
        readonly Material _material;

        public RunnerShapes()
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // 1 px = 1 unit, so localScale is the size in world units.
            _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Sprites/Default");
            _material = new Material(shader);
        }

        /// <summary>Box whose bottom-left corner is at (x, y).</summary>
        public GameObject Box(Transform parent, string name, float x, float y, float width, float height,
            Color color, bool isTrigger = false, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x + width / 2f, y + height / 2f, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sharedMaterial = _material;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one; // scaled by the transform
            col.isTrigger = isTrigger;
            return go;
        }

        /// <summary>Visual-only box (no collider), centred at (x, y).</summary>
        public SpriteRenderer Visual(Transform parent, string name, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sharedMaterial = _material;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
