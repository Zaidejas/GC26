using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Bars above an enemy, as in the prototype: HP (green above 50%, yellow above 25%, red below)
    /// and, for enemies with a shield (Warlord), a blue shield bar above it.
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        static readonly Color Back = new(0.13f, 0.13f, 0.13f, 0.9f);
        static readonly Color High = new(0.4f, 0.73f, 0.42f);
        static readonly Color Mid = new(1f, 0.79f, 0.16f);
        static readonly Color Low = new(0.94f, 0.33f, 0.31f);
        static readonly Color ShieldColor = new(0.26f, 0.65f, 0.96f);

        const float Height = 0.08f;
        const float Gap = 0.03f;

        EnemyUnit _unit;
        float _width;
        SpriteRenderer _hpFill;
        SpriteRenderer _shieldFill;

        public static void Attach(EnemyUnit unit, PlaceholderArt art)
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(unit.transform, false);
            // Undo the enemy's own scale so the bar sizes below are in world units.
            float parentScale = Mathf.Max(0.0001f, unit.transform.localScale.x);
            root.transform.localScale = Vector3.one / parentScale;
            float radius = unit.Data.size;
            root.transform.localPosition = new Vector3(0f, (radius + 0.14f) / parentScale, 0f);

            var bar = root.AddComponent<EnemyHealthBar>();
            bar._unit = unit;
            bar._width = Mathf.Max(0.5f, radius * 2f);

            bar.NewBar(art, root.transform, 0f, Back, 40);
            bar._hpFill = bar.NewBar(art, root.transform, 0f, High, 41);
            if (unit.Data.shield > 0f)
            {
                bar.NewBar(art, root.transform, Height + Gap, Back, 40);
                bar._shieldFill = bar.NewBar(art, root.transform, Height + Gap, ShieldColor, 41);
            }
            bar.LateUpdate();
        }

        SpriteRenderer NewBar(PlaceholderArt art, Transform parent, float y, Color color, int order)
        {
            var sr = art.NewShape("Bar", art.Rect, parent, parent.position, 1f, color, order);
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(_width, Height, 1f);
            return sr;
        }

        void LateUpdate()
        {
            if (_unit == null) return;

            float hp = _unit.MaxHp > 0f ? Mathf.Clamp01(_unit.Hp / _unit.MaxHp) : 0f;
            SetFill(_hpFill, hp);
            _hpFill.color = hp > 0.5f ? High : hp > 0.25f ? Mid : Low;

            if (_shieldFill != null)
                SetFill(_shieldFill, _unit.Data.shield > 0f ? Mathf.Clamp01(_unit.Shield / _unit.Data.shield) : 0f);
        }

        /// <summary>Fills from the left edge: k = 0 empty, 1 full.</summary>
        void SetFill(SpriteRenderer fill, float k)
        {
            var t = fill.transform;
            t.localScale = new Vector3(_width * k, Height, 1f);
            t.localPosition = new Vector3(-_width / 2f + _width * k / 2f, t.localPosition.y, 0f);
        }
    }
}
