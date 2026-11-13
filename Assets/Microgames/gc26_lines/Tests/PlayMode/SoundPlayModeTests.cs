using System.Collections;
using GC26.Lines.Audio;
using GC26.Lines.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GC26.Lines.Tests
{
    /// <summary>The music and sounds in the real game scene, following the Settings switches.</summary>
    public class SoundPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Music_FadesIn_AndTheSwitchesWork()
        {
            yield return LoadGameScene();
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>(), "Something hears the sounds");

            float waited = 0f;
            while (!game.Sounds.Ready && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(game.Sounds.Ready, "The sounds are made within a few seconds");
            Assert.IsNotNull(game.Sounds.MusicClip);
            Assert.IsNotNull(game.Sounds.ClipFor(LinesSound.Select));

            yield return new WaitForSecondsRealtime(LinesAudio.MusicFadeSeconds + 0.3f);
            Assert.AreEqual(1f, game.Sounds.MusicVolume, 1e-3f, "Music is on by default and fades in");

            game.Menu.SettingsButton.Click();
            Assert.Contains(LinesSound.Tap, game.Sounds.Played, "Buttons tick");
            game.Hud.Settings.MusicSwitch.Click();
            yield return new WaitForSecondsRealtime(LinesAudio.MusicFadeSeconds + 0.3f);
            Assert.AreEqual(0f, game.Sounds.MusicVolume, 1e-3f, "Music off fades out");
            Assert.IsFalse(game.Settings.music);

            game.Hud.Settings.SoundsSwitch.Click();
            game.Sounds.Played.Clear();
            game.Hud.Settings.CloseButton.Click();
            Assert.IsEmpty(game.Sounds.Played, "Sounds off: no tick");
        }
    }
}
