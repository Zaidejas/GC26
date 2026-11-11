using System.Collections.Generic;
using UnityEngine;

namespace GC26.Tetris.View
{
    /// <summary>
    /// Textures, sprites and clips made in code are not part of the scene, so the GC26 shell's unload would not free them.
    /// Everything made here is kept in this list and destroyed when the game leaves (TetrisGame.OnDestroy).
    /// </summary>
    public static class RuntimeAssets
    {
        static readonly List<Object> created = new List<Object>();

        public static T Keep<T>(T asset) where T : Object
        {
            created.Add(asset);
            return asset;
        }

        public static void ReleaseAll()
        {
            foreach (Object asset in created)
            {
                if (asset != null)
                    Object.Destroy(asset);
            }
            created.Clear();
            SpecialIcons.Forget();
        }
    }
}
