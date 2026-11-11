using UnityEngine;

namespace GC26.Lines.View
{
    public static class CameraSetup
    {
        /// <summary>Points the camera so that 1 world unit = 1 board cell; the forest background is drawn on top of its colour.</summary>
        public static void Apply(Camera camera, BoardLayout layout)
        {
            camera.orthographic = true;
            camera.orthographicSize = layout.OrthographicSize;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Screen;
        }
    }
}
