using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.Audio
{
    /// <summary>
    /// Plays the game's sounds and the "Firefly dance" music. Everything is made from code by
    /// <see cref="SoundBank"/> on a background thread when the game starts (a fraction of a second), so the game
    /// has no sound files. Music and Sounds follow the switches in Settings; the music fades in and out.
    /// Lives in the game's scene like everything else (no global state).
    /// </summary>
    public sealed class LinesAudio : MonoBehaviour
    {
        public const float MusicFadeSeconds = 1.5f;
        const int RememberedSounds = 64;

        AudioSource effects, music;
        readonly Dictionary<(LinesSound, int), AudioClip> clips = new Dictionary<(LinesSound, int), AudioClip>();
        readonly List<AudioClip> made = new List<AudioClip>();
        Task<SoundBank> making;
        float musicTarget;

        /// <summary>Sound effects play (the Sounds switch).</summary>
        public bool SoundsOn { get; set; } = true;

        /// <summary>The music plays (the Music switch); it fades in or out over <see cref="MusicFadeSeconds"/>.</summary>
        public bool MusicOn { get; private set; }

        /// <summary>All sounds and the music are made and ready to play.</summary>
        public bool Ready { get; private set; }

        /// <summary>The music is audible now (tests).</summary>
        public bool MusicPlaying => music != null && music.isPlaying && music.volume > 0f;

        /// <summary>The last sounds the game asked for while Sounds was on, oldest first (tests).</summary>
        public readonly List<LinesSound> Played = new List<LinesSound>();

        public static LinesAudio Create(Transform parent, bool musicOn, bool soundsOn, bool makeInBackground = true)
        {
            var go = new GameObject("Lines audio");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<LinesAudio>();
            audio.Build(musicOn, soundsOn, makeInBackground);
            return audio;
        }

        void Build(bool musicOn, bool soundsOn, bool makeInBackground)
        {
            // Unity hears through an AudioListener; the game's scene has none, so it brings its own
            // (unless the shared menu already has one).
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0f;
            SoundsOn = soundsOn;
            SetMusic(musicOn);
            if (makeInBackground) making = Task.Run(() => SoundBank.Make());
            else Use(SoundBank.Make());
        }

        /// <summary>Follows the Settings switches.</summary>
        public void Apply(bool musicOn, bool soundsOn)
        {
            SoundsOn = soundsOn;
            SetMusic(musicOn);
        }

        void SetMusic(bool on)
        {
            MusicOn = on;
            musicTarget = on ? 1f : 0f;
            if (on && Ready && !music.isPlaying) music.Play();
        }

        /// <summary>
        /// Plays <paramref name="sound"/> if Sounds is on. <paramref name="detail"/>: for <see cref="LinesSound.Step"/>
        /// which cell of the path (the notes rise), for <see cref="LinesSound.Clear"/> the length of the longest line.
        /// </summary>
        public void Play(LinesSound sound, int detail = 0)
        {
            if (!SoundsOn) return;
            Played.Add(sound);
            if (Played.Count > RememberedSounds) Played.RemoveAt(0);
            if (!Ready) return;
            int variant = sound == LinesSound.Step ? detail % SoundBank.StepNotes
                : sound == LinesSound.Clear ? Mathf.Clamp(detail, SoundBank.ShortestLine, SoundBank.LongestLine)
                : 0;
            if (clips.TryGetValue((sound, variant), out var clip)) effects.PlayOneShot(clip);
        }

        /// <summary>These buttons tick softly when tapped (the board has its own sounds).</summary>
        public void AddTapSounds(IEnumerable<Button> buttons)
        {
            foreach (var button in buttons) button.onClick.AddListener(() => Play(LinesSound.Tap));
        }

        void Update()
        {
            if (making != null && making.IsCompleted)
            {
                var task = making;
                making = null;
                if (task.Status == TaskStatus.RanToCompletion) Use(task.Result);
                else Debug.LogError("Lines: the sounds could not be made: " + task.Exception);
            }
            if (Ready) FadeMusic(Time.unscaledDeltaTime);
        }

        /// <summary>Moves the music's volume towards on or off (every frame; tests call it directly).</summary>
        public void FadeMusic(float seconds)
        {
            music.volume = Mathf.MoveTowards(music.volume, musicTarget, seconds / MusicFadeSeconds);
            if (musicTarget > 0f && !music.isPlaying) music.Play();
            if (musicTarget == 0f && music.volume == 0f && music.isPlaying) music.Pause();
        }

        void Use(SoundBank bank)
        {
            foreach (var (sound, variant) in bank.EffectKeys)
                clips[(sound, variant)] = Clip(sound + " " + variant, bank.Effect(sound, variant), SoundBank.EffectRate);
            if (bank.Music != null)
            {
                music.clip = Clip("Firefly dance", bank.Music, SoundBank.MusicRate);
                if (MusicOn) music.Play();
            }
            Ready = true;
        }

        AudioClip Clip(string name, float[] samples, int rate)
        {
            var clip = AudioClip.Create("Lines " + name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            made.Add(clip);
            return clip;
        }

        /// <summary>The clip for a sound, once made (tests).</summary>
        public AudioClip ClipFor(LinesSound sound, int variant = 0) => clips.TryGetValue((sound, variant), out var c) ? c : null;

        public AudioClip MusicClip => music.clip;

        /// <summary>The music's volume now, 0–1 (tests).</summary>
        public float MusicVolume => music.volume;

        void OnDestroy()
        {
            foreach (var clip in made)
                if (clip != null) { if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip); }
        }
    }
}
