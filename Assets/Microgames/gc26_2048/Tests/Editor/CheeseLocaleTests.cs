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
        public void TextsShowTheMoveLimit()
        {
            string limit = CheeseRules.MoveLimit.ToString();
            foreach (var language in new[] { CheeseLanguage.English, CheeseLanguage.Russian })
            {
                StringAssert.Contains(limit, CheeseLocale.Get("facts", language), $"facts, {language}");
                StringAssert.Contains(limit, CheeseLocale.Get("rules_text", language), $"rules, {language}");
            }
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
        public void EveryItemHasANameInBothLanguages()
        {
            for (int level = 1; level <= CheeseRules.CheeseLevel; level++)
            foreach (var lang in new[] { CheeseLanguage.English, CheeseLanguage.Russian })
                Assert.IsNotEmpty(CheeseLocale.ItemName(level, lang), $"name {level} {lang}");
        }

        [Test]
        public void ItemsUseTheTileNamesEverywhere()
        {
            Assert.AreEqual("Curd", CheeseLocale.ItemName(7, CheeseLanguage.English));
            Assert.AreEqual("Творог", CheeseLocale.ItemName(7, CheeseLanguage.Russian));
            Assert.AreEqual("Grass", CheeseLocale.ItemName(1, CheeseLanguage.English));
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
