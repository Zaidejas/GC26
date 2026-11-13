using System;
using System.Collections.Generic;

namespace GC26.Lines.Audio
{
    /// <summary>The game's moments that make a sound (GDD → Sounds and music).</summary>
    public enum LinesSound { Tap, Select, Step, Land, Clear, Spawn, Blocked, GameOver, Record }

    /// <summary>
    /// Every sound and the music, made by <see cref="Synth"/> from the recipes picked on the Forest Sounds page
    /// (prototype/forest-sounds.html). All notes come from the D major pentatonic scale, so they fit together.
    /// Plain C#: <see cref="Make"/> can run on a background thread.
    /// </summary>
    public sealed class SoundBank
    {
        public const int EffectRate = 44100, MusicRate = 22050;
        public const float EffectLevel = 0.8f, EffectReverb = 0.45f; // the prototype's sound bus
        public const float MusicLevel = 0.45f, MusicReverb = 0.9f;   // ...and its music bus
        public const float Beat = 0.36f;                             // Firefly dance: one step every 0.36 s
        public const int BeatsPerChord = 16, MusicBeats = 128;       // 4 chords, played twice with small changes
        public const float RingOut = 2.5f;                            // room for the reverb after each sound

        /// <summary>D major pentatonic from D5 (note 74) up to F#7.</summary>
        public static readonly int[] Pentatonic = { 74, 76, 78, 81, 83, 86, 88, 90, 93, 95, 98, 100, 102 };
        static readonly int[][] Chords = { new[] { 50, 54, 57, 61 }, new[] { 47, 50, 54, 57 }, new[] { 43, 47, 50, 54 }, new[] { 45, 50, 52, 57 } };

        /// <summary>The move's sparkle run has one note per cell, from these six.</summary>
        public const int StepNotes = 6;
        /// <summary>Line-clear harps for lines of 5 to 9 (a 9×9 board can't make a longer line).</summary>
        public const int ShortestLine = 5, LongestLine = 9;

        readonly Dictionary<(LinesSound, int), float[]> effects = new Dictionary<(LinesSound, int), float[]>();

        /// <summary>The music loop, <see cref="MusicRate"/> samples per second; it repeats without a seam.</summary>
        public float[] Music { get; private set; }

        /// <summary>The samples for <paramref name="sound"/> (<paramref name="variant"/>: the step note, or the line length).</summary>
        public float[] Effect(LinesSound sound, int variant = 0) => effects.TryGetValue((sound, variant), out var s) ? s : null;

        public IEnumerable<(LinesSound sound, int variant)> EffectKeys => effects.Keys;

        /// <summary>Makes everything (about a second of work, so the game does it in the background).</summary>
        public static SoundBank Make(int seed = 7, bool music = true)
        {
            var bank = new SoundBank();
            float effectReverb = Synth.ReverbScale(EffectRate);
            foreach (LinesSound sound in Enum.GetValues(typeof(LinesSound)))
            {
                if (sound == LinesSound.Step)
                    for (int k = 0; k < StepNotes; k++) bank.effects[(sound, k)] = Effect(sound, k, effectReverb);
                else if (sound == LinesSound.Clear)
                    for (int n = ShortestLine; n <= LongestLine; n++) bank.effects[(sound, n)] = Effect(sound, n, effectReverb);
                else bank.effects[(sound, 0)] = Effect(sound, 0, effectReverb);
            }
            if (music) bank.Music = FireflyDance(seed);
            return bank;
        }

        static float[] Effect(LinesSound sound, int variant, float reverbScale)
        {
            var s = new Synth(EffectRate, Length(sound, variant) + RingOut, EffectReverb, 1000 + (int)sound * 31 + variant);
            float g = EffectLevel;
            switch (sound)
            {
                case LinesSound.Tap: // Soft click
                    s.Noise(0, 0.025f, 0.2f * g, 0.001f, Synth.FilterType.BandPass, 2500, 3);
                    s.Tone(Synth.Wave.Sine, 900, 0, 0.03f, 0.07f * g, 0.001f);
                    break;
                case LinesSound.Select: // Fairy tick
                    s.Tone(Synth.Wave.Sine, 1568, 0, 0.12f, 0.22f * g, glide: new[] { (0f, 1568f), (0.06f, 2093f) });
                    s.Bell(3136, 0.02f, 0.25f, 0.04f * g);
                    break;
                case LinesSound.Step: // Sparkle run: one bell per cell
                    s.Bell(Synth.Midi(Pentatonic[3 + variant]), 0, 0.25f, 0.07f * g);
                    break;
                case LinesSound.Land: // Dew drop
                    s.Tone(Synth.Wave.Sine, 700, 0, 0.08f, 0.28f * g, glide: new[] { (0f, 700f), (0.07f, 1500f) });
                    s.Bell(1760, 0.06f, 0.4f, 0.05f * g);
                    break;
                case LinesSound.Clear: // Harp: one note per mushroom; a longer line starts higher
                    for (int i = 0; i < variant; i++) s.Pluck(Synth.Midi(Pentatonic[i + variant - ShortestLine]), i * 0.055f, 0.2f * g);
                    break;
                case LinesSound.Spawn: // Bubbles
                    for (int i = 0; i < 3; i++)
                        s.Tone(Synth.Wave.Sine, 260 + i * 60, i * 0.05f, 0.07f, 0.2f * g, 0.002f, glide: new[] { (0f, 260f + i * 60), (0.06f, 820f + i * 140) });
                    break;
                case LinesSound.Blocked: // Low buzz
                    foreach (float at in new[] { 0f, 0.14f }) s.Tone(Synth.Wave.Saw, 110, at, 0.1f, 0.16f * g, 0.01f, Synth.FilterType.LowPass, 600);
                    break;
                case LinesSound.GameOver: // Music box
                    int[] falling = { 86, 83, 81, 78, 76, 74 };
                    for (int i = 0; i < falling.Length; i++) s.Bell(Synth.Midi(falling[i]), i * 0.22f, i == falling.Length - 1 ? 1.6f : 0.6f, 0.12f * g);
                    break;
                case LinesSound.Record: // Warm swell
                    s.Pad(new[] { 62, 66, 69, 74 }, 0, 1.9f, 0.22f * g, 1800);
                    int[] top = { 86, 90, 93 };
                    for (int i = 0; i < top.Length; i++) s.Bell(Synth.Midi(top[i]), 0.5f + i * 0.1f, 1.2f, 0.06f * g);
                    break;
            }
            return s.Finish(reverbScale);
        }

        /// <summary>How long a sound's notes last, before the reverb rings out.</summary>
        static float Length(LinesSound sound, int variant)
        {
            switch (sound)
            {
                case LinesSound.Clear: return variant * 0.055f + 0.7f;
                case LinesSound.GameOver: return 5 * 0.22f + 1.6f;
                case LinesSound.Record: return 1.9f;
                default: return 0.5f;
            }
        }

        /// <summary>
        /// "Firefly dance": soft chords (D, Bm, G, A; about 6 s each), a low note at each chord change and a kalimba
        /// pattern on top, with a few extra notes at random. Played twice with different extras (46 s), then
        /// whatever rings past the end is folded onto the start, so the loop has no seam.
        /// </summary>
        public static float[] FireflyDance(int seed)
        {
            int loop = (int)Math.Round(MusicBeats * Beat * MusicRate);
            var s = new Synth(MusicRate, MusicBeats * Beat + 7f, MusicReverb, seed);
            float g = MusicLevel;
            int[] pattern = { 0, 2, 1, 3, 2, 1, 3, 2 };
            for (int i = 0; i < MusicBeats; i++)
            {
                float t = i * Beat;
                var chord = Chords[i / BeatsPerChord % Chords.Length];
                int k = i % BeatsPerChord;
                if (k == 0)
                {
                    s.Pad(chord, t, 5.8f, 0.12f * g, 650);
                    s.Pluck(Synth.Midi(chord[0] - 12), t, 0.16f * g, 1.4f);
                }
                if (k % 2 == 0 || s.NextRandom() < 0.3)
                    s.Pluck(Synth.Midi(chord[pattern[k % 8]] + 24), t, (k % 4 == 0 ? 0.09f : 0.06f) * g, 0.9f);
            }
            return s.Finish(Synth.ReverbScale(MusicRate), loop);
        }
    }
}
