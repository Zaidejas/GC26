using GC26._2048.EditorTools;
using NUnit.Framework;

namespace GC26._2048.Tests
{
    public class CheeseVersionToolTests
    {
        const string Code = "public const string Current = \"0.0.1\";\npublic const int Build = 7;";

        [TestCase("0.0.1", VersionPart.Patch, "0.0.2")]
        [TestCase("0.0.9", VersionPart.Minor, "0.1.0")]
        [TestCase("0.4.3", VersionPart.Major, "1.0.0")]
        [TestCase("1.2.3", VersionPart.Patch, "1.2.4")]
        public void BumpRaisesTheRightPart(string from, VersionPart part, string to)
        {
            Assert.AreEqual(to, CheeseVersionTool.Bump(from, part));
        }

        [Test]
        public void ReadsAndWritesVersionAndBuildInCode()
        {
            Assert.AreEqual("0.0.1", CheeseVersionTool.ReadVersion(Code));
            Assert.AreEqual(7, CheeseVersionTool.ReadBuild(Code));
            var code = CheeseVersionTool.WriteBuild(CheeseVersionTool.WriteVersion(Code, "0.1.0"), 8);
            Assert.AreEqual("0.1.0", CheeseVersionTool.ReadVersion(code));
            Assert.AreEqual(8, CheeseVersionTool.ReadBuild(code));
        }

        [Test]
        public void WritesVersionIntoMicrogameJson()
        {
            var json = "{\n  \"id\": \"gc26_2048\",\n  \"version\": \"0.0.1\"\n}";
            StringAssert.Contains("\"version\": \"0.0.2\"", CheeseVersionTool.WriteJsonVersion(json, "0.0.2"));
            var noVersion = "{\n  \"id\": \"gc26_2048\"\n}";
            StringAssert.Contains("\"version\": \"0.0.2\"", CheeseVersionTool.WriteJsonVersion(noVersion, "0.0.2"));
        }

        [Test]
        public void ReleaseTurnsUnreleasedIntoAVersionAndOpensANewOne()
        {
            var log = "# Changelog\n\n## Не выпущено\n\n- Icons\n\n## 0.0.1 — first\n";
            var result = CheeseVersionTool.ReleaseChangelog(log, "0.0.2", "2026-11-18");
            Assert.AreEqual("# Changelog\n\n## Не выпущено\n\n## 0.0.2 — 2026-11-18\n\n- Icons\n\n## 0.0.1 — first\n", result);
        }

        [Test]
        public void ReleaseWithoutUnreleasedSectionAddsAHeadingAboveTheLastVersion()
        {
            var log = "# Changelog\n\n## 0.0.1 — first\n";
            var result = CheeseVersionTool.ReleaseChangelog(log, "0.0.2", "2026-11-18");
            StringAssert.Contains("## 0.0.2 — 2026-11-18", result);
            Assert.Less(result.IndexOf("## 0.0.2"), result.IndexOf("## 0.0.1"));
        }
    }
}
