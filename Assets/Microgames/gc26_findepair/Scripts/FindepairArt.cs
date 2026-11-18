using System;
using UnityEngine;

namespace GC26.Findepair
{
    /// <summary>All pictures the game uses. Filled in on the FindepairGame object in the scene.</summary>
    [Serializable]
    public class FindepairArt
    {
        [Header("Cards")]
        public CardData[] cards = Array.Empty<CardData>();
        public Sprite cardBack;

        [Header("Backgrounds")]
        public Sprite menuWall;
        public Sprite boardCork;

        [Header("Effects")]
        public Sprite sparkle;
        public Sprite flash;
        public Sprite heart;
        public Sprite star;
        public Sprite bolt;

        [Header("UI shapes (white, tinted in code)")]
        public Sprite rounded;
        public Sprite frame;
        public Sprite glow;
        public Sprite circle;
    }
}
