using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Follows the traveler: he stays at 25% from the left edge so obstacles are visible early.
    /// Vertical follow is smoothed and never drops below the base ground view.
    /// The view is the GDD's 270 x 480 px canvas (16.875 x 30 units at 16 px per unit) zoomed in by <see cref="zoom"/>,
    /// so the traveler reads big on a phone.
    /// </summary>
    public class RunnerCamera : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;
        public const float CanvasHeightPx = 480f;

        /// <summary>Where the traveler stands while running: 0 = left edge, 1 = right edge.</summary>
        public const float RunAnchor = 0.3f;

        [Range(0f, 1f)] public float screenAnchorX = RunAnchor;
        [Tooltip("1 = the whole 270x480 canvas; bigger = closer (the traveler looks bigger, less is seen ahead)")]
        public float zoom = 1.2f;
        [Tooltip("Camera centre height above the ground the traveler last stood on (not above the traveler: jumps do not move the view)")]
        public float heightAboveGround = 9f;
        [Tooltip("Lowest camera centre: keeps about 3.5 units of ground at the bottom of the screen")]
        public float minCameraY = 9f;
        [Tooltip("The camera only rises during a jump if the traveler's head gets this close to the top edge")]
        public float topMargin = 3f;
        /// <summary>Height of the ground under the traveler, set by the session while she stands on it.</summary>
        [System.NonSerialized] public float groundY;
        [Tooltip("How fast the horizontal framing follows changes of screenAnchorX (boss chase / fight)")]
        public float anchorSmoothTime = 0.35f;
        [Tooltip("Settings: 0 = no shake, 1 = full")]
        public float shakeFactor = 1f;
        float _anchor = -1f;
        float _anchorVelocity;
        public float verticalSmoothTime = 0.4f;

        Camera _camera;
        Transform _target;
        float _velocityY;
        float _shakeTime;
        float _shakeDuration;
        float _shakeStrength;
        Vector3 _shakeOffset;

        /// <summary>Short camera shake (real time, so it also works while time is slowed).</summary>
        public void Shake(float strength, float duration)
        {
            strength *= shakeFactor;
            if (strength <= 0f) return;
            if (strength < _shakeStrength && _shakeTime > 0f) return;
            _shakeStrength = strength;
            _shakeDuration = _shakeTime = duration;
        }

        public float LeftEdge => transform.position.x - HalfWidth;
        public float RightEdge => transform.position.x + HalfWidth;
        float HalfWidth => _camera.orthographicSize * _camera.aspect;

        public void Init(Camera cam, Transform target)
        {
            _camera = cam;
            _target = target;
            _camera.orthographic = true;
            _camera.orthographicSize = CanvasHeightPx / PixelsPerUnit / 2f / Mathf.Max(0.5f, zoom); // 15 units / zoom
            Snap();
        }

        public void Snap()
        {
            if (_target == null) return;
            transform.position = new Vector3(TargetX(), DesiredY(), transform.position.z - _shakeOffset.z);
            _velocityY = 0f;
            _anchor = screenAnchorX;
            _anchorVelocity = 0f;
            _shakeOffset = Vector3.zero;
            _shakeTime = 0f;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            var basePos = transform.position - _shakeOffset;
            var y = Mathf.SmoothDamp(basePos.y, DesiredY(), ref _velocityY, verticalSmoothTime);
            _anchor = _anchor < 0f ? screenAnchorX
                : Mathf.SmoothDamp(_anchor, screenAnchorX, ref _anchorVelocity, anchorSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            _shakeOffset = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.unscaledDeltaTime;
                var k = _shakeStrength * Mathf.Clamp01(_shakeTime / _shakeDuration);
                _shakeOffset = new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
            }
            transform.position = new Vector3(TargetX(), y, basePos.z) + _shakeOffset;
        }

        float TargetX() => _target.position.x + (0.5f - (_anchor < 0f ? screenAnchorX : _anchor)) * 2f * HalfWidth;
        // Calm vertical framing: follow the ground level (steps, ledges), not every jump.
        float DesiredY() => Mathf.Max(minCameraY, groundY + heightAboveGround,
            _target.position.y + 2f + topMargin - _camera.orthographicSize);
    }
}
