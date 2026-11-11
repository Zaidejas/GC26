using UnityEngine;

namespace GC26.Timetraveler
{
    /// <summary>
    /// Follows the traveler: he stays at 25% from the left edge so obstacles are visible early.
    /// Vertical follow is smoothed and never drops below the base ground view.
    /// The view is the GDD's 270 x 480 px canvas = 16.875 x 30 units at 16 px per unit.
    /// </summary>
    public class RunnerCamera : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;
        public const float CanvasHeightPx = 480f;

        [Range(0f, 1f)] public float screenAnchorX = 0.25f;
        [Tooltip("Camera centre height above the traveler's feet")]
        public float heightAboveTarget = 9f;
        public float minCameraY = 9f;
        public float verticalSmoothTime = 0.18f;

        Camera _camera;
        Transform _target;
        float _velocityY;

        public float LeftEdge => transform.position.x - HalfWidth;
        public float RightEdge => transform.position.x + HalfWidth;
        float HalfWidth => _camera.orthographicSize * _camera.aspect;

        public void Init(Camera cam, Transform target)
        {
            _camera = cam;
            _target = target;
            _camera.orthographic = true;
            _camera.orthographicSize = CanvasHeightPx / PixelsPerUnit / 2f; // 15 units
            Snap();
        }

        public void Snap()
        {
            if (_target == null) return;
            transform.position = new Vector3(TargetX(), DesiredY(), transform.position.z);
            _velocityY = 0f;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            var y = Mathf.SmoothDamp(transform.position.y, DesiredY(), ref _velocityY, verticalSmoothTime);
            transform.position = new Vector3(TargetX(), y, transform.position.z);
        }

        float TargetX() => _target.position.x + (0.5f - screenAnchorX) * 2f * HalfWidth;
        float DesiredY() => Mathf.Max(minCameraY, _target.position.y + heightAboveTarget);
    }
}
