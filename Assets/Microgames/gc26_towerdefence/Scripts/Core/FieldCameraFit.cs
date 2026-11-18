using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// Keeps the whole field on screen at any screen shape: sets the orthographic size so the field
    /// fits both in height and in width, and centers the camera on it.
    /// Horizontal screens show the 16x9 field; upright screens show the turned 9x16 field (see EnemyPath),
    /// with extra room above and below for the HUD (gold, lives, wave on top; the play button at the bottom).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class FieldCameraFit : MonoBehaviour
    {
        [Tooltip("The field is placed relative to this path object and turns with it.")]
        [SerializeField] EnemyPath path;

        [Tooltip("Empty space around the field, in tiles.")]
        [SerializeField, Min(0f)] float margin = 0.25f;

        [Tooltip("Upright screens only: extra tiles kept free above and below the field for the HUD.")]
        [SerializeField, Min(0f)] float verticalHudRoom = 2.5f;

        Camera _camera;

        void OnEnable() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (_camera == null) return;

            bool vertical = path != null && path.IsVertical;
            float width = path != null ? path.FieldWidth : EnemyPath.Columns;
            float height = path != null ? path.FieldHeight : EnemyPath.Rows;

            float halfWidth = width / 2f + margin;
            float halfHeight = height / 2f + margin + (vertical ? verticalHudRoom : 0f);
            float aspect = _camera.aspect > 0f ? _camera.aspect : 16f / 9f;

            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);

            Vector3 origin = path != null ? path.transform.position : Vector3.zero;
            transform.position = new Vector3(origin.x + width / 2f, origin.y + height / 2f, origin.z - 10f);
        }
    }
}
