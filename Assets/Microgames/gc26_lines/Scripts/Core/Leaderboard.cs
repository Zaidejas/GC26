using System;
using System.Collections.Generic;

namespace GC26.Lines.Core
{
    /// <summary>One finished game on the leaderboard.</summary>
    [Serializable]
    public class ScoreEntry
    {
        public int score;
        public string name;
        /// <summary>When the game was saved, in milliseconds since 1 Jan 1970 (UTC).</summary>
        public long time;

        public ScoreEntry() { }
        public ScoreEntry(int score, string name, long time) { this.score = score; this.name = name; this.time = time; }
    }

    /// <summary>
    /// The local top 10 of finished games and the player-name memory (GDD → Scoring → Leaderboard,
    /// Screens → Player name on game over). Plain fields, so it can be written as JSON.
    /// </summary>
    [Serializable]
    public class Leaderboard
    {
        public const int MaxEntries = 10;
        public const int MaxNameLength = 16;
        public const string PlaceholderPrefix = "Player";

        /// <summary>Best first. Equal scores: the older one first.</summary>
        public List<ScoreEntry> entries = new List<ScoreEntry>();
        /// <summary>The last name the player typed themselves; empty if they never typed one.</summary>
        public string lastName = "";
        /// <summary>How many PlayerN names have been used so far.</summary>
        public int placeholdersUsed;

        /// <summary>
        /// The word before the number in placeholder names, in the player's language ("Player", "Игрок").
        /// Not saved: the game sets it from the current language.
        /// </summary>
        [NonSerialized] public string placeholderPrefix = PlaceholderPrefix;

        public int Count => entries.Count;

        /// <summary>The placeholder the next unnamed score gets: Player1, Player2, …</summary>
        public string NextPlaceholder => (string.IsNullOrEmpty(placeholderPrefix) ? PlaceholderPrefix : placeholderPrefix) + (placeholdersUsed + 1);

        /// <summary>What the game-over name field starts with: the last typed name, or the next PlayerN.</summary>
        public string DefaultName => string.IsNullOrEmpty(lastName) ? NextPlaceholder : lastName;

        /// <summary>Cleans what the player typed: trimmed, at most 16 characters; empty means the placeholder.</summary>
        public string CleanName(string typed)
        {
            var name = (typed ?? "").Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength).TrimEnd();
            return name.Length == 0 ? NextPlaceholder : name;
        }

        /// <summary>
        /// Saves a finished game under the name from the field. A PlayerN name moves the placeholder
        /// count on; any other name is remembered for next time. Returns the place (0 = best),
        /// or -1 when the score didn't make the top 10.
        /// </summary>
        public int Record(int score, string typedName, long time) => Add(score, RememberName(typedName), time);

        /// <summary>
        /// The name a score is saved under (see <see cref="CleanName"/>): a PlayerN name moves the placeholder count on,
        /// any other name is remembered for next time. Timed scores go to another list but use this memory too.
        /// </summary>
        public string RememberName(string typedName)
        {
            var name = CleanName(typedName);
            if (name == NextPlaceholder) placeholdersUsed++;
            else lastName = name;
            return name;
        }

        /// <summary>
        /// Adds a score below every entry with the same or higher score (the older score keeps its place),
        /// keeps the best 10, and returns the new entry's place, or -1 if it isn't in the top 10.
        /// </summary>
        public int Add(int score, string name, long time)
        {
            if (score <= 0) return -1; // 0 points never go on the leaderboard (GDD → Scoring)
            int place = 0;
            while (place < entries.Count && entries[place].score >= score) place++;
            if (place >= MaxEntries) return -1;
            entries.Insert(place, new ScoreEntry(score, name, time));
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            return place;
        }

        /// <summary>
        /// Whether <paramref name="score"/> would get on the board: more than 0 points, and either a free place or
        /// higher than the 10th (an equal score loses: older scores win ties). Game over asks for a name only then.
        /// </summary>
        public bool Qualifies(int score) =>
            score > 0 && (entries.Count < MaxEntries || score > entries[MaxEntries - 1].score);

        /// <summary>The 10th place's score when the board is full, else -1 (game over's "not enough" goal).</summary>
        public int TenthScore => entries.Count >= MaxEntries ? entries[MaxEntries - 1].score : -1;

        /// <summary>
        /// Repairs data read from a file: drops broken entries, puts them in order (older first on ties)
        /// and keeps the best 10.
        /// </summary>
        public void Tidy()
        {
            if (entries == null) entries = new List<ScoreEntry>();
            entries.RemoveAll(e => e == null || e.score <= 0); // 0-point games (saved before that rule) go too
            foreach (var e in entries)
            {
                e.name = (e.name ?? "").Trim();
                if (e.name.Length == 0) e.name = PlaceholderPrefix;
                if (e.name.Length > MaxNameLength) e.name = e.name.Substring(0, MaxNameLength);
            }
            // A stable sort: entries with equal score and time keep the order they had.
            var sorted = new List<ScoreEntry>(entries);
            sorted.Sort((a, b) =>
            {
                int byScore = b.score.CompareTo(a.score);
                if (byScore != 0) return byScore;
                int byTime = a.time.CompareTo(b.time);
                return byTime != 0 ? byTime : entries.IndexOf(a).CompareTo(entries.IndexOf(b));
            });
            entries = sorted;
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            if (lastName == null) lastName = "";
            if (placeholdersUsed < 0) placeholdersUsed = 0;
        }
    }
}
