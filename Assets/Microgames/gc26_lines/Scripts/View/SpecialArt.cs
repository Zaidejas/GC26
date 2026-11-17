using UnityEngine;

namespace GC26.Lines.View
{
    /// <summary>
    /// The special mushrooms' pictures (GDD → Art → Special pieces), exported from the prototype as PNGs in
    /// Resources/gc26_lines/Specials: the frosted ice cube drawn over frozen balls (L15), the puffball and its fuse
    /// spark (L16), and the tutorial hand (L17). The rainbow mushroom is the 8th mushroom picture (<see cref="PieceSet"/>).
    /// </summary>
    public static class SpecialArt
    {
        const string Folder = "gc26_lines/Specials/";
        static Sprite ice, puffball, spark, hand;

        public static Sprite Ice() => ice != null ? ice : (ice = Load("ice"));
        public static Sprite Puffball() => puffball != null ? puffball : (puffball = Load("puffball"));
        public static Sprite PuffballSpark() => spark != null ? spark : (spark = Load("puffball_spark"));
        public static Sprite Hand() => hand != null ? hand : (hand = Load("hand"));

        // A missing picture (no Git LFS) falls back to a soft disc, so nothing breaks.
        static Sprite Load(string name) => Resources.Load<Sprite>(Folder + name) ?? SpriteFactory.SoftDisc();
    }
}
