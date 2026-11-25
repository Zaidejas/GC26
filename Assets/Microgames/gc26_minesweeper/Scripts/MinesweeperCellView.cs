using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Minesweeper
{
    // One square on screen. A finger or left mouse button pressing it starts a press that
    // MinesweeperGame follows until release (BeginPress): the finger may slide to another cell to aim,
    // release = tap, holding still = long press (yellow flag). Right click = yellow flag at once.
    public class MinesweeperCellView : MonoBehaviour, IPointerDownHandler
    {
        // Classic number colours: 1 blue, 2 green, 3 red, 4 dark blue, 5 dark red, 6 teal, 7 black, 8 grey.
        public static readonly Color[] NumberColors =
        {
            new Color32(0x1E, 0x5B, 0xD8, 0xFF), new Color32(0x1F, 0x8A, 0x3B, 0xFF),
            new Color32(0xC6, 0x28, 0x28, 0xFF), new Color32(0x18, 0x2E, 0x7A, 0xFF),
            new Color32(0x7A, 0x1C, 0x1C, 0xFF), new Color32(0x0F, 0x86, 0x86, 0xFF),
            new Color32(0x11, 0x11, 0x11, 0xFF), new Color32(0x66, 0x66, 0x66, 0xFF),
        };
        static readonly Color PressedTint = new Color(0.8f, 0.8f, 0.8f, 1f);

        public int X { get; private set; }
        public int Y { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        MinesweeperGame game;
        Image background, icon;
        Text label;

        // What the cell currently shows (the magnifier copies it).
        public Sprite TileSprite => background.sprite;
        public Sprite IconSprite => icon.enabled ? icon.sprite : null;
        public string NumberText => label.text;
        public Color NumberColor => label.color;

        public void Init(MinesweeperGame owner, int x, int y, Image bg, Image iconImage, Text numberLabel)
        {
            game = owner; X = x; Y = y;
            background = bg; icon = iconImage; label = numberLabel;
        }

        // Shows the cell's state. gameOver: after a loss (show hidden red flags, cross wrong yellow flags).
        // hit: this is the red flag the player tapped.
        public void Show(MinesweeperCell c, bool gameOver, bool hit, MinesweeperSprites s)
        {
            label.text = "";
            icon.enabled = false;
            background.sprite = s.HiddenTile;

            if (!c.IsRevealed)
            {
                if (gameOver && c.IsMine && !c.IsFlagged) { background.sprite = s.OpenTile; SetIcon(s.RedFlag); }
                else if (c.IsFlagged) SetIcon(gameOver && !c.IsMine ? s.WrongFlag : s.YellowFlag);
                return;
            }

            background.sprite = hit ? s.HitTile : s.OpenTile;
            if (c.IsMine) SetIcon(s.RedFlag);
            else if (c.AdjacentMines > 0)
            {
                label.text = c.AdjacentMines.ToString();
                label.color = NumberColors[c.AdjacentMines - 1];
            }
        }

        void SetIcon(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.enabled = true;
        }

        // Darkens the cell while a finger is on it (touch screens have no "hover").
        public void SetPressed(bool pressed) => background.color = pressed ? PressedTint : Color.white;

        public void SetFontSize(int size) => label.fontSize = size;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { game.OnSecondaryClick(X, Y); return; }
            if (e.button == PointerEventData.InputButton.Left) game.BeginPress(this);
        }
    }
}
