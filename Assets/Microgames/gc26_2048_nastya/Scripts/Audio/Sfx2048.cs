using UnityEngine;
using UnityEngine.UI;

namespace GC26._2048Nastya
{
    /// <summary>
    /// Sound effects (TDD 10). Plays one-shots on the game's own 2D AudioSources; merges get a higher pitch for
    /// bigger tiles. The sound toggle only mutes these sources, never AudioListener.volume (that is global).
    /// </summary>
    public sealed class Sfx2048 : MonoBehaviour
    {
        [SerializeField] AudioSource source;
        [Tooltip("Separate source for merges, because their pitch changes")]
        [SerializeField] AudioSource mergeSource;

        [SerializeField] AudioClip slide;
        [SerializeField] AudioClip merge;
        [SerializeField] AudioClip invalid;
        [SerializeField] AudioClip win;
        [SerializeField] AudioClip lose;
        [SerializeField] AudioClip click;

        [Tooltip("Every Button under it clicks")]
        [SerializeField] Transform buttonsRoot;

        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [SerializeField] float maxMergePitch = 1.6f;

        bool _muted;

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                if (source != null) source.mute = value;
                if (mergeSource != null) mergeSource.mute = value;
            }
        }

        /// <summary>The last clip asked for (tests check what would have played, even when muted).</summary>
        public AudioClip LastPlayed { get; private set; }

        void Awake()
        {
            // Standalone the scene has no AudioListener (the shell brings one). Add ours only if nobody has one;
            // it is part of this scene, so it unloads with it.
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            if (buttonsRoot != null)
                foreach (var button in buttonsRoot.GetComponentsInChildren<Button>(true))
                    button.onClick.AddListener(Click);
        }

        public void Slide() => Play(source, slide, 1f);
        public void Invalid() => Play(source, invalid, 1f);
        public void Win() => Play(source, win, 1f);
        public void Lose() => Play(source, lose, 1f);
        public void Click() => Play(source, click, 1f);

        /// <summary>Merge sound for the largest tile a move created.</summary>
        public void Merge(int value) => Play(mergeSource != null ? mergeSource : source, merge, MergePitch(value, maxMergePitch));

        /// <summary>TDD 10: 1 + 0.05·log2(value), at most <paramref name="max"/>.</summary>
        public static float MergePitch(int value, float max = 1.6f) =>
            value < 2 ? 1f : Mathf.Min(max, 1f + 0.05f * Mathf.Log(value, 2f));

        void Play(AudioSource on, AudioClip clip, float pitch)
        {
            LastPlayed = clip;
            if (on == null || clip == null || _muted) return;
            on.pitch = pitch;
            on.PlayOneShot(clip, volume);
        }
    }
}
