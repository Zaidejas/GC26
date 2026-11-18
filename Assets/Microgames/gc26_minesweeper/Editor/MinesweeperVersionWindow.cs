using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GC26.Minesweeper.EditorTools
{
    // GC26 → Minesweeper → Version:
    //   Bump Version...        – a window: describe the change, pick Patch / Minor / Major.
    //                            Updates Scripts/MinesweeperVersion.cs and adds a row to the README changelog.
    //   Show Current Version   – shows the version and what the three numbers mean.
    public class MinesweeperVersionWindow : EditorWindow
    {
        const string Folder = "Assets/Microgames/" + MinesweeperGame.Id;
        const string VersionFile = Folder + "/Scripts/MinesweeperVersion.cs";
        const string ReadmeFile = Folder + "/README.md";

        // Finds: Number = "MAJOR.MINOR.PATCH"
        static readonly Regex NumberPattern = new Regex("Number\\s*=\\s*\"(\\d+)\\.(\\d+)\\.(\\d+)\"");

        const string Explanation =
            "A version number has three parts: MAJOR.MINOR.PATCH, for example 1.4.2.\n\n" +
            "• PATCH (the last number) – bug fixes and small tweaks. 0.1.1 → 0.1.2\n" +
            "• MINOR (the middle number) – new features. 0.1.2 → 0.2.0 (PATCH goes back to 0)\n" +
            "• MAJOR (the first number) – a big redesign or a new edition. 0.2.0 → 1.0.0 (MINOR and PATCH go back to 0)\n\n" +
            "Bump the version every time you change the game, so everyone can see at the bottom of the screen " +
            "which version they are playing and which changes it has. 0.x.x means \"still in development\"; " +
            "1.0.0 is the first finished release.";

        string changes = "";
        Vector2 scroll;

        [MenuItem("GC26/Minesweeper/Version/Bump Version...", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<MinesweeperVersionWindow>(true, "Red Flags – Bump Version");
            window.minSize = new Vector2(480, 460);
        }

        [MenuItem("GC26/Minesweeper/Version/Show Current Version", priority = 101)]
        public static void ShowCurrent()
        {
            string current = TryRead(out int major, out int minor, out int patch) ? $"{major}.{minor}.{patch}" : "(not found)";
            EditorUtility.DisplayDialog("Red Flags version", $"Current version: {current}\n\n{Explanation}", "OK");
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!TryRead(out int major, out int minor, out int patch))
            {
                EditorGUILayout.HelpBox($"Couldn't find Number = \"x.y.z\" in {VersionFile}.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current version", $"{major}.{minor}.{patch}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Explanation, MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1. What changed? (added to the changelog in README.md)", EditorStyles.boldLabel);
            changes = EditorGUILayout.TextArea(changes, GUILayout.MinHeight(48));
            bool described = !string.IsNullOrWhiteSpace(changes);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. Pick the kind of change", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!described))
            {
                BumpButton("Patch", "Bug fixes and small tweaks", major, minor, patch + 1);
                BumpButton("Minor", "New features", major, minor + 1, 0);
                BumpButton("Major", "Big redesign or a new edition of the game", major + 1, 0, 0);
            }
            if (!described) EditorGUILayout.HelpBox("Describe the change first; the buttons unlock then.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Afterwards: rebuild (GC26 → Minesweeper → Build Windows Player) and commit the game folder.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void BumpButton(string kind, string when, int major, int minor, int patch)
        {
            string next = $"{major}.{minor}.{patch}";
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"{kind}  →  {next}", GUILayout.Width(190), GUILayout.Height(28))) Apply(next);
            EditorGUILayout.LabelField(when, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        void Apply(string next)
        {
            TryRead(out int major, out int minor, out int patch);
            string old = $"{major}.{minor}.{patch}";

            string code = File.ReadAllText(VersionFile);
            File.WriteAllText(VersionFile, NumberPattern.Replace(code, $"Number = \"{next}\"", 1));
            bool logged = AddChangelogRow(next, changes);

            AssetDatabase.ImportAsset(VersionFile);       // recompiles, so the game shows the new number
            AssetDatabase.ImportAsset(ReadmeFile);
            Debug.Log($"[{MinesweeperGame.Id}] version {old} → {next}: {changes.Trim()}");

            changes = "";
            EditorUtility.DisplayDialog("Version bumped",
                $"{old} → {next}\n\n" +
                (logged ? "A row was added to the changelog in README.md.\n\n" : "Couldn't find the changelog table in README.md; add the row by hand.\n\n") +
                "Next: rebuild the Windows player and commit the game folder.", "OK");
        }

        // Adds "| version | changes |" right under the header of the changelog table (newest first).
        static bool AddChangelogRow(string version, string text)
        {
            if (!File.Exists(ReadmeFile)) return false;
            var lines = File.ReadAllLines(ReadmeFile).ToList();
            int header = lines.FindIndex(l => l.StartsWith("| Version |"));
            if (header < 0 || header + 1 >= lines.Count) return false;

            string clean = text.Trim().Replace("\r", "").Replace("\n", " ").Replace("|", "/");
            lines.Insert(header + 2, $"| {version} | {clean} |");
            File.WriteAllLines(ReadmeFile, lines);
            return true;
        }

        // Reads the number from the file (not the compiled constant, which is stale until Unity recompiles).
        static bool TryRead(out int major, out int minor, out int patch)
        {
            major = minor = patch = 0;
            if (!File.Exists(VersionFile)) return false;
            Match m = NumberPattern.Match(File.ReadAllText(VersionFile));
            if (!m.Success) return false;
            major = int.Parse(m.Groups[1].Value);
            minor = int.Parse(m.Groups[2].Value);
            patch = int.Parse(m.Groups[3].Value);
            return true;
        }
    }
}
