using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Root of one game session. Everything the game creates is a child of this object, so it lives in our
    /// scene and is unloaded with it (no DontDestroyOnLoad, no static state). Owns the simulation (BomberWorld),
    /// the pixel-art view (BomberView) and the phone UI (BomberPhoneUI) and drives them each frame.
    /// </summary>
    public sealed class BomberContext : MonoBehaviour
    {
        public BomberGame Game { get; private set; }
        public BomberArt Art { get; private set; }
        public BomberWorld World { get; private set; }
        public BomberView View { get; private set; }
        public BomberPhoneUI UI { get; private set; }

        public void Init(BomberGame game)
        {
            Game = game;
            Art = new BomberArt();
            World = new BomberWorld();
            View = Spawn("gc26_bomber_View").AddComponent<BomberView>();
            View.Init(this);
            UI = Spawn("gc26_bomber_UI").AddComponent<BomberPhoneUI>();
            UI.Init(this, View.Target);
            World.Toast += (m, t) => UI.Toast(m, t);
            Restart();
        }

        public void Restart() => World.StartRun((uint)Random.Range(1, int.MaxValue));

        public void ExitToMenu() => Game.ExitToMenu(World.Score);

        /// <summary>Creates a child object of the context (so it stays in our scene).</summary>
        public GameObject Spawn(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : transform, false);
            return go;
        }

        void Update()
        {
            if (World == null) return;
            var intent = UI.ReadIntent();
            if (!UI.Paused) World.Tick(Mathf.Min(Time.deltaTime, 1f / 30f), intent);
            View.Sync();
            UI.Sync(Time.unscaledDeltaTime);
        }

        void OnDisable() => Time.timeScale = 1f;
        void OnDestroy() => Art?.Dispose();
    }
}
