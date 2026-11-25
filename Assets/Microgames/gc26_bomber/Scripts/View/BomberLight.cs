using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// «Тёмная станция»: a low-res darkness map over the room (1 texel = 8 world px, bilinear) with holes cut by
    /// light sources — the prototype's darkness mask with destination-out gradients, computed on the CPU so it needs
    /// no custom shader. Drawn above the world sprites and below glows, shots and particles (which stay bright).
    /// </summary>
    public sealed class BomberLight
    {
        const int TexelPx = 8;
        readonly SpriteRenderer _sr;
        Texture2D _tex; Sprite _sprite; float[] _dark; Color32[] _px; int _w, _h;
        Color32 _tint;

        public BomberLight(Transform parent, int sortingOrder)
        {
            var go = new GameObject("gc26_bomber_Darkness"); go.transform.SetParent(parent, false);
            _sr = go.AddComponent<SpriteRenderer>(); _sr.sortingOrder = sortingOrder;
        }

        public bool Enabled { set => _sr.enabled = value; }

        /// <summary>Covers the room plus the shaft below it (viewTotal world px tall).</summary>
        public void Resize(float viewTotal)
        {
            int w = Mathf.CeilToInt(BomberData.W / TexelPx), h = Mathf.CeilToInt(viewTotal / TexelPx);
            if (w == _w && h == _h) return;
            Release();
            _w = w; _h = h; _dark = new float[w * h]; _px = new Color32[w * h];
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "gc26_bomber_darkness" };
            _sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0f, 1f), BomberData.Cell / TexelPx, 0, SpriteMeshType.FullRect);
            _sr.sprite = _sprite;
            _sr.transform.position = new Vector3(0f, BomberData.H / BomberData.Cell, 0f); // top-left corner of the room
        }

        public void Release()
        {
            if (_sprite) Object.Destroy(_sprite);
            if (_tex) Object.Destroy(_tex);
            _sprite = null; _tex = null;
        }

        /// <param name="ambient">darkness where no light reaches (0..1)</param>
        public void Begin(float ambient, Color32 tint)
        {
            _tint = tint;
            for (int i = 0; i < _dark.Length; i++) _dark[i] = ambient;
        }

        /// <summary>Cuts a soft hole: full strength a at the centre, 60% at 0.45 r, nothing at r.</summary>
        public void Add(float x, float y, float r, float a)
        {
            if (_dark == null || r <= 1f || a <= 0f) return;
            float inv = 1f / TexelPx;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((x - r) * inv)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt((x + r) * inv));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((y - r) * inv)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt((y + r) * inv));
            float ir = 1f / r;
            for (int ty = y0; ty <= y1; ty++)
            {
                float dy = (ty + 0.5f) * TexelPx - y; int row = (_h - 1 - ty) * _w;
                for (int tx = x0; tx <= x1; tx++)
                {
                    float dx = (tx + 0.5f) * TexelPx - x, d = Mathf.Sqrt(dx * dx + dy * dy) * ir;
                    if (d >= 1f) continue;
                    float cut = a * (d < 0.45f ? 1f - 0.4f * (d / 0.45f) : 0.6f * (1f - (d - 0.45f) / 0.55f));
                    _dark[row + tx] *= 1f - cut;
                }
            }
        }

        public void Apply()
        {
            if (_tex == null) return;
            for (int i = 0; i < _dark.Length; i++) _px[i] = new Color32(_tint.r, _tint.g, _tint.b, (byte)(Mathf.Clamp01(_dark[i]) * 255f));
            _tex.SetPixels32(_px); _tex.Apply(false, false);
        }
    }
}
