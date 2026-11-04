using System;
using System.Linq;
using Lines.Core;
using UnityEngine;

namespace Lines.View
{
    /// <summary>
    /// What the 7 ball colors look like on screen: the blob creatures (MVP art, GDD → Art)
    /// or the generated glossy balls (fallback). Each game picks 7 of the 9 creatures at random,
    /// as in docs/prototype/lines-prototype.html.
    /// </summary>
    public sealed class PieceSet
    {
        public const int CreatureCount = 9;
        const string CreatureFolder = "Creatures/creature_";
        static readonly Sprite[] CreatureCache = new Sprite[CreatureCount];

        readonly int[] creatureForColor; // index = (int)BallColor - 1; null for balls

        public bool UsesCreatures => creatureForColor != null;

        /// <summary>Piece size compared to a cell (prototype: balls 80%, creatures 94%).</summary>
        public float SizeInCell => UsesCreatures ? 0.94f : 0.8f;

        PieceSet(int[] creatureForColor) { this.creatureForColor = creatureForColor; }

        public static PieceSet Balls() => new PieceSet(null);

        /// <summary>7 of the 9 creatures, picked with <paramref name="seed"/>. Falls back to balls if the images are missing.</summary>
        public static PieceSet RandomCreatures(int seed)
        {
            if (!CreaturesAvailable()) return Balls();
            var random = new GameRandom(seed);
            var all = Enumerable.Range(0, CreatureCount).ToArray();
            for (int i = all.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            return new PieceSet(all.Take(GameConfig.ColorCount).ToArray());
        }

        /// <summary>Which creature (0–8) stands for <paramref name="color"/>, or -1 when using balls.</summary>
        public int CreatureFor(BallColor color) =>
            UsesCreatures && color != BallColor.None ? creatureForColor[(int)color - 1] : -1;

        public Sprite SpriteFor(BallColor color)
        {
            int creature = CreatureFor(color);
            return creature >= 0 ? Creature(creature) : SpriteFactory.Ball(color);
        }

        public static bool CreaturesAvailable() => Creature(0) != null;

        static Sprite Creature(int index)
        {
            if (index < 0 || index >= CreatureCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (CreatureCache[index] == null) CreatureCache[index] = Resources.Load<Sprite>(CreatureFolder + index);
            return CreatureCache[index];
        }
    }
}
