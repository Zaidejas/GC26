using System;
using System.Collections.Generic;

namespace GC26.Tanki.Game
{
    /// <summary>Awards: each pays parts once, the first time its goal is reached. Same list as the browser version.</summary>
    public static class TankiAwards
    {
        public sealed class Award
        {
            public string Id;
            public string Name;
            public string Text;
            public int Reward;
            public int Goal;
            public Func<SaveData, int> Value;
        }

        public static readonly Award[] All =
        {
            new Award { Id = "first", Name = "Первый подбитый", Text = "Подбей 1 танк", Reward = 5, Goal = 1, Value = s => s.Kills },
            new Award { Id = "hunter", Name = "Охотник", Text = "Подбей 100 танков", Reward = 30, Goal = 100, Value = s => s.Kills },
            new Award { Id = "veteran", Name = "Ветеран", Text = "Подбей 500 танков", Reward = 80, Goal = 500, Value = s => s.Kills },
            new Award { Id = "boss", Name = "Гроза боссов", Text = "Победи босса", Reward = 25, Goal = 1, Value = s => s.Bosses },
            new Award { Id = "bosses", Name = "Укротитель", Text = "Победи 4 боссов", Reward = 60, Goal = 4, Value = s => s.Bosses },
            new Award { Id = "crates", Name = "Сносим всё", Text = "Разбей 200 ящиков", Reward = 20, Goal = 200, Value = s => s.Crates },
            new Award { Id = "sapper", Name = "Сапёр", Text = "Расстреляй 10 мин", Reward = 20, Goal = 10, Value = s => s.MinesShot },
            new Award { Id = "power", Name = "Сила реактора", Text = "Используй способность 25 раз", Reward = 25, Goal = 25, Value = s => s.Abilities },
            new Award { Id = "flawless", Name = "Без царапин", Text = "Пройди карту без потерь", Reward = 15, Goal = 1, Value = s => s.Flawless },
            new Award { Id = "stars10", Name = "Звёздный", Text = "Собери 10 звёзд", Reward = 30, Goal = 10, Value = s => s.TotalStars },
            new Award { Id = "stars60", Name = "Безупречный", Text = "Собери все 60 звёзд кампании", Reward = 150, Goal = 60, Value = s => s.CampaignStars },
            new Award { Id = "wave10", Name = "Несгибаемый", Text = "Дойди до 10-й волны в выживании", Reward = 50, Goal = 10, Value = s => s.BestWave },
            new Award { Id = "breaker", Name = "Штурмовик", Text = "Возьми первую крепость", Reward = 30, Goal = 1, Value = s => s.AssaultWins },
            new Award { Id = "conqueror", Name = "Покоритель", Text = "Возьми все 6 крепостей", Reward = 100, Goal = 6, Value = s => s.AssaultWins },
            new Award { Id = "garage", Name = "Коллекционер", Text = "Собери все 4 танка", Reward = 50, Goal = 4, Value = s => s.Owned.Count }
        };

        /// <summary>Pays every award newly reached and returns their lines for the toasts.</summary>
        public static List<string> Check(SaveData save)
        {
            var earned = new List<string>();
            foreach (Award award in All)
            {
                if (save.Awards.Contains(award.Id) || award.Value(save) < award.Goal) continue;
                save.Awards.Add(award.Id);
                save.Parts += award.Reward;
                earned.Add("Награда «" + award.Name + "»: +" + award.Reward + " деталей");
            }
            if (earned.Count > 0) save.Save();
            return earned;
        }
    }
}
