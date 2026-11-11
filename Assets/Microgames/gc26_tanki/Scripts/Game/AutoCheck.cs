#if DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// Development builds only. Run with "-autocheck folder": plays for a few seconds,
    /// saves screenshots of the menu and the game, then quits.
    /// </summary>
    public sealed class AutoCheck : MonoBehaviour
    {
        string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-autocheck");
            if (index < 0 || index + 1 >= args.Length) return;
            // The check must not stall when its window loses focus.
            Application.runInBackground = true;
            new GameObject("AutoCheck").AddComponent<AutoCheck>().folder = args[index + 1];
        }

        IEnumerator Start()
        {
            GameRunner game;
            while ((game = FindAnyObjectByType<GameRunner>()) == null) yield return null;

            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "splash.png"));
            while (!game.World.SplashJustFired && game.OnSplash) yield return null;
            yield return new WaitForSeconds(0.14f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "splash_shot.png"));
            while (game.OnSplash) yield return null;
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "menu.png"));
            yield return new WaitForSeconds(0.5f);

            // Every menu page once, with the whole campaign open and parts to spend.
            game.Save.UnlockedLevel = Rules.Levels.All.Length - 1;
            game.Save.Parts = 100;
            foreach (Page page in new[] { Page.LevelSelect, Page.Workshop, Page.Settings })
            {
                game.Open(page);
                yield return new WaitForSeconds(0.4f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "page_" + page + ".png"));
                yield return new WaitForSeconds(0.2f);
            }
            game.Buy(Rules.Stat.Armor);
            Debug.Log("WORKSHOP armour=" + game.Save.Upgrades[0] + " parts=" + game.Save.Parts);
            game.Open(Page.Menu);

            // The fifth level: grenades clear the waves until the boss comes, then the strike hits it.
            game.StartLevel(4);
            float waited = 0f;
            while (game.Sim.Boss == null && waited < 90f && game.Sim.Phase == Rules.GamePhase.Playing)
            {
                game.Sim.Player.ShieldTimer = 9f;
                game.Sim.ApplyBonus(Rules.BonusKind.Bomb);
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            while (game.Sim.Boss != null && game.Sim.Boss.Spawning) yield return null;
            yield return new WaitForSeconds(1.5f);
            Debug.Log("BOSS present=" + (game.Sim.Boss != null) + " hp=" + (game.Sim.Boss != null ? game.Sim.Boss.Hp : 0));
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "boss.png"));
            yield return new WaitForSeconds(0.3f);
            if (game.Sim.Boss != null)
            {
                var boss = game.Sim.Boss;
                game.Sim.Player.ShieldTimer = 9f;
                game.Sim.Player.Dir = boss.X > game.Sim.Player.X + 1f ? Rules.Dir.Right : boss.X < game.Sim.Player.X - 1f ? Rules.Dir.Left : Rules.Dir.Up;
                game.Sim.FillSuper();
                yield return new WaitForSeconds(0.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "super_ready.png"));
                yield return new WaitForSeconds(0.2f);
                game.Sim.UseSuper();
                yield return new WaitForSeconds(0.12f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "super_strike.png"));
                yield return new WaitForSeconds(0.4f);
                Debug.Log("STRIKE boss hp=" + boss.Hp);
            }

            // Survival: clear the first wave and look at the choice.
            game.StartSurvival();
            waited = 0f;
            while (game.Sim.Phase == Rules.GamePhase.Playing && waited < 60f)
            {
                game.Sim.Player.ShieldTimer = 9f;
                game.Sim.ApplyBonus(Rules.BonusKind.Bomb);
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            yield return new WaitForSeconds(0.3f);
            Debug.Log("SURVIVAL phase=" + game.Sim.Phase + " choices=" + string.Join(",", game.Sim.Choices));
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "wave_choice.png"));
            yield return new WaitForSeconds(0.3f);
            if (game.Sim.Choices.Count > 0) game.ChooseUpgrade(game.Sim.Choices[0]);
            Debug.Log("SURVIVAL after choice phase=" + game.Sim.Phase + " wave=" + game.Sim.Wave);

            game.ToMenu();
            game.StartGame();
            yield return new WaitForSeconds(9f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "play.png"));
            yield return new WaitForSeconds(0.5f);

            // Two hits, to see the health bar turn yellow and then red.
            for (int hit = 2; hit >= 1; hit--)
            {
                while (!game.Sim.Player.Alive || game.Sim.Player.Shielded) yield return null;
                game.Sim.Bullets.Add(new Rules.Bullet { OwnerId = -1, X = game.Sim.Player.X, Y = game.Sim.Player.Y, Dir = Rules.Dir.Up, Speed = 1f });
                yield return new WaitForSeconds(Rules.Tuning.RespawnDelay + 0.5f);
                Debug.Log("HEALTH lives=" + game.Sim.Lives + " phase=" + game.Sim.Phase);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "health_" + hit + ".png"));
                yield return new WaitForSeconds(0.3f);
            }

            // Close-ups to check the models: the rig restores position every frame, not angle or zoom.
            Camera view = Camera.main;
            Quaternion rotation = view.transform.rotation;
            view.fieldOfView = 9f;

            view.transform.LookAt(WorldView.ToWorld(5.5f, 11.5f, 0.5f));
            foreach (Rules.BonusKind kind in System.Enum.GetValues(typeof(Rules.BonusKind)))
            {
                game.Sim.PlaceBonus(kind, 5, 11);
                yield return new WaitForSeconds(0.25f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "bonus_" + kind + ".png"));
                yield return new WaitForSeconds(0.15f);
            }

            view.transform.LookAt(WorldView.ToWorld(game.Sim.Player.X, game.Sim.Player.Y));
            game.Sim.ApplyBonus(Rules.BonusKind.Freeze);
            foreach (Rules.Dir dir in new[] { Rules.Dir.Up, Rules.Dir.Down, Rules.Dir.Right, Rules.Dir.Left })
            {
                game.Sim.Player.Dir = dir;
                view.transform.LookAt(WorldView.ToWorld(game.Sim.Player.X, game.Sim.Player.Y));
                yield return new WaitForSeconds(0.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "player_" + dir + ".png"));
                yield return new WaitForSeconds(0.2f);
                if (game.Sim.Enemies.Count == 0) continue;
                var enemy = game.Sim.Enemies[0];
                enemy.Dir = dir;
                view.transform.LookAt(WorldView.ToWorld(enemy.X, enemy.Y));
                yield return new WaitForSeconds(0.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "enemy_" + dir + ".png"));
                yield return new WaitForSeconds(0.2f);
            }
            view.transform.rotation = rotation;
            view.fieldOfView = 36f;

            Debug.Log("AUTOCHECK ok phase=" + game.Sim.Phase + " enemies=" + game.Sim.Enemies.Count + " left=" + game.Sim.EnemiesLeft +
                      " player=" + game.Sim.Player.X + "," + game.Sim.Player.Y + " dir=" + game.Sim.Player.Dir + " lives=" + game.Sim.Lives);
            Application.Quit();
        }
    }
}
#endif
