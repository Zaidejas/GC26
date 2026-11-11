using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GC26.Core.Editor
{
    /// <summary>
    /// GC26 → Sync Microgames: reads every Assets/Microgames/*/microgame.json,
    /// puts the entry scenes into Build Settings and writes the runtime registry.
    /// Run by the organizer on main after merging a game branch.
    /// </summary>
    public static class MicrogameSync
    {
        const string GamesDir = "Assets/Microgames";
        const string ShellScenesDir = "Assets/Core/Scenes";
        const string RegistryPath = "Assets/Core/Resources/" + MicrogameRegistry.ResourcePath + ".json";

        [MenuItem("GC26/Sync Microgames")]
        public static void Sync()
        {
            var games = new List<MicrogameManifest>();
            var scenes = new List<EditorBuildSettingsScene>();

            if (Directory.Exists(ShellScenesDir))
                scenes.AddRange(Directory.GetFiles(ShellScenesDir, "*.unity")
                    .OrderBy(p => p)
                    .Select(p => new EditorBuildSettingsScene(p.Replace('\\', '/'), true)));

            foreach (var dir in Directory.GetDirectories(GamesDir).OrderBy(d => d))
            {
                var json = Path.Combine(dir, "microgame.json");
                if (!File.Exists(json)) { Debug.LogWarning($"[GC26] skip {dir}: no microgame.json"); continue; }

                var game = JsonUtility.FromJson<MicrogameManifest>(File.ReadAllText(json));
                var scenePath = $"{dir.Replace('\\', '/')}/Scenes/{game.entry_scene}.unity";
                if (!File.Exists(scenePath)) { Debug.LogError($"[GC26] {game.id}: missing {scenePath}"); continue; }

                games.Add(game);
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath));
            File.WriteAllText(RegistryPath, JsonUtility.ToJson(new MicrogameRegistry { games = games.ToArray() }, true));
            AssetDatabase.Refresh();
            Debug.Log($"[GC26] synced {games.Count} microgame(s): {string.Join(", ", games.Select(g => g.id))}");
        }
    }
}
