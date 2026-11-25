using GC26.Tetris.Logic;
using GC26.Tetris.View;
using UnityEngine;

namespace GC26.Tetris
{
    /// <summary>
    /// The tall layout leaves a narrow strip on each side of the well. The left one shows how far the level has got
    /// (lines towards the next one), the right one, under the pause button, the combo building up.
    /// </summary>
    public sealed partial class TetrisGame
    {
        const int LinesPerLevel = 10;
        const int ComboBarFull = 8;

        float shownLevelFill;
        float shownComboFill;

        void UpdateStrips(float deltaTime)
        {
            float level = (game.Lines % LinesPerLevel) / (float)LinesPerLevel;
            // A level-up empties the bar at once instead of draining it backwards.
            shownLevelFill = level < shownLevelFill ? level : Mathf.MoveTowards(shownLevelFill, level, deltaTime * 2f);
            float combo = Mathf.Clamp01(game.Combo / (float)ComboBarFull);
            shownComboFill = Mathf.MoveTowards(shownComboFill, combo, deltaTime * (combo < shownComboFill ? 3f : 2f));
        }

        void DrawSideStrips(float unit)
        {
            if (!HudLayout.Portrait)
                return;
            Color accent = view.AccentColor;

            // Left: the level, filling towards the next one.
            Rect left = HudLayout.LevelStrip;
            DrawStripBar(left, shownLevelFill, accent, unit);
            Label(T("УР", "LV"), left.center.x, left.yMax + 1.05f, unit, 0.3f, TextAnchor.MiddleCenter, Muted);
            Label(game.Level.ToString(), left.center.x, left.yMax + 0.5f, unit, 0.5f, TextAnchor.MiddleCenter, Bright);

            // Right: the combo, warming from gold to red as the chain grows.
            Rect right = HudLayout.ComboStrip;
            Color heat = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.3f, 0.25f), shownComboFill);
            DrawStripBar(right, shownComboFill, heat, unit);
            float multiplier = TetrisMatch.ComboMultiplier(Mathf.Max(1, game.Combo));
            Color textColor = game.Combo >= 1 ? heat : Muted;
            Label("×" + MultiplierText(multiplier), right.center.x, right.yMax + 0.5f, unit, 0.42f * (1f + comboKick * 0.3f), TextAnchor.MiddleCenter, textColor);
        }

        void DrawStripBar(Rect bar, float fill, Color color, float unit)
        {
            Vector2 center = bar.center;
            RoundedWorld(center.x, center.y, bar.width, bar.height, new Color(0.11f, 0.09f, 0.16f, 0.95f), 0.3f, 0f, unit);
            RoundedWorld(center.x, center.y, bar.width, bar.height, new Color(color.r, color.g, color.b, 0.25f), 0.3f, 0.05f, unit);
            float inner = bar.height - 0.2f;
            float height = inner * Mathf.Clamp01(fill);
            if (height < 0.05f)
                return;
            // Fills from the bottom up.
            float y = bar.yMin + 0.1f + height / 2f;
            RoundedWorld(center.x, y, bar.width - 0.2f, height, new Color(color.r, color.g, color.b, 0.85f), 0.22f, 0f, unit);
        }
    }
}
