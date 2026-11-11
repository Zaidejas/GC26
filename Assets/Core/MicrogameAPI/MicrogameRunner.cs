using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GC26.Core
{
    /// <summary>
    /// Shell side: loads a game scene additively, waits until the game calls exit
    /// (from its own pause menu or game-over screen), then unloads it.
    /// </summary>
    public class MicrogameRunner : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }

        public MicrogameRegistry Registry { get; private set; }

        void Awake()
        {
            IsRunning = true;
            Registry = MicrogameRegistry.Load();
        }

        void OnDestroy() => IsRunning = false;

        /// <summary>Plays a game until it exits; onDone receives the score (null if the game failed to start).</summary>
        public IEnumerator Play(MicrogameManifest game, Action<int?> onDone)
        {
            yield return SceneManager.LoadSceneAsync(game.entry_scene, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName(game.entry_scene);
            SceneManager.SetActiveScene(scene);

            IMicrogame microgame = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                microgame = root.GetComponentInChildren<IMicrogame>(true);
                if (microgame != null) break;
            }

            int? score = null;
            bool done = false;
            if (microgame == null)
            {
                Debug.LogError($"[GC26] {game.id}: no IMicrogame in scene {game.entry_scene}");
                done = true;
            }
            else
            {
                microgame.Begin(s => { if (!done) { score = s; done = true; } });
            }

            while (!done)
                yield return null;

            yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = 1f;
            onDone?.Invoke(score);
        }
    }
}
