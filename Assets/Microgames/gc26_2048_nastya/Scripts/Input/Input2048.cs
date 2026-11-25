using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Turns the gc26_2048_nastya_Gameplay action map into game events (TDD 7).
    /// Keys are separate button actions, so rolling from one arrow to the next never drops a move.
    /// The gamepad stick has a 0.5 deadzone and must return to center before the next move.
    /// Swipe / mouse drag (TDD 7.2): on release, a drag longer than 5% of the short screen side is a move
    /// along its dominant axis. A press that starts on a button or another control is not a swipe.
    /// The map is enabled in OnEnable and disabled in OnDisable, so nothing stays active after the scene unloads.
    /// </summary>
    public sealed class Input2048 : MonoBehaviour
    {
        public const string MapName = "gc26_2048_nastya_Gameplay";

        [SerializeField] InputActionAsset actions;
        [SerializeField, Range(0.1f, 0.9f)] float stickDeadzone = 0.5f;
        [SerializeField, Range(0.01f, 0.2f)] float swipeThreshold = 0.05f;

        public event Action<Direction2048> Moved;
        public event Action PausePressed;
        public event Action NewGamePressed;
        public event Action UndoPressed;

        InputActionMap _map;
        InputAction _up, _down, _left, _right, _stick, _pause, _newGame, _undo, _press, _position;
        bool _stickArmed = true;
        bool _swiping;
        Vector2 _swipeStart;
        PointerEventData _pointerData;
        EventSystem _pointerDataOwner;
        readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

        /// <summary>False while a menu or dialog is open: moves are ignored, Pause still works.</summary>
        public bool GameplayEnabled { get; set; } = true;

        void Awake()
        {
            _map = actions.FindActionMap(MapName, throwIfNotFound: true);
            _up = _map.FindAction("Up", true);
            _down = _map.FindAction("Down", true);
            _left = _map.FindAction("Left", true);
            _right = _map.FindAction("Right", true);
            _stick = _map.FindAction("Stick", true);
            _pause = _map.FindAction("Pause", true);
            _newGame = _map.FindAction("NewGame", true);
            _undo = _map.FindAction("Undo", true);
            _press = _map.FindAction("PointerPress", true);
            _position = _map.FindAction("PointerPosition", true);
        }

        void OnEnable()
        {
            _up.performed += OnUp;
            _down.performed += OnDown;
            _left.performed += OnLeft;
            _right.performed += OnRight;
            _pause.performed += OnPause;
            _newGame.performed += OnNewGame;
            _undo.performed += OnUndo;
            _stickArmed = true;
            _swiping = false;
            _map.Enable();
        }

        void OnDisable()
        {
            _map.Disable();
            _up.performed -= OnUp;
            _down.performed -= OnDown;
            _left.performed -= OnLeft;
            _right.performed -= OnRight;
            _pause.performed -= OnPause;
            _newGame.performed -= OnNewGame;
            _undo.performed -= OnUndo;
        }

        void Update()
        {
            UpdateSwipe();
            UpdateStick();
        }

        void UpdateSwipe()
        {
            if (_press.WasPressedThisFrame())
            {
                _swipeStart = _position.ReadValue<Vector2>();
                _swiping = !IsOverControl(_swipeStart);
            }
            if (!_press.WasReleasedThisFrame() || !_swiping) return;
            _swiping = false;
            var delta = _position.ReadValue<Vector2>() - _swipeStart;
            if (TrySwipeDirection(delta, Mathf.Min(Screen.width, Screen.height) * swipeThreshold, out var dir))
                Emit(dir);
        }

        /// <summary>
        /// A drag of <paramref name="delta"/> pixels is a move when it is at least <paramref name="minLength"/> long;
        /// the direction is its dominant axis (screen y up = Up).
        /// </summary>
        public static bool TrySwipeDirection(Vector2 delta, float minLength, out Direction2048 dir)
        {
            dir = default;
            if (delta.sqrMagnitude < minLength * minLength || delta == Vector2.zero) return false;
            dir = DominantDirection(delta);
            return true;
        }

        /// <summary>True when the screen point is over a button or another control (not the board background).</summary>
        bool IsOverControl(Vector2 screenPoint)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_pointerData == null || _pointerDataOwner != es)
            {
                _pointerData = new PointerEventData(es);
                _pointerDataOwner = es;
            }
            _pointerData.position = screenPoint;
            _hits.Clear();
            es.RaycastAll(_pointerData, _hits);
            foreach (var hit in _hits)
                if (hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }

        void UpdateStick()
        {
            var v = _stick.ReadValue<Vector2>();
            float m = v.magnitude;
            if (m < stickDeadzone * 0.6f) { _stickArmed = true; return; }
            if (!_stickArmed || m < stickDeadzone) return;
            _stickArmed = false;
            Emit(DominantDirection(v));
        }

        /// <summary>Dominant axis of a screen-space vector; positive y is Up.</summary>
        public static Direction2048 DominantDirection(Vector2 v) =>
            Mathf.Abs(v.x) >= Mathf.Abs(v.y)
                ? (v.x >= 0f ? Direction2048.Right : Direction2048.Left)
                : (v.y >= 0f ? Direction2048.Up : Direction2048.Down);

        void Emit(Direction2048 dir)
        {
            if (GameplayEnabled) Moved?.Invoke(dir);
        }

        void OnUp(InputAction.CallbackContext _) => Emit(Direction2048.Up);
        void OnDown(InputAction.CallbackContext _) => Emit(Direction2048.Down);
        void OnLeft(InputAction.CallbackContext _) => Emit(Direction2048.Left);
        void OnRight(InputAction.CallbackContext _) => Emit(Direction2048.Right);
        void OnPause(InputAction.CallbackContext _) => PausePressed?.Invoke();
        void OnNewGame(InputAction.CallbackContext _) { if (GameplayEnabled) NewGamePressed?.Invoke(); }
        void OnUndo(InputAction.CallbackContext _) { if (GameplayEnabled) UndoPressed?.Invoke(); }
    }
}
