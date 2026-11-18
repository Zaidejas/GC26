using System.Collections.Generic;
using UnityEngine;

namespace GC26.Tetris.View
{
    /// <summary>
    /// Textures, sprites and clips made in code are not part of the scene, so the GC26 shell's unload would not free them.
    /// Each game instance keeps its own list and frees only that list when it leaves (TetrisGame.OnDestroy).
    /// Per instance, not global: in the editor (no domain reload) or in the shell, a second copy of the game can
    /// exist for a moment, and one copy closing must never take the other's textures with it.
    /// </summary>
    public static class RuntimeAssets
    {
        static readonly Dictionary<Object, List<Object>> created = new Dictionary<Object, List<Object>>();

        /// <summary>The game instance that owns what Keep receives. TetrisGame sets it before it builds its views.</summary>
        public static Object Owner { get; set; }

        // The project enters Play Mode without a domain reload, so statics would otherwise survive from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlayMode()
        {
            created.Clear();
            Owner = null;
        }

        public static T Keep<T>(T asset) where T : Object
        {
            // Never let an "unload unused assets" pass free them under a live game; this class frees them itself.
            asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            Object owner = Owner;
            if (owner == null)
            {
                Debug.LogWarning("[BlockDrop] RuntimeAssets.Keep without an owner: " + asset.name);
                return asset;
            }
            if (!created.TryGetValue(owner, out List<Object> list))
                created[owner] = list = new List<Object>();
            list.Add(asset);
            return asset;
        }

        /// <summary>Destroys everything this owner made, and nothing else.</summary>
        public static void ReleaseAll(Object owner)
        {
            if (!created.TryGetValue(owner, out List<Object> list))
                return;
            foreach (Object asset in list)
            {
                if (asset != null)
                    Object.Destroy(asset);
            }
            created.Remove(owner);
            if (Owner == owner)
                Owner = null;
        }
    }
}
