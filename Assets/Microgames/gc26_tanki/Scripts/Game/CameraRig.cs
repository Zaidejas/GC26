using GC26.Tanki.Rules;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>Creates the camera and light and fits the whole map on any screen shape.</summary>
    public static class CameraRig
    {
        public const float TiltDegrees = 62f;

        // Screen share kept free for the status bar and, in portrait, for the thumb controls.
        const float PortraitTopShare = 0.08f;
        const float PortraitBottomShare = 0.2f;
        const float LandscapeTopShare = 0.11f;
        const float LandscapeBottomShare = 0.01f;

        public static Camera Create(Transform parent)
        {
            // Always an own camera, drawn over the GC26 menu's camera; one ear for the whole app.
            var holder = new GameObject("gc26_tanki Camera") { tag = "MainCamera" };
            holder.transform.SetParent(parent, false);
            Camera camera = holder.AddComponent<Camera>();
            camera.depth = 10f;
            if (Object.FindAnyObjectByType<AudioListener>() == null) holder.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WorldView.Hex("2B2235");
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 100f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.color = WorldView.Hex("FFE7C7");
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = WorldView.Hex("6E6678");

            return camera;
        }

        public static void Fit(Camera camera)
        {
            float w = Levels.Width;
            float h = Levels.Height;
            bool portrait = Screen.height > Screen.width;
            float topShare = portrait ? PortraitTopShare : LandscapeTopShare;
            float bottomShare = portrait ? PortraitBottomShare : LandscapeBottomShare;

            camera.transform.rotation = Quaternion.Euler(TiltDegrees, 0f, 0f);
            Vector3 centre = new Vector3(w / 2f, 0f, h / 2f);
            Vector3 back = camera.transform.forward;

            var corners = new[]
            {
                new Vector3(-0.4f, 0f, -0.4f),
                new Vector3(w + 0.4f, 0f, -0.4f),
                new Vector3(-0.4f, 0.7f, h + 0.4f),
                new Vector3(w + 0.4f, 0.7f, h + 0.4f)
            };

            for (float distance = 8f; distance < 80f; distance += 0.1f)
            {
                camera.transform.position = centre - back * distance;

                float minY = 1f, maxY = 0f;
                bool widthFits = true;
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                    if (point.x < 0.01f || point.x > 0.99f) widthFits = false;
                }
                if (widthFits && maxY - minY < 1f - topShare - bottomShare) break;
            }

            // Slide along the ground until the map sits in the middle of the free area.
            for (int i = 0; i < 400; i++)
            {
                float minY = 1f, maxY = 0f;
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }
                float error = ((1f - topShare) - maxY) - (minY - bottomShare);
                if (Mathf.Abs(error) < 0.004f) break;
                camera.transform.position += Vector3.forward * (error > 0f ? -0.02f : 0.02f);
            }
        }
    }
}
