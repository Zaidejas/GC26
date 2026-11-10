using UnityEngine;
using UnityEngine.UI;

namespace Lines.View
{
    /// <summary>
    /// The dim magic forest behind every screen (GDD → Art), exported from the prototype. The start screen
    /// shows the version with tall glowing mushrooms; the game, How to play and Leaderboard show the plain
    /// one, so nothing sits behind the board or the buttons. The picture covers the whole screen (cropped
    /// at the top and bottom on 16:9, at the sides on 20:9), like CSS "background-size: cover".
    /// </summary>
    public sealed class ForestBackground : MonoBehaviour
    {
        public const string MenuArt = "Backgrounds/forest_menu", PlainArt = "Backgrounds/forest_plain";

        Image image;
        Sprite menuSprite, plainSprite;
        Vector2 screenSize = new Vector2(1080, 1920);

        public bool ShowsMenuVersion { get; private set; }
        public Sprite Current => image.sprite;

        public static ForestBackground Create(Camera camera)
        {
            var go = new GameObject("Forest background");
            var view = go.AddComponent<ForestBackground>();
            view.Build(camera);
            return view;
        }

        void Build(Camera camera)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 9f;
            canvas.sortingOrder = -100; // behind the board and every other screen
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var rt = (RectTransform)new GameObject("Picture", typeof(RectTransform)).transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            menuSprite = Resources.Load<Sprite>(MenuArt);
            plainSprite = Resources.Load<Sprite>(PlainArt);
            image.enabled = menuSprite != null && plainSprite != null; // without the art, the camera colour shows
            Show(menu: true);
        }

        public void ApplyLayout(BoardLayout layout)
        {
            screenSize = layout.ScreenSize;
            Fit();
        }

        /// <summary>The start screen's version (with mushrooms) or the plain one for the other screens.</summary>
        public void Show(bool menu)
        {
            ShowsMenuVersion = menu;
            image.sprite = menu ? menuSprite : plainSprite;
            Fit();
        }

        void Fit()
        {
            if (image.sprite == null) return;
            var art = image.sprite.rect.size;
            float scale = Mathf.Max(screenSize.x / art.x, screenSize.y / art.y);
            image.rectTransform.sizeDelta = art * scale;
            image.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
