using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GC26.Tanki.Game
{
    /// <summary>
    /// The game's own look on top of the shared URP: glow around bright parts (reactor, shots, blasts),
    /// a slightly richer picture and darker corners. Made in code inside the game's scene, so it goes
    /// away with the scene and never changes the GC26 menu or other games.
    /// </summary>
    public static class TankiPostFx
    {
        public static Volume Create(Transform parent, Camera camera)
        {
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "gc26_tanki PostFx";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(false);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments colour = profile.Add<ColorAdjustments>(true);
            colour.contrast.Override(10f);
            colour.saturation.Override(12f);
            colour.postExposure.Override(0.1f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.08f, 0.04f, 0.12f));
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            var holder = new GameObject("gc26_tanki PostFx");
            holder.transform.SetParent(parent, false);
            Volume volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return volume;
        }
    }
}
