using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Lines.EditorTools
{
    /// <summary>
    /// Builds the game from the command line (scripts/build.sh calls these):
    /// an Xcode project for the iOS Simulator, an Xcode project for a real iPhone, and an Android APK.
    /// Simulator/device choices are set only for the build and put back afterwards, and the editor is
    /// switched back to the Mac platform at the end, so opening Unity later looks the same as before.
    /// </summary>
    public static class BuildScripts
    {
        public const string IosSimulatorFolder = "Builds/iOS-Simulator";
        public const string IosDeviceFolder = "Builds/iOS-Device";
        public const string AndroidApk = "Builds/Android/Lines.apk";

        /// <summary>Xcode project for the iOS Simulator (Apple silicon Macs).</summary>
        public static void IosSimulator() => BuildIos(true, IosSimulatorFolder);

        /// <summary>
        /// Xcode project for a real iPhone. If LINES_APPLE_TEAM is set (the team ID of your free Apple
        /// account, from Xcode), Xcode signs it automatically with that team.
        /// </summary>
        public static void IosDevice() => BuildIos(false, IosDeviceFolder);

        /// <summary>An APK to install on the Android Emulator or a phone. Needs Unity's Android module (task 29).</summary>
        public static void Android()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Fail("Android Build Support isn't installed in Unity yet (task 29).");
                return;
            }
            try { Build(BuildTarget.Android, AndroidApk); }
            finally { BackToMac(); }
        }

        static void BuildIos(bool simulator, string folder)
        {
            var oldSdk = PlayerSettings.iOS.sdkVersion;
            var oldArch = PlayerSettings.iOS.simulatorSdkArchitecture;
            var team = simulator ? null : Environment.GetEnvironmentVariable("LINES_APPLE_TEAM");
            bool signing = !string.IsNullOrEmpty(team); // signing settings are only touched when a team is given
            var oldTeam = signing ? PlayerSettings.iOS.appleDeveloperTeamID : null;
            bool oldAutoSign = signing && PlayerSettings.iOS.appleEnableAutomaticSigning;
            try
            {
                PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                if (signing)
                {
                    PlayerSettings.iOS.appleDeveloperTeamID = team;
                    PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                }
                Build(BuildTarget.iOS, folder);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = oldSdk;
                PlayerSettings.iOS.simulatorSdkArchitecture = oldArch;
                if (signing)
                {
                    PlayerSettings.iOS.appleDeveloperTeamID = oldTeam;
                    PlayerSettings.iOS.appleEnableAutomaticSigning = oldAutoSign;
                }
                BackToMac();
            }
        }

        static void Build(BuildTarget target, string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                Fail($"Lines: {target} build failed ({report.summary.totalErrors} errors).");
            else
                Debug.Log($"Lines: {target} build finished: {path}");
        }

        static void BackToMac()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
        }

        static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
