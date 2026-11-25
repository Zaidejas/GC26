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
        public BomberDebugLog Log { get; private set; }
        public BomberAudio Audio { get; private set; }

        public void Init(BomberGame game)
        {
            Game = game;
            Log = new BomberDebugLog(() => World != null ? World.DebugState : "starting");
            Log.Attach();
            Art = new BomberArt();
            World = new BomberWorld { Meta = BomberMeta.Load() };
            World.MetaChanged += () => World.Meta.Save();
            View = Spawn("gc26_bomber_View").AddComponent<BomberView>();
            View.Init(this);
            UI = Spawn("gc26_bomber_UI").AddComponent<BomberPhoneUI>();
            UI.Init(this, View.Target);
            World.Toast += (m, t) => UI.Toast(m, t);
            World.Shake += a => View.AddShake(a);
            Audio = Spawn("gc26_bomber_Audio").AddComponent<BomberAudio>(); Audio.Init();
            World.Sfx += n => Audio.Play(n);
            if (FindAnyObjectByType<AudioListener>() == null) View.Cam.gameObject.AddComponent<AudioListener>();
            Log.ErrorLogged += _ => UI.OnBugLogged();
            if (World.Meta.TutorialDone) World.EnterHub(); else World.EnterTutorial();
        }

        /// <summary>Straight into a new run (end screen "Новый забег").</summary>
        public void Restart() => World.StartRun((uint)Random.Range(1, int.MaxValue));

        public void ExitToMenu() { World.Meta.Save(); Game.ExitToMenu(Mathf.Max(World.Score, 0)); }

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
            // a bug in one frame must not freeze the game: log it (with the game state) and keep going
            try
            {
                var intent = UI.ReadIntent();
                if (!UI.Paused) World.Tick(Mathf.Min(Time.deltaTime, 1f / 30f), intent);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            try { Audio.SetTheme(World.Mode == BomberMode.Hub ? 0 : World.Sector + 1); } catch (System.Exception e) { Debug.LogException(e); }
            try { View.Sync(); } catch (System.Exception e) { Debug.LogException(e); }
            try { UI.Sync(Time.unscaledDeltaTime); } catch (System.Exception e) { Debug.LogException(e); }
        }

        void OnDisable() => Time.timeScale = 1f;
        void OnDestroy() { Log?.Detach(); Art?.Dispose(); }
    }
}
