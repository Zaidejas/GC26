using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Keeps the whole 16x9 field on screen at any screen shape: sets the orthographic size
    /// so the field fits both in height and in width, and centers the camera on it.
    /// On a phone held upright the field is small; the "Horizontal and vertical layout" task
    /// will turn the map for portrait screens.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class FieldCameraFit : MonoBehaviour
    {
        [Tooltip("Optional. The field is placed relative to this path object; empty = field starts at world (0, 0).")]
        [SerializeField] EnemyPath path;

        [Tooltip("Empty space around the field, in tiles.")]
        [SerializeField, Min(0f)] float margin = 0.25f;

        Camera _camera;

        void OnEnable() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (_camera == null) return;

            float halfWidth = EnemyPath.Columns / 2f + margin;
            float halfHeight = EnemyPath.Rows / 2f + margin;
            float aspect = _camera.aspect > 0f ? _camera.aspect : 16f / 9f;

            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);

            Vector3 origin = path != null ? path.transform.position : Vector3.zero;
            transform.position = new Vector3(origin.x + EnemyPath.Columns / 2f, origin.y + EnemyPath.Rows / 2f, origin.z - 10f);
        }
    }
}
