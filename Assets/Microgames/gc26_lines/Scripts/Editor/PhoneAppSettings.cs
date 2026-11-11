using UnityEditor;
using UnityEngine;

namespace GC26.Lines.EditorTools
{
    /// <summary>
    /// Settings that make Lines its own phone app: name, app id, portrait only, icon and splash screen.
    /// They are project-wide, which the team rules leave to the organiser, so they are never committed:
    /// BuildScripts applies them just before building, and Tools~/build.sh puts ProjectSettings back
    /// to how it was once Unity has finished.
    /// </summary>
    public static class PhoneAppSettings
    {
        public const string BundleId = "com.gc26.lines";
        public const string IconFolder = ArtImportSettings.AppIconFolder;
        const string SplashBackground = ArtImportSettings.BackgroundFolder + "forest_plain.png";

        public static void Apply()
        {
            PlayerSettings.companyName = "GC26";
            PlayerSettings.productName = "Lines";
            // A unique app id, needed to install on a real iPhone with a free Apple account (and on Android).
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            ApplyIconAndSplash();
        }

        /// <summary>
        /// The app icon (one glowing mushroom on the dark forest) and the splash screen (the same mushroom and
        /// the "Lines" title over the forest, no Unity logo). Android 8+ gets the icon as two layers, so the
        /// phone can cut it into its own shape.
        /// </summary>
        static void ApplyIconAndSplash()
        {
            var icon = Load<Texture2D>("icon.png");
            if (icon == null) { Debug.LogWarning("Lines: app icon not found in " + IconFolder); return; }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // Android's adaptive icon kind lives in the Android module; it is looked up by name so this
            // still compiles on a Unity without that module.
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            var foreground = Load<Texture2D>("icon_foreground.png");
            var background = Load<Texture2D>("icon_background.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                bool adaptive = kind.ToString().Contains("Adaptive");
                foreach (var platformIcon in icons)
                {
                    if (adaptive) platformIcon.SetTextures(background, foreground);
                    else platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = View.Palette.Screen;
            PlayerSettings.SplashScreen.background = AssetDatabase.LoadAssetAtPath<Sprite>(SplashBackground);
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(2f, Load<Sprite>("splash_logo.png")),
            };
            // The screen iOS shows before Unity starts: a plain colour, the same dark as the game.
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.Default);
        }

        static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>(IconFolder + file);
    }
}
