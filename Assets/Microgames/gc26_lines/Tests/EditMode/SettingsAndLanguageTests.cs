using System.IO;
using System.Linq;
using GC26.Lines.Core;
using GC26.Lines.Services;
using GC26.Lines.UI;
using GC26.Lines.View;
using NUnit.Framework;
using UnityEngine;

namespace GC26.Lines.Tests
{
    /// <summary>Settings (switches, language, How to play) and the English/Russian texts (GDD → Settings).</summary>
    public class SettingsAndLanguageTests
    {
        Camera camera;
        HudView hud;
        MenuView menu;
        string folder;

        [SetUp]
        public void SetUp()
        {
            Tween.KillAll();
            Texts.Set(Language.English);
            folder = Path.Combine(Path.GetTempPath(), "LinesSettings_" + System.Guid.NewGuid().ToString("N"));
            camera = new GameObject("Camera").AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            Tween.KillAll();
            Texts.Set(Language.English);
            if (hud != null) Object.DestroyImmediate(hud.gameObject);
            if (menu != null) Object.DestroyImmediate(menu.gameObject);
            Object.DestroyImmediate(camera.gameObject);
            try { Directory.Delete(folder, true); } catch { /* best effort */ }
        }

        SettingsDialog NewSettings()
        {
            hud = HudView.Create(camera);
            hud.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            return hud.Settings;
        }

        // ---- Texts ----

        [Test]
        public void EveryText_ExistsInBothLanguages()
        {
            foreach (var key in Texts.Keys)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(Texts.In(Language.English, key)), "English: " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(Texts.In(Language.Russian, key)), "Russian: " + key);
            }
        }

        [Test]
        public void ChangingTheLanguage_TellsTheScreens_OnlyWhenItChanges()
        {
            int changes = 0;
            System.Action count = () => changes++;
            Texts.Changed += count;
            try
            {
                Texts.Set(Language.Russian);
                Texts.Set(Language.Russian);
                Assert.AreEqual(1, changes);
                Assert.AreEqual("Новая игра", Texts.Get("newGame"));
                Texts.Set(Language.English);
                Assert.AreEqual(2, changes);
                Assert.AreEqual("New game", Texts.Get("newGame"));
            }
            finally { Texts.Changed -= count; }
        }

        [Test]
        public void LanguageCodes_AndThePhonesLanguage()
        {
            Assert.AreEqual(Language.Russian, Texts.FromCode("ru"));
            Assert.AreEqual(Language.English, Texts.FromCode("en"));
            Assert.IsNull(Texts.FromCode(""), "Not chosen yet: the phone's language decides");
            Assert.AreEqual(Language.Russian, Texts.FromSystem(SystemLanguage.Russian));
            Assert.AreEqual(Language.English, Texts.FromSystem(SystemLanguage.Lithuanian));
            Assert.AreEqual("ru", Texts.Code(Language.Russian));
        }

        [Test]
        public void ResultLine_AndDates_InRussian()
        {
            Texts.Set(Language.Russian);
            Assert.AreEqual("Новый рекорд, Аня!", Texts.ResultMessage(0, 300, "Аня"));
            Assert.AreEqual("Ты на 3-м месте, Аня!", Texts.ResultMessage(2, 120, "Аня"));
            Assert.AreEqual("Счёт 120 не попал в топ-10", Texts.ResultMessage(-1, 120, "Аня"));
            long march5 = new System.DateTimeOffset(2026, 3, 5, 12, 0, 0, System.TimeSpan.Zero).ToUnixTimeMilliseconds();
            StringAssert.EndsWith("марта", Texts.Date(march5));
            Texts.Set(Language.English);
            StringAssert.EndsWith("Mar", Texts.Date(march5));
        }

        [Test]
        public void PlaceholderNames_FollowTheLanguage_ButArentSaved()
        {
            var board = new Leaderboard { placeholderPrefix = "Игрок" };
            Assert.AreEqual("Игрок1", board.DefaultName);
            var again = JsonUtility.FromJson<Leaderboard>(JsonUtility.ToJson(board));
            Assert.AreEqual("Player1", again.DefaultName, "The word comes from the language, not from the file");
        }

        [Test]
        public void Fonts_HaveRussianLetters()
        {
            Assert.IsNotNull(UiFonts.CyrillicDisplay, "Kurale is there for Russian titles");
            Assert.IsTrue(UiFonts.Display.fallbackFontAssetTable.Contains(UiFonts.CyrillicDisplay), "Mystery Quest falls back to Kurale");
            Assert.IsTrue(UiFonts.Body.HasCharacters("Счёт Игрок", out _, false, true), "Nunito has Cyrillic");
            Assert.IsTrue(UiFonts.CyrillicDisplay.HasCharacters("Настройки", out _, false, true));
        }

        // ---- Saved settings ----

        [Test]
        public void Settings_AreRemembered_AndDefaultsWhenMissingOrDamaged()
        {
            var file = new SettingsService(folder);
            var fresh = file.Load();
            Assert.IsTrue(fresh.music && fresh.sounds && fresh.vibration, "Everything on at first");
            Assert.AreEqual("", fresh.language, "The phone's language until chosen");

            file.Save(new GameSettings { music = false, sounds = true, vibration = false, language = "ru" });
            var back = new SettingsService(folder).Load();
            Assert.IsFalse(back.music);
            Assert.IsTrue(back.sounds);
            Assert.IsFalse(back.vibration);
            Assert.AreEqual("ru", back.language);

            File.WriteAllText(file.FilePath, "{ broken");
            Assert.IsTrue(new SettingsService(folder).Load().music, "A damaged file means the defaults");
        }

        // ---- The Settings pop-up ----

        [Test]
        public void Settings_ShowsTheSwitches_AndReportsEachChange()
        {
            var dialog = NewSettings();
            GameSettings last = null;
            dialog.Changed += s => last = s;
            dialog.Show(new GameSettings { music = true, sounds = false, vibration = true });

            Assert.IsTrue(dialog.IsOpen);
            Assert.AreEqual("Settings", dialog.Title);
            Assert.IsTrue(dialog.MusicSwitch.IsOn);
            Assert.IsFalse(dialog.SoundsSwitch.IsOn);
            Assert.IsTrue(dialog.VibrationSwitch.IsOn);

            dialog.MusicSwitch.Click();
            Assert.IsNotNull(last);
            Assert.IsFalse(last.music, "Music switched off");
            Assert.IsFalse(last.sounds, "The others stay as they were");
            dialog.SoundsSwitch.Click();
            Assert.IsTrue(last.sounds);
        }

        [Test]
        public void Switch_KnobSlidesToTheRightWhenOn()
        {
            var dialog = NewSettings();
            dialog.Show(new GameSettings { music = false });
            float off = dialog.MusicSwitch.KnobX;
            dialog.MusicSwitch.Click();
            Tween.Tick(ToggleSwitch.SlideSeconds + 0.01f);
            Assert.Greater(dialog.MusicSwitch.KnobX, off);
        }

        [Test]
        public void ChoosingRussian_IsReported_AndTheCardKeepsItsSize()
        {
            var dialog = NewSettings();
            GameSettings last = null;
            dialog.Changed += s => last = s;
            dialog.Show(new GameSettings());
            var englishSize = dialog.Card.sizeDelta;

            dialog.ChooseLanguage(Language.Russian);
            Assert.AreEqual("ru", last.language);
            Texts.Set(Language.Russian); // what the game does with it
            hud.RefreshTexts();
            Assert.AreEqual("Настройки", dialog.Title);
            Assert.AreEqual("Как играть", dialog.HowToPlayButton.Text);
            Assert.AreEqual(englishSize, dialog.Card.sizeDelta, "The card doesn't jump when the language changes");
        }

        [Test]
        public void Settings_ClosesWithTheCross_AndHowToPlayClosesIt()
        {
            var dialog = NewSettings();
            dialog.Show(new GameSettings());
            dialog.CloseButton.Click();
            Assert.IsFalse(dialog.IsOpen);

            int howTo = 0;
            dialog.HowToPlayClicked += () => howTo++;
            dialog.Show(new GameSettings());
            dialog.HowToPlayButton.Click();
            Assert.AreEqual(1, howTo);
            Assert.IsFalse(dialog.IsOpen, "Settings closes before How to play opens");
        }

        [Test]
        public void Gears_OnTheGameScreen_AndTheMenu_ArePlainIcons()
        {
            NewSettings();
            Assert.AreEqual("Gear", hud.SettingsButton.IconSprite.name);
            Assert.IsTrue(hud.SettingsButton.IsPlain);
            int opened = 0;
            hud.SettingsClicked += () => opened++;
            hud.SettingsButton.Click();
            Assert.AreEqual(1, opened);

            menu = MenuView.Create(camera);
            var layout = BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920));
            menu.ApplyLayout(layout);
            Assert.AreEqual("Gear", menu.SettingsButton.IconSprite.name);
            Assert.Greater(layout.SettingsButton.x, layout.BackButton.x, "Top-right, opposite the house");
            Assert.AreEqual(layout.BackButton.y, layout.SettingsButton.y);
        }

        [Test]
        public void Menu_AndHud_InRussian()
        {
            menu = MenuView.Create(camera);
            menu.ApplyLayout(BoardLayout.Compute(new Vector2(1080, 1920), new Rect(0, 0, 1080, 1920)));
            menu.Show(120, PieceSet.Balls());
            NewSettings();
            Texts.Set(Language.Russian);
            menu.RefreshTexts();
            hud.RefreshTexts();
            Assert.AreEqual("Новая игра", menu.NewGameButton.Text);
            StringAssert.Contains("Продолжить", menu.ContinueButton.Text);
            StringAssert.Contains("счёт 120", menu.ContinueButton.Text);
            Assert.AreEqual("Собери пять в ряд", menu.Tagline);
            Assert.AreEqual("Заново", hud.ResetButton.Text);
        }
    }
}
