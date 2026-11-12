using System.Collections;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// TEMPORARY: sends a few enemies down the path to check EnemyPath and EnemyUnit.
    /// Remove it from the scene once WaveSpawner exists.
    /// Set Test Damage Per Second above 0 to see enemies die (Grunt has 50 HP).
    /// </summary>
    public class EnemyTestSpawner : MonoBehaviour
    {
        [SerializeField] TowerdefenceGame game;
        [SerializeField] EnemyPath path;
        [SerializeField] EnemyData enemy;
        [SerializeField, Min(1)] int count = 8;
        [SerializeField, Min(0.05f)] float interval = 1f;
        [Tooltip("Damage per second dealt to every enemy, to test dying. 0 = they all reach the castle.")]
        [SerializeField, Min(0f)] float testDamagePerSecond;

        Sprite _circle;
        Texture2D _circleTexture;
        int _kills;
        int _livesLost;

        void OnEnable()
        {
            if (game == null) return;
            game.Started += OnGameStarted;
            if (game.IsStarted) OnGameStarted();
        }

        void OnDisable()
        {
            if (game != null) game.Started -= OnGameStarted;
            StopAllCoroutines();
        }

        void OnDestroy()
        {
            if (_circle != null) Destroy(_circle);
            if (_circleTexture != null) Destroy(_circleTexture);
        }

        void OnGameStarted()
        {
            if (path == null || enemy == null)
            {
                Debug.LogError($"[{TowerdefenceGame.Id}] EnemyTestSpawner: assign Path and Enemy", this);
                return;
            }
            StartCoroutine(SpawnRoutine());
        }

        IEnumerator SpawnRoutine()
        {
            for (int i = 0; i < count; i++)
            {
                var unit = EnemyUnit.Spawn(enemy, path, transform, 1f, GetCircle());
                unit.Died += u => Debug.Log($"[{TowerdefenceGame.Id}] {u.Data.displayName} died, +{u.Data.gold} gold (kills: {++_kills})");
                unit.ReachedCastle += u => Debug.Log($"[{TowerdefenceGame.Id}] {u.Data.displayName} reached the castle, -{u.Data.livesLost} lives (total: {_livesLost += u.Data.livesLost})");
                yield return new WaitForSeconds(interval);
            }
        }

        void Update()
        {
            if (testDamagePerSecond <= 0f) return;
            foreach (var unit in GetComponentsInChildren<EnemyUnit>())
                unit.TakeDamage(testDamagePerSecond * Time.deltaTime);
        }

        /// <summary>A white circle sprite, 1 unit wide, built once at runtime (no art needed yet).</summary>
        Sprite GetCircle()
        {
            if (_circle != null) return _circle;

            const int size = 64;
            _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            _circleTexture.SetPixels32(pixels);
            _circleTexture.Apply();
            _circle = Sprite.Create(_circleTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _circle;
        }
    }
}
