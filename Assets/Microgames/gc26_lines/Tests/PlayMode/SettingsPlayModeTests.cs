using System.Collections;
using System.IO;
using GC26.Lines.Game;
using GC26.Lines.Services;
using GC26.Lines.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>Settings in the real game scene: the gears, the language, and How to play from Settings.</summary>
    public class SettingsPlayModeTests : PlayModeTestBase
    {
        static IEnumerator Launch()
        {
            yield return LoadGameScene();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gear_OpensSettings_FromTheMenuAndTheGame()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();

            game.Menu.SettingsButton.Click();
            Assert.IsTrue(game.Hud.Settings.IsOpen, "From the menu");
            game.Hud.Settings.Close();
            yield return new WaitForSeconds(0.2f);

            game.Menu.NewGameButton.Click();
            yield return new WaitForSeconds(1.2f); // the entrance plays
            game.Hud.SettingsButton.Click();
            Assert.IsTrue(game.Hud.Settings.IsOpen, "From the game");
            Assert.IsTrue(game.InputBlocked, "The board doesn't take taps behind Settings");
        }

        /// <summary>What a finger at <paramref name="button"/>'s centre would touch first (the top raycast hit).</summary>
        static GameObject TopHitAt(PillButton button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = screen }, hits);
            foreach (var h in hits) Debug.Log("Lines test: hit " + h.gameObject.name + " (canvas " + h.gameObject.GetComponentInParent<Canvas>().name + ", order " + h.sortingOrder + ", depth " + h.depth + ")");
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        [UnityTest]
        public IEnumerator Gears_AreReallyTappable()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            yield return null;
            var hit = TopHitAt(game.Menu.SettingsButton);
            Assert.IsNotNull(hit);
            Assert.IsTrue(hit.transform.IsChildOf(game.Menu.SettingsButton.transform), "A finger on the menu's gear touches the gear, not " + hit.name);

            game.Menu.NewGameButton.Click();
            yield return new WaitForSeconds(1.2f);
            hit = TopHitAt(game.Hud.SettingsButton);
            Assert.IsTrue(hit.transform.IsChildOf(game.Hud.SettingsButton.transform), "The game's gear too, not " + hit.name);
        }

        [UnityTest]
        public IEnumerator Russian_ChangesEveryScreen_AndIsRemembered()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.SettingsButton.Click();

            game.Hud.Settings.ChooseLanguage(Language.Russian);
            yield return null;
            Assert.AreEqual(Language.Russian, Texts.Current);
            Assert.AreEqual("Новая игра", game.Menu.NewGameButton.Text, "The menu behind changes at once");
            Assert.AreEqual("Настройки", game.Hud.Settings.Title);
            Assert.AreEqual("Заново", game.Hud.ResetButton.Text);
            Assert.AreEqual("ru", new SettingsService(SaveFolder).Load().language, "Saved straight away");

            game.Hud.Settings.MusicSwitch.Click();
            Assert.IsFalse(new SettingsService(SaveFolder).Load().music, "Switches are saved too");

            game.Hud.Settings.ChooseLanguage(Language.English);
            yield return null;
            Assert.AreEqual("New game", game.Menu.NewGameButton.Text);
        }

        [UnityTest]
        public IEnumerator HowToPlay_FromSettingsDuringAGame_ReturnsToTheGame()
        {
            yield return Launch();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            game.Menu.NewGameButton.Click();
            yield return new WaitForSeconds(1.2f);

            game.Hud.SettingsButton.Click();
            game.Hud.Settings.HowToPlayButton.Click();
            yield return null;
            Assert.IsTrue(game.HowToPlay.IsShowing);
            Assert.IsFalse(game.GameScreenShowing);

            game.HowToPlay.BackButton.Click();
            yield return null;
            Assert.IsTrue(game.GameScreenShowing, "Back to the game, not the menu");
            Assert.IsFalse(game.Menu.IsShowing);

            // From the menu it still returns to the menu.
            game.Hud.BackButton.Click();
            yield return null;
            game.Menu.HowToPlayButton.Click();
            yield return null;
            game.HowToPlay.BackButton.Click();
            yield return null;
            Assert.IsTrue(game.Menu.IsShowing);
        }
    }
}
