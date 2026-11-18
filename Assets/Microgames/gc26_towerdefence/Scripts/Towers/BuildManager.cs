using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The 11 build slots by the road. Tap an empty slot to open the build menu, tap a tower in it to build
    /// (if there is enough gold). Tapping anywhere else closes the menu.
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
        [SerializeField] BuildMenuView menu;
        [Tooltip("Towers offered in the build menu, in this order.")]
        [SerializeField] TowerData[] towers;

        [Tooltip("Build slots in prototype map coordinates (x right, y down).")]
        [SerializeField]
        Vector2[] slotMapPoints =
        {
            new(1.5f, 3f), new(2f, 5.5f), new(5.5f, 3f), new(5.5f, 5f), new(5.5f, 8f), new(9.5f, 1f),
            new(9.5f, 4.5f), new(9.5f, 7.8f), new(13.5f, 1.5f), new(13.5f, 4.5f), new(13.5f, 8f),
        };

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
            if (level != null) level.LevelBegun += ClearTowers;
        }

        void OnDisable()
        {
            if (level != null) level.LevelBegun -= ClearTowers;
        }

        void Start()
        {
            _built = new TowerUnit[slotMapPoints.Length];
            _slotViews = new SpriteRenderer[slotMapPoints.Length];
            for (int i = 0; i < slotMapPoints.Length; i++)
                _slotViews[i] = art.NewShape($"Slot {i + 1}", art.Square, transform, SlotPosition(i), 0.76f, SlotColor, 1);

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
            if (slot >= 0 && _built[slot] == null && slot != _selected) OpenMenu(slot);
            else CloseMenu();
        }

        /// <summary>Called by the build menu. Spends the gold and builds on the selected slot.</summary>
        public void BuildSelected(TowerData tower)
        {
            if (_selected < 0 || _built[_selected] != null || tower == null) return;
            if (!economy.TrySpend(tower.cost)) return;

            int slot = _selected;
            _built[slot] = TowerUnit.Build(tower, slot, SlotPosition(slot), _towerRoot, enemies, art, _effectRoot);
            _slotViews[slot].gameObject.SetActive(false);
            CloseMenu();
        }

        /// <summary>Shows the range circle of a tower on the selected slot (menu hover), or hides it with null.</summary>
        public void PreviewRange(TowerData tower)
        {
            if (_rangePreview == null) return;
            bool show = tower != null && _selected >= 0;
            _rangePreview.gameObject.SetActive(show);
            if (!show) return;
            _rangePreview.transform.position = SlotPosition(_selected);
            _rangePreview.transform.localScale = Vector3.one * tower.range * 2f;
        }

        void OpenMenu(int slot)
        {
            CloseMenu();
            _selected = slot;
            _slotViews[slot].color = SlotSelectedColor;
            menu.Open(worldCamera.WorldToScreenPoint(SlotPosition(slot)), this);
        }

        void CloseMenu()
        {
            if (_selected >= 0 && _slotViews[_selected] != null) _slotViews[_selected].color = SlotColor;
            _selected = -1;
            PreviewRange(null);
            menu.Close();
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
