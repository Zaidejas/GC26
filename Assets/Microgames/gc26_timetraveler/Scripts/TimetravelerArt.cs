using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Art slots: every visible thing asks for a picture by slot name. If the PNG is missing,
    /// the game keeps drawing its coloured placeholder box, so art can be added one file at a time.
    /// Files go to Resources/gc26_timetraveler/Slots/&lt;slot&gt;.png (see the README there for sizes).
    /// </summary>
    public static class TimetravelerArt
    {
        // ---- runner ----
        public const string Ground = "ground";            // tiled
        public const string Ledge = "ledge";              // tiled
        public const string Rock = "rock";
        public const string Log = "log";
        public const string TallWall = "tallwall";
        public const string Bridge = "bridge";            // tiled, tinted with the world colour
        public const string WorldWall = "worldwall";      // tiled, tinted with the world colour
        public const string Blade = "blade";
        public const string Flyer = "flyer";
        public const string Boulder = "boulder";
        public const string Crusher = "crusher";
        public const string Coin = "coin";
        public const string Finish = "finish";
        public const string RunnerBackgroundPast = "runner_bg_past";
        public const string RunnerBackgroundFuture = "runner_bg_future";

        // ---- world map ----
        public const string MapGround = "map_ground";     // tiled
        public const string MapPath = "map_path";         // tiled
        public const string MapNode = "map_node";
        public const string MapNodeDone = "map_node_done";
        public const string MapNodeLocked = "map_node_locked";
        public const string MapGate = "map_gate";         // tiled
        public const string MapPlayer = "map_player";
        public const string MapNpc = "map_npc";
        public const string MapShop = "map_shop";
        public const string MapMinigame = "map_minigame";

        // ---- menu ----
        public const string MenuBackground = "menu_background";
        public const string MenuLogo = "menu_logo";

        const string Folder = TimetravelerGame.Id + "/Slots/";

        /// <summary>The slot's sprite, or null if the artist has not added it yet.</summary>
        public static Sprite Sprite(string slot) => Resources.Load<Sprite>(Folder + slot);

        /// <summary>The slot's whole texture (for screen-space UI), or null.</summary>
        public static Texture2D Texture(string slot)
        {
            var sprite = Sprite(slot);
            return sprite != null ? sprite.texture : null;
        }

        /// <summary>
        /// Puts the slot's picture on a placeholder box (a unit sprite scaled to its size).
        /// The box's own renderer is hidden and a child "Art" renderer is drawn at exactly the box size,
        /// so colliders and gameplay sizes never depend on the art. Returns false if there is no art yet.
        /// </summary>
        public static bool Dress(GameObject box, string slot, bool tile = false, bool tint = false)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            var sprite = Sprite(slot);
            if (sprite == null) return false;
            var placeholder = box.GetComponent<SpriteRenderer>();

            var art = new GameObject("Art");
            art.transform.SetParent(box.transform, false);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (placeholder != null)
            {
                sr.sharedMaterial = placeholder.sharedMaterial;
                sr.sortingOrder = placeholder.sortingOrder;
                sr.color = tint ? placeholder.color : Color.white;
                placeholder.enabled = false;
            }
            art.AddComponent<SlotArt>().Setup(sr, tile);
            return true;
        }

        /// <summary>The renderer the player actually sees on a box: its art if dressed, else the box itself.</summary>
        public static SpriteRenderer ShownRenderer(GameObject box)
        {
            var art = box.transform.Find("Art");
            if (art != null && art.TryGetComponent<SpriteRenderer>(out var sr)) return sr;
            return box.GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>Keeps a slot picture exactly the size of its (scaled) parent box, stretched or tiled.</summary>
    public class SlotArt : MonoBehaviour
    {
        SpriteRenderer _renderer;
        bool _tile;
        Vector3 _lastParentScale;

        public void Setup(SpriteRenderer sr, bool tile)
        {
            _renderer = sr;
            _tile = tile;
            if (tile) _renderer.drawMode = SpriteDrawMode.Tiled;
            Fit();
        }

        void LateUpdate()
        {
            // The ground box is stretched while the track grows, so tiled art follows it.
            if (_tile && transform.parent != null && transform.parent.localScale != _lastParentScale) Fit();
        }

        void Fit()
        {
            var parentScale = transform.parent != null ? transform.parent.localScale : Vector3.one;
            _lastParentScale = parentScale;
            var bounds = _renderer.sprite.bounds;
            if (_tile)
            {
                // Undo the parent's scale; draw tiles over the box's world size.
                transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
                _renderer.size = new Vector2(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y));
                transform.localPosition = Vector3.zero;
            }
            else
            {
                // Stretch the picture over the unit box (any pivot the artist chose).
                transform.localScale = new Vector3(1f / bounds.size.x, 1f / bounds.size.y, 1f);
                transform.localPosition = new Vector3(-bounds.center.x / bounds.size.x, -bounds.center.y / bounds.size.y, 0f);
            }
        }
    }
}
