using System.Collections.Generic;
using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Greybox track for the endless test run: builds patterns (flat, gap, rock, log, steps, ledge)
    /// ahead of the camera and removes them behind it. Difficulty grows with distance.
    /// Ground at the same height is merged into one long box, so the traveler never catches on seams.
    /// Later replaced by hand-made chunk prefabs with Tilemaps (see the GDD).
    /// </summary>
    public class RunnerTrack : MonoBehaviour
    {
        const float GroundDepth = 30f;
        const float MaxGroundY = 7.5f;
        const float StepHeight = 2.5f;
        const float SafeStartLength = 45f;
        const float DistanceForFullDifficulty = 600f;

        public float KillY => -12f;
        public float StartX => 0f;

        RunnerShapes _shapes;
        System.Random _rng;
        float _cursorX;
        float _groundY;
        Transform _openGround;          // last ground box, extended while the height stays the same
        float _openGroundStartX;
        readonly List<Transform> _pieces = new List<Transform>();

        public void Build(RunnerShapes shapes, int seed)
        {
            _shapes = shapes;
            Clear();
            _rng = new System.Random(seed);
            _cursorX = StartX - 20f;
            _groundY = 0f;
            Flat(SafeStartLength);
        }

        public void Extend(float untilX)
        {
            while (_cursorX < untilX) AddPattern();
        }

        public void Trim(float behindX)
        {
            for (var i = _pieces.Count - 1; i >= 0; i--)
            {
                var piece = _pieces[i];
                if (piece == _openGround) continue;
                var right = piece.position.x + piece.localScale.x / 2f;
                if (right < behindX)
                {
                    Destroy(piece.gameObject);
                    _pieces.RemoveAt(i);
                }
            }
        }

        void Clear()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);
            _pieces.Clear();
            _openGround = null;
        }

        float Difficulty => Mathf.Clamp01((_cursorX - StartX) / DistanceForFullDifficulty);
        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        void AddPattern()
        {
            var d = Difficulty;
            var roll = _rng.NextDouble();

            if (roll < 0.18) Flat(Range(8f, 14f) - d * 4f);
            else if (roll < 0.36) Gap(Range(3.5f, 4.5f) + d * 2f);
            else if (roll < 0.52) Rock();
            else if (roll < 0.66) Log();
            else if (roll < 0.78) StepUp();
            else if (roll < 0.88) StepDown();
            else Ledge();
        }

        // ---------- patterns ----------

        void Flat(float length) => Ground(Mathf.Max(4f, length));

        void Gap(float width)
        {
            Flat(6f);
            _cursorX += width;
            _openGround = null;
            Flat(8f);
        }

        void Rock()
        {
            Flat(5f);
            Hazard("TrackRock", _cursorX, _groundY, 1.5f, 1.5f);
            Flat(9f);
        }

        void Log()
        {
            // Overhead log: standing (3 units) hits it, sliding (1.4 units) passes under.
            Flat(5f);
            Hazard("TrackLog", _cursorX, _groundY + 1.8f, 2.5f, 12f);
            Flat(9f);
        }

        void StepUp()
        {
            if (_groundY + StepHeight > MaxGroundY) { StepDown(); return; }
            Flat(6f);
            _groundY += StepHeight;
            _openGround = null;
            Flat(10f);
        }

        void StepDown()
        {
            if (_groundY - StepHeight < 0f) { Rock(); return; }
            Flat(6f);
            _groundY -= StepHeight;
            _openGround = null;
            Flat(10f);
        }

        void Ledge()
        {
            // A floating platform above a rock: jump onto it, or jump the rock below.
            var startX = _cursorX;
            Flat(16f);
            var ledge = _shapes.Box(transform, "TrackLedge", startX + 4f, _groundY + 5f, 8f, 1f, RunnerShapes.LedgeColor);
            _pieces.Add(ledge.transform);
            Hazard("TrackRock", startX + 9f, _groundY, 1.5f, 1.5f);
        }

        // ---------- building blocks ----------

        void Ground(float length)
        {
            var top = _groundY;
            if (_openGround != null)
            {
                // Same height as the last box: stretch it instead of adding a seam.
                var width = _cursorX + length - _openGroundStartX;
                _openGround.localScale = new Vector3(width, GroundDepth, 1f);
                _openGround.position = new Vector3(_openGroundStartX + width / 2f, top - GroundDepth / 2f, 0f);
            }
            else
            {
                var box = _shapes.Box(transform, "TrackGround", _cursorX, top - GroundDepth, length, GroundDepth,
                    RunnerShapes.GroundColor);
                _openGround = box.transform;
                _openGroundStartX = _cursorX;
                _pieces.Add(box.transform);
            }
            _cursorX += length;
        }

        void Hazard(string name, float x, float y, float width, float height)
        {
            var go = _shapes.Box(transform, name, x, y, width, height, RunnerShapes.HazardColor, isTrigger: true,
                sortingOrder: 1);
            go.AddComponent<RunnerHazard>();
            _pieces.Add(go.transform);
        }
    }
}
