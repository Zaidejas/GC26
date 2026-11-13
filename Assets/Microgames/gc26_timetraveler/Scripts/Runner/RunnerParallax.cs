using System.Collections.Generic;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Background layers that scroll slower than the track (parallax), so the run feels deep and fast.
    /// Each layer is an art slot (runner_layer_far / _mid / _near, tiled sideways, any width, height up to 480 px);
    /// until the art exists, simple silhouettes (hills, ridges, bushes) are generated from boxes.
    /// Colours follow the current world (warm in the PAST, cold in the FUTURE).
    /// </summary>
    public class RunnerParallax : MonoBehaviour
    {
        class Layer
        {
            public string Slot;
            public float Factor;          // 0 = glued to the camera (infinitely far), 1 = moves with the track
            public float BaseY;           // world height of the layer's bottom, relative to the camera's bottom edge
            public Color Past, Future;
            public Transform Root;
            public float TileWidth;
            public readonly List<SpriteRenderer> Renderers = new List<SpriteRenderer>();
        }

        readonly List<Layer> _layers = new List<Layer>();
        Camera _camera;
        RunnerShapes _shapes;
        System.Random _rng;

        int _era;

        /// <param name="hills">The far layer's colour in the PAST world for this timeline (nearer layers are darker).</param>
        public void Init(Camera cam, RunnerShapes shapes, int seed, Color hills, int era)
        {
            _camera = cam;
            _shapes = shapes;
            _era = era;
            _rng = new System.Random(seed);
            Add(TimetravelerArt.RunnerLayerFar, 0.1f, 6f, hills, new Color(0.12f, 0.18f, 0.30f),
                height: 9f, bumps: 5, bumpWidth: 7f, order: -90);
            Add(TimetravelerArt.RunnerLayerMid, 0.3f, 3f, hills * 0.8f, new Color(0.09f, 0.15f, 0.25f),
                height: 6f, bumps: 8, bumpWidth: 4f, order: -80);
            Add(TimetravelerArt.RunnerLayerNear, 0.6f, 0.5f, hills * 0.63f, new Color(0.07f, 0.12f, 0.2f),
                height: 3f, bumps: 12, bumpWidth: 2f, order: -70);
        }

        void Add(string slot, float factor, float baseY, Color past, Color future, float height, int bumps,
            float bumpWidth, int order)
        {
            var layer = new Layer { Slot = slot, Factor = factor, BaseY = baseY, Past = past, Future = future };
            layer.Root = new GameObject("Parallax_" + slot).transform;
            layer.Root.SetParent(transform, false);

            var art = TimetravelerArt.EraSprite(slot, _era);
            if (art != null)
            {
                // Artist's strip: tiled sideways, bottom at BaseY.
                layer.TileWidth = art.bounds.size.x;
                for (var i = 0; i < 3; i++)
                {
                    var go = new GameObject("Tile");
                    go.transform.SetParent(layer.Root, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art;
                    sr.sharedMaterial = _shapes.Material;
                    sr.sortingOrder = order;
                    go.transform.localPosition = new Vector3(i * layer.TileWidth, art.bounds.size.y / 2f - art.bounds.center.y, 0f);
                    layer.Renderers.Add(sr);
                }
            }
            else
            {
                // Placeholder: a strip of rounded bumps (hills / ridges / bushes), repeated three times.
                layer.TileWidth = 40f;
                for (var copy = 0; copy < 3; copy++)
                {
                    var x = 0f;
                    var r = new System.Random(_rng.Next());
                    for (var i = 0; i < bumps && x < layer.TileWidth; i++)
                    {
                        var w = bumpWidth * (0.7f + (float)r.NextDouble() * 0.8f);
                        var h = height * (0.5f + (float)r.NextDouble() * 0.5f);
                        if (x + w > layer.TileWidth) w = layer.TileWidth - x;
                        var sr = _shapes.Visual(layer.Root, "Bump", new Vector2(w, h), Color.white, order);
                        sr.transform.localPosition = new Vector3(copy * layer.TileWidth + x + w / 2f, h / 2f, 0f);
                        layer.Renderers.Add(sr);
                        // a smaller cap on top makes the silhouette less boxy
                        var cap = _shapes.Visual(layer.Root, "Cap", new Vector2(w * 0.55f, h * 0.25f), Color.white, order);
                        cap.transform.localPosition = new Vector3(copy * layer.TileWidth + x + w / 2f, h + h * 0.125f, 0f);
                        layer.Renderers.Add(cap);
                        x += w;
                    }
                    // fill below the bumps down to the ground
                    var fill = _shapes.Visual(layer.Root, "Fill", new Vector2(layer.TileWidth, 30f), Color.white, order);
                    fill.transform.localPosition = new Vector3(copy * layer.TileWidth + layer.TileWidth / 2f, -15f, 0f);
                    layer.Renderers.Add(fill);
                }
            }
            _layers.Add(layer);
        }

        public void SetWorld(RunnerWorld world)
        {
            foreach (var layer in _layers)
            {
                var c = world == RunnerWorld.Future ? layer.Future : layer.Past;
                // Artist art keeps its own colours, only lightly tinted.
                if (TimetravelerArt.EraSprite(layer.Slot, _era) != null) c = Color.Lerp(Color.white, c, 0.25f);
                foreach (var sr in layer.Renderers) sr.color = c;
            }
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            var cam = _camera.transform.position;
            var bottom = cam.y - _camera.orthographicSize;
            foreach (var layer in _layers)
            {
                // The layer moves Factor as fast as the world: offset = camera x * (1 - Factor), wrapped by tile width.
                var scroll = cam.x * layer.Factor;
                var offset = Mathf.Repeat(scroll, layer.TileWidth);
                var left = cam.x - _camera.orthographicSize * _camera.aspect - offset;
                // Far layers also move less vertically.
                var y = Mathf.Lerp(bottom, 0f, layer.Factor) + layer.BaseY * (1f - layer.Factor * 0.5f);
                layer.Root.position = new Vector3(left, y, 0f);
            }
        }
    }
}
