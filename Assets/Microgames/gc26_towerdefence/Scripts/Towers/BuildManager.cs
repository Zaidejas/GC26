using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The 11 build slots by the road. Tap an empty slot to open the build menu, tap a tower in it to build
    /// (if there is enough gold). Tap a built tower for Upgrade / Sell / Target (its range is shown).
    /// Tapping anywhere else closes the menu.
    /// Slots are typed in prototype map coordinates, like EnemyPath (SLOTS in game.js).
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] EnemyPath path;
        [SerializeField] LevelFlow level;
        [SerializeField] EconomyManager economy;
        [SerializeField] WaveSpawner enemies;
        [SerializeField] PlaceholderArt art;
        [SerializeField] PoolManager pool;
        [SerializeField] BuildMenuView menu;
        /// <summary>Towers offered in the build menu, in this order (LevelData.towers).</summary>
        TowerData[] towers => level.Data != null && level.Data.towers != null ? level.Data.towers : System.Array.Empty<TowerData>();

        /// <summary>Build slots in prototype map coordinates (LevelData.slots).</summary>
        Vector2[] slotMapPoints => level.Data != null && level.Data.slots != null ? level.Data.slots : System.Array.Empty<Vector2>();

        /// <summary>Half the size of the tap area around a slot, in tiles.</summary>
        const float TapHalfSize = 0.55f;

        static readonly Color SlotColor = new(0.64f, 0.64f, 0.64f);
        static readonly Color SlotSelectedColor = new(0.85f, 0.85f, 0.85f);

        SpriteRenderer[] _slotViews;
        TowerUnit[] _built;
        Transform _towerRoot;
        Transform _effectRoot;
        SpriteRenderer _rangePreview;
        int _selected = -1;
        readonly List<RaycastResult> _uiHits = new();

        public IReadOnlyList<TowerData> Towers => towers;
        public bool CanAfford(TowerData tower) => economy.CanAfford(tower.cost);

        void Awake()
        {
            _towerRoot = new GameObject("Towers").transform;
            _towerRoot.SetParent(transform, false);
            _effectRoot = new GameObject("Effects").transform;
            _effectRoot.SetParent(transform, false);
        }

        void OnEnable()
        {
            if (level != null)
            {
                level.LevelBegun += OnLevelBegun;
                level.LevelClosed += ClearTowers;
            }
            if (path != null) path.LayoutChanged += Relayout;
        }

        void OnDisable()
        {
            if (level != null)
            {
                level.LevelBegun -= OnLevelBegun;
                level.LevelClosed -= ClearTowers;
            }
            if (path != null) path.LayoutChanged -= Relayout;
        }

        void Start()
        {
            if (_slotViews == null) RebuildSlots();
            _rangePreview = art.NewShape("RangePreview", art.Ring, transform, Vector3.zero, 1f, new Color(1f, 1f, 1f, 0.7f), 2);
            _rangePreview.gameObject.SetActive(false);
        }

        public Vector3 SlotPosition(int slot) => path.MapToWorld(slotMapPoints[slot]);

        void Update()
        {
            if (_selected >= 0 && (level.IsOver || Time.timeScale == 0f))
                CloseMenu();

            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            if (Time.timeScale == 0f || level.IsOver || level.State == LevelState.Waiting) return;

            Vector2 screen = pointer.position.ReadValue();
            if (IsOverUi(screen)) return; // the menu, pause or start buttons handle this tap

            int slot = SlotAt(screen);
            if (slot >= 0 && slot != _selected) OpenMenu(slot);
            else CloseMenu();
        }

        void BuildOn(int slot, TowerData tower)
        {
            if (_built[slot] != null || tower == null) return;
            if (!economy.TrySpend(tower.cost)) return;

            _built[slot] = TowerUnit.Build(tower, slot, SlotPosition(slot), _towerRoot, enemies, art, _effectRoot, pool);
            _slotViews[slot].gameObject.SetActive(false);
            CloseMenu();
        }

        void Upgrade(TowerUnit tower)
        {
            int cost = tower.UpgradeCost;
            if (!tower.CanUpgrade || !economy.TrySpend(cost)) return;
            tower.Upgrade(cost);
            CloseMenu();
        }

        void Sell(TowerUnit tower)
        {
            int slot = tower.SlotIndex;
            economy.Add(tower.SellValue);
            Destroy(tower.gameObject);
            _built[slot] = null;
            _slotViews[slot].gameObject.SetActive(true);
            CloseMenu();
        }

        /// <summary>
        /// Range circle on the selected slot: `range` while hovering a menu button,
        /// otherwise (0) the selected tower's own range, or nothing on an empty slot.
        /// </summary>
        void PreviewRange(float range)
        {
            if (_rangePreview == null) return;
            if (range <= 0f && _selected >= 0 && _built[_selected] != null) range = _built[_selected].Range;
            bool show = range > 0f && _selected >= 0;
            _rangePreview.gameObject.SetActive(show);
            if (!show) return;
            _rangePreview.transform.position = SlotPosition(_selected);
            _rangePreview.transform.localScale = Vector3.one * range * 2f;
        }

        void OpenMenu(int slot)
        {
            CloseMenu();
            _selected = slot;
            var tower = _built[slot];
            if (tower == null) _slotViews[slot].color = SlotSelectedColor;

            var items = tower == null ? BuildItems(slot) : TowerItems(tower);
            menu.Open(worldCamera.WorldToScreenPoint(SlotPosition(slot)), items, PreviewRange);
            PreviewRange(0f);
        }

        List<RadialItem> BuildItems(int slot)
        {
            var items = new List<RadialItem>();
            foreach (var t in towers)
            {
                if (t == null) continue;
                var tower = t;
                items.Add(new RadialItem
                {
                    Label = tower.displayName,
                    Sub = () => $"{tower.cost}g",
                    Color = tower.color,
                    CanUse = () => economy.CanAfford(tower.cost),
                    Use = () => BuildOn(slot, tower),
                    PreviewRange = tower.range,
                });
            }
            return items;
        }

        List<RadialItem> TowerItems(TowerUnit tower)
        {
            int slot = tower.SlotIndex;
            return new List<RadialItem>
            {
                new()
                {
                    Label = tower.CanUpgrade ? "Upgrade" : "Max",
                    Sub = () => tower.CanUpgrade ? $"{tower.UpgradeCost}g" : "",
                    Color = new Color(0.4f, 0.73f, 0.42f),
                    CanUse = () => tower.CanUpgrade && economy.CanAfford(tower.UpgradeCost),
                    Use = () => Upgrade(tower),
                    PreviewRange = tower.CanUpgrade ? tower.RangeAt(tower.Level + 1) : 0f,
                },
                new()
                {
                    Label = "Sell",
                    Sub = () => $"+{tower.SellValue}g",
                    Color = new Color(0.94f, 0.33f, 0.31f),
                    Use = () => Sell(tower),
                },
                new()
                {
                    Label = "Target",
                    Sub = () => tower.Mode.ToString(),
                    Color = new Color(0.56f, 0.79f, 0.98f),
                    Use = tower.CycleMode, // the menu stays open; the label updates by itself
                },
            };
        }

        void CloseMenu()
        {
            if (_selected >= 0 && _slotViews[_selected] != null) _slotViews[_selected].color = SlotColor;
            _selected = -1;
            PreviewRange(0f);
            menu.Close();
        }

        /// <summary>The screen turned or the level's points were edited: move slots and towers.</summary>
        void Relayout()
        {
            CloseMenu();
            if (_slotViews == null) return;
            if (_slotViews.Length != slotMapPoints.Length)
            {
                Debug.LogWarning($"[{TowerdefenceGame.Id}] the number of slots changed; stop and start Play to apply it", this);
                return;
            }
            for (int i = 0; i < _slotViews.Length; i++)
            {
                _slotViews[i].transform.position = SlotPosition(i);
                if (_built[i] != null) _built[i].transform.position = SlotPosition(i);
            }
        }

        // A new attempt, maybe on another level: empty field, slots of the current level.
        void OnLevelBegun()
        {
            ClearTowers();
            RebuildSlots();
        }

        /// <summary>Creates one grey square per slot of the current level (levels differ in count and place).</summary>
        void RebuildSlots()
        {
            if (_slotViews != null)
                foreach (var view in _slotViews)
                    if (view != null) Destroy(view.gameObject);

            _built = new TowerUnit[slotMapPoints.Length];
            _slotViews = new SpriteRenderer[slotMapPoints.Length];
            for (int i = 0; i < slotMapPoints.Length; i++)
                _slotViews[i] = art.NewShape($"Slot {i + 1}", art.Square, transform, SlotPosition(i), 0.76f, SlotColor, 1);
        }

        void ClearTowers()
        {
            CloseMenu();
            if (_built == null) return;
            for (int i = 0; i < _built.Length; i++)
            {
                if (_built[i] != null) Destroy(_built[i].gameObject);
                _built[i] = null;
                _slotViews[i].gameObject.SetActive(true);
            }
            foreach (Transform fx in _effectRoot) Destroy(fx.gameObject);
        }

        int SlotAt(Vector2 screen)
        {
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            for (int i = 0; i < slotMapPoints.Length; i++)
            {
                Vector3 d = world - SlotPosition(i);
                if (Mathf.Abs(d.x) <= TapHalfSize && Mathf.Abs(d.y) <= TapHalfSize) return i;
            }
            return -1;
        }

        /// <summary>True when a UI element (HUD, build menu) is under this screen point. Works for mouse and touch.</summary>
        bool IsOverUi(Vector2 screen)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            _uiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = screen }, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
