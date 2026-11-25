using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GC26._2048.EditorTools
{
    /// <summary>
    /// GC26 → gc26_2048 → Build: raise the version and build gc26_2048_Main for the active platform
    /// (Windows or Android) in one click.
    ///  • Development Build — build number +1, development player, shows "0.0.1-dev.N" in the game.
    ///  • Release: Patch / Minor / Major — version +1 in that part, build number +1, the "unreleased" section of
    ///    CHANGELOG.md becomes the new version, release player.
    /// Changes CheeseVersion.cs, microgame.json and CHANGELOG.md only (never Player Settings or Build Settings);
    /// if the build fails, the three files are put back. Builds go to Builds/gc26_2048 (ignored by git).
    /// Batch mode: -executeMethod GC26._2048.EditorTools.CheeseBuildMenu.BuildFromCommandLine -cheeseBuild dev|patch|minor|major
    /// </summary>
    public static class CheeseBuildMenu
    {
        const string Menu = "GC26/gc26_2048/Build/";
        const string VersionFile = CheeseSceneBuilder.Folder + "/Scripts/Core/CheeseVersion.cs";
        const string PassportFile = CheeseSceneBuilder.Folder + "/microgame.json";
        const string ChangelogFile = CheeseSceneBuilder.Folder + "/CHANGELOG.md";
        const string FolderPref = "gc26_2048_build_folder";

        [MenuItem(Menu + "Development Build", false, 10)]
        static void DevelopmentBuild() => Run(null);

        [MenuItem(Menu + "Release: Patch (x.y.+1)", false, 30)]
        static void ReleasePatch() => Run(VersionPart.Patch);

        [MenuItem(Menu + "Release: Minor (x.+1.0)", false, 31)]
        static void ReleaseMinor() => Run(VersionPart.Minor);

        [MenuItem(Menu + "Release: Major (+1.0.0)", false, 32)]
        static void ReleaseMajor() => Run(VersionPart.Major);

        [MenuItem(Menu + "Show Version", false, 50)]
        static void ShowVersion()
        {
            string code = File.ReadAllText(VersionFile);
            EditorUtility.DisplayDialog("gc26_2048 version",
                $"Version: {CheeseVersionTool.ReadVersion(code)}\nBuild number: {CheeseVersionTool.ReadBuild(code)}\n" +
                $"Platform: {EditorUserBuildSettings.activeBuildTarget}\nBuilds folder: {OutputRoot}", "OK");
        }

        [MenuItem(Menu + "Open Builds Folder", false, 51)]
        static void OpenBuildsFolder()
        {
            Directory.CreateDirectory(OutputRoot);
            EditorUtility.RevealInFinder(OutputRoot);
        }

        [MenuItem(Menu + "Change Builds Folder…", false, 52)]
        static void ChangeBuildsFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Folder for gc26_2048 builds", OutputRoot, "");
            if (!string.IsNullOrEmpty(folder)) EditorPrefs.SetString(FolderPref, folder);
        }

        static string OutputRoot => EditorPrefs.GetString(FolderPref, Path.GetFullPath("Builds/gc26_2048"));

        public static void BuildFromCommandLine()
        {
            string kind = "dev";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-cheeseBuild") kind = args[i + 1].ToLowerInvariant();
            VersionPart? part = kind == "patch" ? VersionPart.Patch : kind == "minor" ? VersionPart.Minor
                : kind == "major" ? VersionPart.Major : (VersionPart?)null;
            bool ok = Run(part);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>release == null → development build.</summary>
        static bool Run(VersionPart? release)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Tell("Stop Play mode first", "Leave Play mode, then choose the build again.");
                return false;
            }

            var target = EditorUserBuildSettings.activeBuildTarget;
            string fileName;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneWindows: fileName = "gc26_2048.exe"; break;
                case BuildTarget.Android: fileName = "gc26_2048.apk"; break;
                default:
                    Tell("Platform not supported", $"Switch to Windows or Android in File → Build Profiles (now: {target}).");
                    return false;
            }

            string code = File.ReadAllText(VersionFile), passport = File.ReadAllText(PassportFile), changelog = File.ReadAllText(ChangelogFile);
            string oldVersion = CheeseVersionTool.ReadVersion(code);
            int oldBuild = CheeseVersionTool.ReadBuild(code);
            string version = release.HasValue ? CheeseVersionTool.Bump(oldVersion, release.Value) : oldVersion;
            int build = oldBuild + 1;
            string label = release.HasValue ? version : $"{version}-dev.{build}";
            string kind = release.HasValue ? "Release" : "Development";

            if (!Application.isBatchMode && !EditorUtility.DisplayDialog($"gc26_2048 — {kind} build",
                    $"Build {label} for {target}?\n\nVersion: {oldVersion} → {version}\nBuild number: {oldBuild} → {build}" +
                    (release.HasValue ? "\n\nCHANGELOG.md: the unreleased section becomes " + version + "." : ""),
                    "Build", "Cancel"))
                return false;

            // 1. raise the version; the player build compiles scripts from these files on disk
            File.WriteAllText(VersionFile, CheeseVersionTool.WriteBuild(CheeseVersionTool.WriteVersion(code, version), build));
            if (release.HasValue)
            {
                File.WriteAllText(PassportFile, CheeseVersionTool.WriteJsonVersion(passport, version));
                File.WriteAllText(ChangelogFile, CheeseVersionTool.ReleaseChangelog(changelog, version, DateTime.Now.ToString("yyyy-MM-dd")));
            }

            // 2. build
            string output = Path.Combine(OutputRoot, release.HasValue ? "release" : "development", label, target.ToString());
            if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { CheeseSceneBuilder.ScenePath },
                    locationPathName = Path.Combine(output, fileName),
                    target = target,
                    options = release.HasValue ? BuildOptions.None : BuildOptions.Development
                });
            }
            catch (Exception e)
            {
                Restore(code, passport, changelog);
                Tell("Build failed", e.Message);
                return false;
            }

            var s = report.summary;
            if (s.result != BuildResult.Succeeded)
            {
                Restore(code, passport, changelog);
                Tell("Build failed", $"{kind} build {label}: {s.result}, {s.totalErrors} error(s). The version was put back to {oldVersion} (build {oldBuild}). See the Console.");
                return false;
            }

            AssetDatabase.Refresh(); // the editor picks up the new version after the build
            Debug.Log($"[gc26_2048] {kind} build {label} for {target}: {s.totalSize / (1024 * 1024)} MB -> {output}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog($"gc26_2048 — {kind} build {label}",
                    $"Done: {output}\n\nCommit the new version: CheeseVersion.cs" + (release.HasValue ? ", microgame.json, CHANGELOG.md" : "") + ".",
                    "OK");
                EditorUtility.RevealInFinder(Path.Combine(output, fileName));
            }
            return true;
        }

        /// <summary>Puts back the three files exactly as they were before the build.</summary>
        static void Restore(string code, string passport, string changelog)
        {
            File.WriteAllText(VersionFile, code);
            File.WriteAllText(PassportFile, passport);
            File.WriteAllText(ChangelogFile, changelog);
            AssetDatabase.Refresh();
        }

        static void Tell(string title, string text)
        {
            if (Application.isBatchMode) Debug.LogError($"[gc26_2048] {title}: {text}");
            else EditorUtility.DisplayDialog(title, text, "OK");
        }
    }
}
