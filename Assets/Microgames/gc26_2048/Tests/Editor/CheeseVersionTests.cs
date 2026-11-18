using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace GC26._2048.Tests
{
    public class CheeseVersionTests
    {
        [Serializable]
        class Passport
        {
            public string id;
            public string version;
        }

        [Test]
        public void VersionIsMajorMinorPatch()
        {
            StringAssert.IsMatch(@"^\d+\.\d+\.\d+$", CheeseVersion.Current);
        }

        [Test]
        public void MicrogameJsonHasTheSameVersion()
        {
            var json = File.ReadAllText("Assets/Microgames/gc26_2048/microgame.json");
            var passport = JsonUtility.FromJson<Passport>(json);
            Assert.AreEqual("gc26_2048", passport.id);
            Assert.AreEqual(CheeseVersion.Current, passport.version,
                "Update \"version\" in microgame.json together with CheeseVersion.Current");
        }

        [Test]
        public void ChangelogHasAnEntryForTheCurrentVersion()
        {
            var changelog = File.ReadAllText("Assets/Microgames/gc26_2048/CHANGELOG.md");
            StringAssert.IsMatch("## " + Regex.Escape(CheeseVersion.Current) + @"\b", changelog);
        }
    }
}
