using NUnit.Framework;

namespace GC26._2048.Tests
{
    public class CheeseLocaleTests
    {
        [Test]
        public void EnglishIsTheDefault()
        {
            Assert.AreEqual(CheeseLanguage.English, new CheeseLocale().Language);
            Assert.AreEqual("Mousey's Cheese", new CheeseLocale()["title"]);
        }

        [Test]
        public void EveryTextHasBothLanguages()
        {
            foreach (var key in CheeseLocale.Keys)
            {
                Assert.IsNotEmpty(CheeseLocale.Get(key, CheeseLanguage.English), key);
                Assert.IsNotEmpty(CheeseLocale.Get(key, CheeseLanguage.Russian), key);
            }
        }

        [Test]
        public void EveryItemHasBothNames()
        {
            for (int level = 1; level <= CheeseRules.CheeseLevel; level++)
            foreach (var lang in new[] { CheeseLanguage.English, CheeseLanguage.Russian })
            {
                Assert.IsNotEmpty(CheeseLocale.ItemName(level, lang), $"name {level} {lang}");
                Assert.IsNotEmpty(CheeseLocale.ItemShort(level, lang), $"short {level} {lang}");
            }
        }

        [Test]
        public void SwitchingFlipsBetweenTheTwoLanguages()
        {
            var loc = new CheeseLocale();
            loc.Set(loc.Next);
            Assert.AreEqual(CheeseLanguage.Russian, loc.Language);
            Assert.AreEqual("Мышкин сыр", loc["title"]);
            loc.Set(loc.Next);
            Assert.AreEqual(CheeseLanguage.English, loc.Language);
        }
    }
}
