using System;
using System.Text.RegularExpressions;

namespace GC26._2048.EditorTools
{
    public enum VersionPart
    {
        Patch,
        Minor,
        Major
    }

    /// <summary>
    /// Text rewrites behind the Build menu: raise MAJOR.MINOR.PATCH, set the build number in CheeseVersion.cs,
    /// the version in microgame.json, and turn the "unreleased" section of CHANGELOG.md into a release.
    /// Pure string work, covered by edit-mode tests.
    /// </summary>
    public static class CheeseVersionTool
    {
        public const string UnreleasedHeading = "## Не выпущено";

        static readonly Regex CodeVersion = new Regex("(const string Current = \")([^\"]*)(\";)");
        static readonly Regex CodeBuild = new Regex(@"(const int Build = )(\d+)(;)");
        static readonly Regex JsonVersion = new Regex("(\"version\"\\s*:\\s*\")([^\"]*)(\")");
        static readonly Regex SemVer = new Regex(@"^(\d+)\.(\d+)\.(\d+)$");

        public static string Bump(string version, VersionPart part)
        {
            var m = SemVer.Match(version ?? "");
            if (!m.Success) throw new FormatException($"Version '{version}' is not MAJOR.MINOR.PATCH");
            int major = int.Parse(m.Groups[1].Value), minor = int.Parse(m.Groups[2].Value), patch = int.Parse(m.Groups[3].Value);
            switch (part)
            {
                case VersionPart.Major: return $"{major + 1}.0.0";
                case VersionPart.Minor: return $"{major}.{minor + 1}.0";
                default: return $"{major}.{minor}.{patch + 1}";
            }
        }

        public static string ReadVersion(string code) => Read(CodeVersion, code, "Current");

        public static int ReadBuild(string code) => int.Parse(Read(CodeBuild, code, "Build"));

        public static string WriteVersion(string code, string version) => Write(CodeVersion, code, version, "Current");

        public static string WriteBuild(string code, int build) => Write(CodeBuild, code, build.ToString(), "Build");

        public static string WriteJsonVersion(string json, string version)
        {
            if (JsonVersion.IsMatch(json)) return JsonVersion.Replace(json, m => m.Groups[1].Value + version + m.Groups[3].Value, 1);
            // no "version" yet: add it as the last field
            int end = json.LastIndexOf('}');
            if (end < 0) throw new FormatException("microgame.json is not a JSON object");
            string head = json.Substring(0, end).TrimEnd();
            return head + ",\n  \"version\": \"" + version + "\"\n" + json.Substring(end);
        }

        /// <summary>
        /// The "unreleased" section becomes "## version — date" and a fresh empty "unreleased" section goes on top.
        /// Without such a section, a release heading is inserted before the first version.
        /// </summary>
        public static string ReleaseChangelog(string text, string version, string date)
        {
            string release = $"## {version} — {date}";
            int i = text.IndexOf(UnreleasedHeading, StringComparison.Ordinal);
            if (i >= 0)
                return text.Substring(0, i) + UnreleasedHeading + "\n\n" + release + text.Substring(i + UnreleasedHeading.Length);
            int first = text.IndexOf("\n## ", StringComparison.Ordinal);
            string entry = release + "\n\n- (описание изменений)\n\n";
            return first < 0 ? text.TrimEnd() + "\n\n" + entry : text.Insert(first + 1, entry);
        }

        static string Read(Regex re, string text, string what)
        {
            var m = re.Match(text);
            if (!m.Success) throw new FormatException($"CheeseVersion.cs has no '{what}' constant");
            return m.Groups[2].Value;
        }

        static string Write(Regex re, string text, string value, string what)
        {
            if (!re.IsMatch(text)) throw new FormatException($"CheeseVersion.cs has no '{what}' constant");
            return re.Replace(text, m => m.Groups[1].Value + value + m.Groups[3].Value, 1);
        }
    }
}
