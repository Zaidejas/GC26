using System;
using GC26.Core;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// The GC26 shell's entry point for «Стальной рубеж». Sits on the one object in
    /// gc26_tanki_Main; everything else (camera, light, map, menus) is made in code
    /// under this object, so it all goes away when the shell unloads the scene.
    /// </summary>
    public sealed class TankiGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_tanki";

        GameRunner runner;

        public void Begin(Action<int> exit)
        {
            if (runner != null) return;
            var holder = new GameObject("Steel Line");
            holder.transform.SetParent(transform, false);
            runner = holder.AddComponent<GameRunner>();
            runner.ExitRequested = exit;
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (runner == null && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
        }
    }
}
