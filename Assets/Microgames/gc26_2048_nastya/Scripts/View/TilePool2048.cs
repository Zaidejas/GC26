using System.Collections.Generic;
using UnityEngine;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Keeps inactive tile views for reuse, so there is no Instantiate/Destroy during play (TDD 6.2).
    /// Owned by <see cref="BoardView2048"/>; the views are children of the board, so they unload with the scene.
    /// </summary>
    public sealed class TilePool2048
    {
        readonly TileView2048 _prefab;
        readonly Transform _parent;
        readonly Stack<TileView2048> _free = new Stack<TileView2048>(16);

        public TilePool2048(TileView2048 prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public void Prewarm(int count)
        {
            while (_free.Count < count)
                _free.Push(Create());
        }

        public TileView2048 Get()
        {
            var view = _free.Count > 0 ? _free.Pop() : Create();
            view.gameObject.SetActive(true);
            return view;
        }

        public void Release(TileView2048 view)
        {
            view.gameObject.SetActive(false);
            _free.Push(view);
        }

        TileView2048 Create()
        {
            var view = Object.Instantiate(_prefab, _parent, false);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
