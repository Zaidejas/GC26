using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GC26.Timetraveler
{
    /// <summary>
    /// The "open world": a top-down map the traveler walks around freely. Each timeline is a region
    /// stacked upwards; level nodes sit on a path. Stand on a node to see its panel and press Play.
    /// Locked timelines are behind a gate. Walk: drag/hold a finger (or the mouse) where to go, or WASD / arrows.
    /// Art slots: map_ground, map_path, map_gate (tiled), map_node, map_node_done, map_node_locked, map_player.
    /// </summary>
    public class WorldMap : MonoBehaviour
    {
        const float WalkSpeed = 9f;
        const float NodeSize = 2f;
        const float NodeReach = 1.6f;
        const float EdgeMargin = 7.8f;   // the 16.9-unit wide view must keep the traveler visible

        static readonly Color GroundPlayable = new Color(0.30f, 0.42f, 0.24f);
        static readonly Color GroundLocked = new Color(0.25f, 0.25f, 0.28f);
        static readonly Color PathColor = new Color(0.62f, 0.52f, 0.36f);
        static readonly Color NodeOpen = new Color(0.95f, 0.95f, 0.95f);
        static readonly Color NodeDone = new Color(1f, 0.82f, 0.25f);
        static readonly Color NodeLocked = new Color(0.45f, 0.45f, 0.5f);
        static readonly Color NodeEndless = new Color(0.7f, 0.45f, 0.95f);
        static readonly Color GateColor = new Color(0.55f, 0.2f, 0.2f);
        static readonly Color Background = new Color(0.08f, 0.1f, 0.08f);

        public event Action<Vector2> PositionChanged;

        Camera _camera;
        TimetravelerSave _save;
        Action<TimetravelerLevel> _playLevel;
        Action _playEndless, _back;
        RunnerShapes _shapes;
        Transform _player;
        SpriteRenderer _playerRenderer;
        Vector2? _walkTarget;
        float _maxY;

        TimetravelerLevel _nearLevel;
        bool _nearEndless;
        Rect _panelRect, _menuButtonRect;   // GUI coordinates: pointer presses there do not walk
        GUIStyle _label, _title, _button, _nodeLabel;

        public void Init(Camera cam, TimetravelerSave save, Vector2? start, Action<TimetravelerLevel> playLevel,
            Action playEndless, Action back)
        {
            _camera = cam;
            _save = save;
            _playLevel = playLevel;
            _playEndless = playEndless;
            _back = back;
            _shapes = new RunnerShapes();
            _camera.orthographic = true;
            _camera.orthographicSize = RunnerCamera.CanvasHeightPx / RunnerCamera.PixelsPerUnit / 2f;
            _camera.backgroundColor = Background;

            BuildRegions();
            BuildLevels();
            BuildPlayer(start ?? DefaultStart());
            SnapCamera();
        }

        // ---------- building ----------

        void BuildRegions()
        {
            var w = TimetravelerLevels.MapHalfWidth;
            var lastPlayableTop = 0f;
            foreach (var era in TimetravelerLevels.Eras)
            {
                var bottom = era.MapBottom - (era.MapBottom == 0f ? 6f : 0f);   // a bit of land below the first node
                var height = era.MapBottom + TimetravelerLevels.EraHeight - bottom;
                if (era.Playable)
                {
                    _shapes.Box(transform, "MapGround", -w, bottom, w * 2f, height, GroundPlayable, sortingOrder: -10,
                        slot: TimetravelerArt.MapGround, tile: true);
                    lastPlayableTop = era.MapBottom + TimetravelerLevels.EraHeight;
                }
                else
                {
                    // Locked timelines: the same ground art, darkened.
                    _shapes.Box(transform, "MapGroundLocked", -w, bottom, w * 2f, height, GroundLocked, sortingOrder: -10,
                        slot: TimetravelerArt.MapGround, tile: true, tint: true);
                }
            }
            // A gate where the playable part ends; the traveler cannot walk past it.
            _shapes.Box(transform, "MapGate", -w, lastPlayableTop - 1f, w * 2f, 1f, GateColor, sortingOrder: -5,
                slot: TimetravelerArt.MapGate, tile: true);
            _maxY = lastPlayableTop - 2.5f;
        }

        void BuildLevels()
        {
            var levels = TimetravelerLevels.Era1;
            var previous = new Vector2(levels[0].MapPosition.x, -4f);
            foreach (var level in levels)
            {
                Path(previous, level.MapPosition);
                previous = level.MapPosition;
                var state = _save.IsDone(level) ? NodeDone : _save.IsOpen(level) ? NodeOpen : NodeLocked;
                var slot = _save.IsDone(level) ? TimetravelerArt.MapNodeDone
                    : _save.IsOpen(level) ? TimetravelerArt.MapNode : TimetravelerArt.MapNodeLocked;
                Node("MapNode_" + level.Title, level.MapPosition, state, slot);
            }
            Path(previous, TimetravelerLevels.EndlessMapPosition);
            Node("MapNode_Endless", TimetravelerLevels.EndlessMapPosition, _save.EndlessOpen ? NodeEndless : NodeLocked,
                _save.EndlessOpen ? TimetravelerArt.MapNode : TimetravelerArt.MapNodeLocked);
        }

        void Node(string name, Vector2 center, Color color, string slot)
        {
            _shapes.Box(transform, name, center.x - NodeSize / 2f, center.y - NodeSize / 2f, NodeSize, NodeSize, color,
                sortingOrder: -2, slot: slot);
        }

        /// <summary>An L-shaped path: across, then up.</summary>
        void Path(Vector2 from, Vector2 to)
        {
            const float width = 0.8f;
            var minX = Mathf.Min(from.x, to.x);
            _shapes.Box(transform, "MapPath", minX - width / 2f, from.y - width / 2f, Mathf.Abs(to.x - from.x) + width, width,
                PathColor, sortingOrder: -8, slot: TimetravelerArt.MapPath, tile: true);
            var minY = Mathf.Min(from.y, to.y);
            _shapes.Box(transform, "MapPath", to.x - width / 2f, minY - width / 2f, width, Mathf.Abs(to.y - from.y) + width,
                PathColor, sortingOrder: -8, slot: TimetravelerArt.MapPath, tile: true);
        }

        void BuildPlayer(Vector2 position)
        {
            var go = new GameObject("MapPlayer");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            _player = go.transform;
            _playerRenderer = go.AddComponent<SpriteRenderer>();
            _playerRenderer.sharedMaterial = _shapes.Material;
            _playerRenderer.sortingOrder = 5;
            // map_player slot, else the runner's traveler frame, else a plain box.
            _playerRenderer.sprite = TimetravelerArt.Sprite(TimetravelerArt.MapPlayer) ?? TravelerAnimator.LoadPreviewFrame();
            if (_playerRenderer.sprite == null)
            {
                Destroy(go);
                var box = _shapes.Box(transform, "MapPlayer", position.x - 0.75f, position.y, 1.5f, 2.2f,
                    RunnerWorldState.PastColor, sortingOrder: 5);
                Destroy(box.GetComponent<Collider2D>());
                var holder = new GameObject("MapPlayer");
                holder.transform.SetParent(transform, false);
                holder.transform.position = position;
                box.transform.SetParent(holder.transform, true);
                _player = holder.transform;
                _playerRenderer = null;
            }
        }

        Vector2 DefaultStart()
        {
            foreach (var level in TimetravelerLevels.Era1)
                if (!_save.IsDone(level)) return level.MapPosition;
            return TimetravelerLevels.EndlessMapPosition;
        }

        // ---------- walking ----------

        void Update()
        {
            var move = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.escapeKey.wasPressedThisFrame) { _back?.Invoke(); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { PlayNear(); return; }
            }

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                var screen = pointer.position.ReadValue();
                var gui = new Vector2(screen.x, Screen.height - screen.y);
                if (!_panelRect.Contains(gui) && !_menuButtonRect.Contains(gui))
                {
                    var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
                    _walkTarget = world;
                }
            }

            Vector2 pos = _player.position;
            if (move != Vector2.zero)
            {
                _walkTarget = null;
                pos += move.normalized * WalkSpeed * Time.deltaTime;
            }
            else if (_walkTarget.HasValue)
            {
                var to = _walkTarget.Value - pos;
                var step = WalkSpeed * Time.deltaTime;
                if (to.magnitude <= step) { pos = _walkTarget.Value; _walkTarget = null; }
                else pos += to.normalized * step;
                move = to;
            }
            if (_playerRenderer != null && Mathf.Abs(move.x) > 0.01f) _playerRenderer.flipX = move.x < 0f;

            pos.x = Mathf.Clamp(pos.x, -EdgeMargin, EdgeMargin);
            pos.y = Mathf.Clamp(pos.y, -4f, _maxY);
            if ((Vector2)_player.position != pos)
            {
                _player.position = pos;
                PositionChanged?.Invoke(pos);
            }

            FindNearNode(pos);
        }

        void LateUpdate() => SnapCamera();

        void SnapCamera()
        {
            if (_player == null) return;
            var half = _camera.orthographicSize;
            var y = Mathf.Clamp(_player.position.y + 4f, -6f + half, _maxY + 12f);
            _camera.transform.position = new Vector3(0f, y, _camera.transform.position.z);
        }

        void FindNearNode(Vector2 pos)
        {
            _nearLevel = null;
            _nearEndless = false;
            foreach (var level in TimetravelerLevels.Era1)
                if (Vector2.Distance(pos, level.MapPosition) < NodeReach) _nearLevel = level;
            if (_nearLevel == null && Vector2.Distance(pos, TimetravelerLevels.EndlessMapPosition) < NodeReach) _nearEndless = true;
        }

        void PlayNear()
        {
            if (_nearLevel != null && _save.IsOpen(_nearLevel)) _playLevel?.Invoke(_nearLevel);
            else if (_nearEndless && _save.EndlessOpen) _playEndless?.Invoke();
        }

        // ---------- greybox UI ----------

        void OnGUI()
        {
            var unit = Screen.height / 40f;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _nodeLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }
            _label.fontSize = Mathf.RoundToInt(unit * 0.8f);
            _nodeLabel.fontSize = Mathf.RoundToInt(unit * 0.9f);
            _title.fontSize = Mathf.RoundToInt(unit * 1.2f);
            _button.fontSize = Mathf.RoundToInt(unit * 0.9f);

            // Node and region labels, placed over the world.
            foreach (var level in TimetravelerLevels.Era1) WorldLabel(level.MapPosition, level.Title, unit);
            WorldLabel(TimetravelerLevels.EndlessMapPosition, "∞", unit);
            foreach (var era in TimetravelerLevels.Eras)
                WorldLabel(new Vector2(0f, era.MapBottom + TimetravelerLevels.EraHeight - 3f),
                    era.Playable ? era.Name : era.Name + "  (locked)", unit, _title);

            var safe = Screen.safeArea;
            var top = Screen.height - safe.yMax;
            _menuButtonRect = new Rect(safe.x + unit * 0.5f, top + unit * 0.5f, unit * 5f, unit * 2f);
            if (GUI.Button(_menuButtonRect, "Menu", _button)) _back?.Invoke();
            GUI.Label(new Rect(safe.x, top + unit * 0.5f, safe.width - unit, unit * 2f), $"Coins: {_save.Coins}",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleRight });

            _panelRect = Rect.zero;
            if (_nearLevel != null) LevelPanel(_nearLevel, unit);
            else if (_nearEndless) EndlessPanel(unit);
            else GUI.Label(new Rect(0, Screen.height - safe.y - unit * 3f, Screen.width, unit * 2.5f),
                "Walk to a level: hold where to go, or WASD / arrows", _label);
        }

        void WorldLabel(Vector2 world, string text, float unit, GUIStyle style = null)
        {
            var s = _camera.WorldToScreenPoint(world);
            if (s.z < 0f) return;
            GUI.Label(new Rect(s.x - unit * 8f, Screen.height - s.y - unit, unit * 16f, unit * 2f), text, style ?? _nodeLabel);
        }

        Rect PanelRect(float unit)
        {
            var safe = Screen.safeArea;
            var h = unit * 7.5f;
            return new Rect(safe.x + unit * 0.5f, Screen.height - safe.y - h - unit * 0.5f, safe.width - unit, h);
        }

        void DrawPanelBack(Rect r)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void LevelPanel(TimetravelerLevel level, float unit)
        {
            var r = _panelRect = PanelRect(unit);
            DrawPanelBack(r);
            GUI.Label(new Rect(r.x, r.y + unit * 0.4f, r.width, unit * 1.6f), $"Level {level.Title}", _title);
            var status = _save.IsDone(level) ? $"Done   best coins {_save.LevelBestCoins(level)}"
                : _save.IsOpen(level) ? "New!" : "Locked: finish the previous level first";
            GUI.Label(new Rect(r.x, r.y + unit * 2f, r.width, unit * 1.4f), $"{level.LengthMeters} m   {status}", _label);
            GUI.enabled = _save.IsOpen(level);
            if (GUI.Button(new Rect(r.x + r.width * 0.2f, r.y + unit * 4f, r.width * 0.6f, unit * 2.6f), "Play  (Enter)", _button))
                _playLevel?.Invoke(level);
            GUI.enabled = true;
        }

        void EndlessPanel(float unit)
        {
            var r = _panelRect = PanelRect(unit);
            DrawPanelBack(r);
            GUI.Label(new Rect(r.x, r.y + unit * 0.4f, r.width, unit * 1.6f), "Endless run", _title);
            GUI.Label(new Rect(r.x, r.y + unit * 2f, r.width, unit * 1.4f),
                _save.EndlessOpen ? $"Best {_save.BestMeters} m" : "Finish all levels of this timeline first", _label);
            GUI.enabled = _save.EndlessOpen;
            if (GUI.Button(new Rect(r.x + r.width * 0.2f, r.y + unit * 4f, r.width * 0.6f, unit * 2.6f), "Run  (Enter)", _button))
                _playEndless?.Invoke();
            GUI.enabled = true;
        }
    }
}
