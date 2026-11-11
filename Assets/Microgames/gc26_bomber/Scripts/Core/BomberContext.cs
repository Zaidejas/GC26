using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Root of one game session. Everything the game creates is a child of this object, so it lives in
    /// our scene and is unloaded with it (no DontDestroyOnLoad, no static state). Systems get what they
    /// need from here instead of from singletons.
    /// </summary>
    public sealed class BomberContext : MonoBehaviour
    {
        public const int RoomW = 30, RoomH = 17;

        public BomberGame Game { get; private set; }
        public Camera Cam { get; private set; }
        public BomberArt Art { get; private set; }
        public HeatModel Core { get; private set; }
        public BomberDrone Drone { get; private set; }
        public BomberProjectiles Projectiles { get; private set; }
        public readonly List<BomberTargetDummy> Dummies = new List<BomberTargetDummy>();
        public int Score { get; set; }
        public bool Paused { get; private set; }

        float _shake;
        Vector3 _camBase;

        public void Init(BomberGame game)
        {
            Game = game;
            Art = new BomberArt();
            Core = new HeatModel(HeatSettings.Default);
            Cam = CreateCamera();
            BomberRoom.Build(this);
            Projectiles = Spawn("gc26_bomber_Projectiles").AddComponent<BomberProjectiles>();
            Projectiles.Init(this);
            Drone = BomberDrone.Create(this, new Vector2(2.5f, RoomH / 2f));
            foreach (var p in new[] { new Vector2(20f, 12f), new Vector2(24f, 8.5f), new Vector2(20f, 5f), new Vector2(15f, 13.5f) })
                Dummies.Add(BomberTargetDummy.Create(this, p));
            gameObject.AddComponent<BomberDebugHud>().Init(this);
        }

        /// <summary>Creates a child object of the context (so it stays in our scene).</summary>
        public GameObject Spawn(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent ? parent : transform, false);
            return go;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

        public Vector2 MouseWorld(Vector2 screen)
        {
            var p = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Cam.transform.position.z));
            return p;
        }

        Camera CreateCamera()
        {
            var go = Spawn("gc26_bomber_Camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = RoomH / 2f + 0.25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = (Color)BomberArt.Void;
            cam.depth = 10;
            _camBase = new Vector3(RoomW / 2f, RoomH / 2f, -10f);
            go.transform.position = _camBase;
            return cam;
        }

        void LateUpdate()
        {
            if (!Cam) return;
            _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * 2.5f);
            var off = _shake > 0f && !Paused ? (Vector3)(Random.insideUnitCircle * _shake * 0.15f) : Vector3.zero;
            Cam.transform.position = _camBase + off;
        }

        void OnDisable() => Time.timeScale = 1f;
        void OnDestroy() => Art?.Dispose();
    }
}
