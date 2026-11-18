#if DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using GC26.Tanki.Rules;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// Development builds only. Run with "-touchtest": taps every menu and result-screen button with a
    /// virtual finger through the Input System, the way the editor's Device Simulator does, and logs
    /// "TOUCHTEST ok/FAIL" for each step, then "TOUCHTEST done passed/failed" and quits.
    /// </summary>
    public sealed class TouchTest : MonoBehaviour
    {
        GameRunner game;
        Touchscreen finger;
        int passed;
        int failed;
        int touchId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-touchtest") < 0) return;
            Application.runInBackground = true;
            new GameObject("TouchTest").AddComponent<TouchTest>();
        }

        IEnumerator Start()
        {
            while ((game = FindAnyObjectByType<GameRunner>()) == null) yield return null;
            while (game.OnSplash) yield return null;
            finger = InputSystem.AddDevice<Touchscreen>();
            game.Save.UnlockedLevel = Levels.All.Length - 1;
            game.Save.AssaultUnlocked = Levels.Assault.Length - 1;
            game.Save.Parts = 1000;
            yield return new WaitForSeconds(0.5f);

            // Title page and every page behind it.
            yield return Tap("НАСТРОЙКИ", () => game.Page == Page.Settings);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("ГАРАЖ", () => game.Page == Page.Garage);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("МАСТЕРСКАЯ", () => game.Page == Page.Workshop);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("НАГРАДЫ", () => game.Page == Page.Awards);
            yield return Tap(">", () => true);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("ШТУРМ", () => game.Page == Page.Assault);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("КАМПАНИЯ", () => game.Page == Page.LevelSelect);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);

            // Assault: won, then next fortress, then won again and back to the menu.
            yield return Tap("ШТУРМ", () => game.Page == Page.Assault);
            yield return Tap("1", () => game.Page == Page.Game && game.Sim.Mode == GameMode.Assault);
            yield return End(GamePhase.LevelCleared);
            yield return Tap("ДАЛЬШЕ", () => game.Sim.Phase == GamePhase.Playing && game.Sim.LevelIndex == 1);
            yield return End(GamePhase.LevelCleared);
            yield return Tap("В МЕНЮ", () => game.Page == Page.Menu);

            // Campaign: lost, again, lost, garage; won, next; pause and leave.
            yield return Tap("КАМПАНИЯ", () => game.Page == Page.LevelSelect);
            yield return Tap("1", () => game.Page == Page.Game && game.Sim.Mode == GameMode.Campaign);
            yield return End(GamePhase.GameOver);
            yield return Tap("ЕЩЁ РАЗ", () => game.Sim.Phase == GamePhase.Playing);
            yield return End(GamePhase.GameOver);
            yield return Tap("ГАРАЖ", () => game.Page == Page.Garage);
            yield return Tap("НАЗАД", () => game.Page == Page.Menu);
            yield return Tap("КАМПАНИЯ", () => game.Page == Page.LevelSelect);
            yield return Tap("1", () => game.Page == Page.Game);
            yield return End(GamePhase.LevelCleared);
            yield return Tap("ДАЛЬШЕ", () => game.Sim.Phase == GamePhase.Playing && game.Sim.LevelIndex == 1);
            yield return Tap("II", () => game.Paused);
            yield return Tap("ПРОДОЛЖИТЬ", () => !game.Paused);
            yield return Tap("II", () => game.Paused);
            yield return Tap("В МЕНЮ", () => game.Page == Page.Menu);

            // Survival: a cleared wave offers upgrades; then lost and back to the menu.
            yield return Tap("ВЫЖИВАНИЕ", () => game.Page == Page.Game && game.Sim.Mode == GameMode.Survival);
            yield return End(GamePhase.GameOver);
            yield return Tap("В МЕНЮ", () => game.Page == Page.Menu);

            // A finger still on the stick when the battle ends (as in real play) must not block the result buttons.
            yield return Tap("ШТУРМ", () => game.Page == Page.Assault);
            yield return Tap("1", () => game.Page == Page.Game && game.Sim.Mode == GameMode.Assault);
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = 900, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(Screen.width * 0.2f, Screen.height * 0.12f), pressure = 1f });
            yield return new WaitForSeconds(0.3f);
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = 900, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = new Vector2(Screen.width * 0.22f, Screen.height * 0.12f), pressure = 1f });
            yield return End(GamePhase.LevelCleared);
            yield return Tap("ДАЛЬШЕ", () => game.Sim.Phase == GamePhase.Playing && game.Sim.LevelIndex == 1);
            // The finger left from the last battle must not drive the tank in the next one.
            yield return new WaitForSeconds(0.3f);
            Check("stick let go after the battle", game.Controls.Direction == null && game.Controls.KnobOffset == Vector2.zero);
            yield return End(GamePhase.LevelCleared);
            yield return Tap("В МЕНЮ", () => game.Page == Page.Menu);
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = 900, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(Screen.width * 0.22f, Screen.height * 0.12f) });
            yield return new WaitForSeconds(0.3f);
            yield return Tap("КАМПАНИЯ", () => game.Page == Page.LevelSelect);

            Debug.Log("TOUCHTEST done passed=" + passed + " failed=" + failed);
            Application.Quit();
        }

        void Check(string what, bool ok)
        {
            if (ok) passed++; else failed++;
            Debug.Log("TOUCHTEST " + (ok ? "ok" : "FAIL") + " " + what);
        }

        /// <summary>Ends the battle the given way, as if it had been played out.</summary>
        IEnumerator End(GamePhase phase)
        {
            typeof(GameSim).GetProperty("Phase").SetValue(game.Sim, phase);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Tap(string button, System.Func<bool> expected)
        {
            HudPointer.Drawn.Clear();
            yield return null;
            yield return null;
            if (!HudPointer.Drawn.TryGetValue(button, out Rect rect))
            {
                failed++;
                Debug.Log("TOUCHTEST FAIL no button '" + button + "' on page " + game.Page + " phase " + game.Sim.Phase);
                yield break;
            }
            var at = new Vector2(rect.center.x, Screen.height - rect.center.y);
            touchId++;
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = touchId, phase = UnityEngine.InputSystem.TouchPhase.Began, position = at, pressure = 1f });
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = touchId, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = at });
            yield return new WaitForSeconds(0.5f);
            bool ok = expected();
            if (ok) passed++; else failed++;
            Debug.Log("TOUCHTEST " + (ok ? "ok" : "FAIL") + " '" + button + "' -> page " + game.Page + " phase " + game.Sim.Phase + " level " + game.Sim.LevelIndex);
        }
    }
}
#endif
