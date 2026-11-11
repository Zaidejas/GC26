using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// First test room (30x17 cells, 1 cell = 1 unit): floor, border walls with an exit gap on the right,
    /// and a few covers. The procedural generator from the prototype replaces this in a later step.
    /// </summary>
    public static class BomberRoom
    {
        public static void Build(BomberContext ctx)
        {
            var root = ctx.Spawn("gc26_bomber_Room").transform;
            int w = BomberContext.RoomW, h = BomberContext.RoomH;

            var floor = ctx.Spawn("Floor", root).AddComponent<SpriteRenderer>();
            floor.sprite = ctx.Art.MakeFloor(w, h, BomberArt.Hex("#0d1626"), BomberArt.Hex("#4a6fa5"), 1234);
            floor.transform.position = new Vector3(w / 2f, h / 2f, 1f);
            floor.sortingOrder = -100;

            // border (exit gap at the right wall, rows 7..9 like the prototype)
            Wall(ctx, root, 0, 0, w, 1);
            Wall(ctx, root, 0, h - 1, w, 1);
            Wall(ctx, root, 0, 1, 1, h - 2);
            Wall(ctx, root, w - 1, 1, 1, 6);
            Wall(ctx, root, w - 1, 10, 1, h - 11);
            Wall(ctx, root, w - 1, 7, 1, 3); // closed exit door for now
            // covers
            Wall(ctx, root, 8, 4, 2, 2);
            Wall(ctx, root, 8, 11, 2, 2);
            Wall(ctx, root, 14, 7, 1, 3);
            Wall(ctx, root, 18, 3, 3, 1);
            Wall(ctx, root, 18, 13, 3, 1);
        }

        /// <summary>Wall block in cell coordinates (x, y from the bottom-left corner).</summary>
        static void Wall(BomberContext ctx, Transform root, int x, int y, int cw, int ch)
        {
            var go = ctx.Spawn("Wall", root);
            go.transform.position = new Vector3(x + cw / 2f, y + ch / 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ctx.Art.Wall;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(cw, ch);
            sr.sortingOrder = -50;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cw, ch);
            go.AddComponent<BomberSolid>();
        }
    }
}
