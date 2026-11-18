using UnityEngine;

namespace GC26.Findepair
{
    /// <summary>One pair of cards: the picture on the face and the color of its neon frame.</summary>
    [CreateAssetMenu(menuName = "Findepair/Card Data", fileName = "CardData")]
    public class CardData : ScriptableObject
    {
        [Tooltip("Unique id; two cards match when their ids are equal.")]
        public string id;

        [Tooltip("For accessibility and debugging only; names are not drawn on the cards.")]
        public string displayName;

        [Tooltip("Card face, 900 x 1200 with a transparent background.")]
        public Sprite front;

        [Tooltip("Color of the neon frame and glow around the face.")]
        public Color glow = new Color(1f, 0.184f, 0.702f);
    }
}
