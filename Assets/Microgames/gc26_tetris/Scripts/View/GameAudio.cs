using System;
using System.Collections.Generic;
using UnityEngine;

namespace GC26.Tetris.View
{
    /// <summary>
    /// Sound effects and a two-layer looping track, all synthesised at start-up. No audio files.
    /// The second music layer is heavier drums, faded in with the level and with how full the well is.
    /// </summary>
    public sealed class GameAudio
    {
        const int Rate = 22050;
        const string MutedKey = "gc26_tetris_Muted";
        const float MusicVolume = 0.2f;
        // Leaves headroom: a hard drop, a clear and the music can all sound at once without clipping.
        const float EffectsVolume = 0.45f;

        readonly AudioSource[] voices = new AudioSource[10];
        readonly AudioSource music;
        readonly AudioSource drive;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        int nextVoice;
        bool muted;
        bool persistMute;
        float musicLevel = 1f;
        float driveLevel;

        public bool Muted { get { return muted; } }

        /// <param name="silent">Forces silence without touching the saved setting (for test runs).</param>
        public GameAudio(GameObject host, bool silent)
        {
            foreach (KeyValuePair<string, float[]> sound in BuildAllSounds())
            {
                AudioClip clip = RuntimeAssets.Keep(AudioClip.Create(sound.Key, sound.Value.Length, 1, Rate, false));
                clip.SetData(sound.Value, 0);
                clips[sound.Key] = clip;
            }

            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = host.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }
            music = host.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.clip = clips["music"];
            drive = host.AddComponent<AudioSource>();
            drive.playOnAwake = false;
            drive.loop = true;
            drive.clip = clips["musicdrive"];

            persistMute = !silent;
            muted = silent || PlayerPrefs.GetInt(MutedKey, 0) == 1;
            music.mute = muted;
            drive.mute = muted;
            music.volume = MusicVolume;
            drive.volume = 0f;
            // Same length, started together and always at the same pitch, so the two layers stay in step.
            music.Play();
            drive.Play();
            Debug.Log("[BlockDrop] Audio ready, clips: " + clips.Count + ", muted: " + muted);
        }

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (muted)
                return;
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clips[name], volume * EffectsVolume);
        }

        public void ToggleMute()
        {
            muted = !muted;
            music.mute = muted;
            drive.mute = muted;
            if (persistMute)
            {
                PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <param name="tension">0 to 1: how close the stack is to the top. Speeds the music up.</param>
        public void UpdateMusic(GameStateForAudio state, int level, float tension, float deltaTime)
        {
            float target = state == GameStateForAudio.Playing ? 1f : state == GameStateForAudio.Paused ? 0.3f : 0f;
            musicLevel = Mathf.MoveTowards(musicLevel, target, deltaTime * 1.5f);
            music.volume = MusicVolume * musicLevel;
            float wantedDrive = Mathf.Clamp01(0.45f + Mathf.Min(level - 1, 8) * 0.07f + tension * 0.5f);
            driveLevel = Mathf.MoveTowards(driveLevel, wantedDrive, deltaTime * 0.5f);
            drive.volume = MusicVolume * musicLevel * driveLevel;
            float levelSpeed = 1f + Mathf.Min(level - 1, 10) * 0.015f;
            float wanted = levelSpeed + tension * 0.12f;
            music.pitch = Mathf.MoveTowards(music.pitch, wanted, deltaTime * 0.15f);
            drive.pitch = music.pitch;
        }

        // --- synthesis: public so the rules tests can check every sound -----------------------

        enum Wave { Sine, Square, Saw, SuperSaw }

        public static Dictionary<string, float[]> BuildAllSounds()
        {
            var random = new System.Random(4242);
            return new Dictionary<string, float[]>
            {
                { "move", BuildMove(random) },
                { "rotate", BuildRotate(random) },
                { "hold", BuildHold(random) },
                { "lock", BuildLock(random) },
                { "harddrop", BuildHardDrop(random) },
                { "clear1", BuildClear(new[] { 523f, 784f }, null, 0.65f, random) },
                { "clear2", BuildClear(new[] { 523f, 659f, 1047f }, null, 0.72f, random) },
                { "clear3", BuildClear(new[] { 523f, 659f, 784f, 1047f, 1319f }, null, 0.8f, random) },
                { "clear4", BuildClear(new[] { 523f, 659f, 784f, 1047f, 1319f, 1568f, 2093f }, new[] { 1047f, 1319f, 1568f }, 0.9f, random) },
                { "levelup", BuildLevelUp() },
                { "gameover", BuildGameOver(random) },
                { "bomb", BuildBomb(random) },
                { "zap", BuildZap(random) },
                { "bothit", BuildBotHit(random) },
                { "botdown", BuildBotDown(random) },
                { "coin", Done(Echo(Arp(new[] { 1319f, 1976f }, 0.045f, 0.12f, Wave.Square, 0.05f), 0.11f, 0.35f, 2), 0.42f) },
                { "music", BuildMusic(false, random) },
                { "musicdrive", BuildMusic(true, random) },
            };
        }

        // --- effects --------------------------------------------------------------------------

        // A tiny digital tick: a high blip and a breath of bright noise.
        static float[] BuildMove(System.Random random)
        {
            float[] data = Sweep(0.03f, 2100f, 1700f, Wave.Sine, 0.008f, 0f, 0f, random);
            Mix(data, HighPass(Sweep(0.02f, 1f, 1f, Wave.Sine, 0.005f, 1f, 0f, random)), 0, 0.35f);
            return Done(data, 0.28f);
        }

        // A filtered saw "zip" up, crushed a little for a digital edge.
        static float[] BuildRotate(System.Random random)
        {
            float[] data = Sweep(0.07f, 380f, 1900f, Wave.Saw, 0.035f, 0f, 0.3f, random);
            LowPass(data, 4500f);
            return Done(Crush(data, 2, 24), 0.38f);
        }

        // A short noise whoosh rising into a two-note bleep.
        static float[] BuildHold(System.Random random)
        {
            var data = new float[(int)(0.2f * Rate)];
            float smooth = 0f;
            int whoosh = (int)(0.08f * Rate);
            for (int i = 0; i < whoosh; i++)
            {
                float share = (float)i / whoosh;
                smooth += Mathf.Lerp(0.05f, 0.7f, share) * (((float)random.NextDouble() * 2f - 1f) - smooth);
                data[i] += smooth * share * 0.5f;
            }
            AddTone(data, whoosh - (int)(0.01f * Rate), 0.11f, 880f, 0.55f, Wave.Square, 0.04f, false, 0f, 3500f);
            AddTone(data, whoosh + (int)(0.03f * Rate), 0.09f, 1320f, 0.45f, Wave.Square, 0.035f, false, 0f, 3500f);
            return Done(data, 0.45f);
        }

        // A tight 808-style thump.
        static float[] BuildLock(System.Random random)
        {
            float[] data = Sweep(0.12f, 260f, 62f, Wave.Sine, 0.045f, 0f, 0.9f, random);
            Mix(data, HighPass(Sweep(0.015f, 1f, 1f, Wave.Sine, 0.004f, 1f, 0f, random)), 0, 0.4f);
            return Done(data, 0.6f);
        }

        // A heavy 808 hit with a long sub, a noise impact and a clap on top.
        static float[] BuildHardDrop(System.Random random)
        {
            float[] data = Sweep(0.42f, 210f, 40f, Wave.Sine, 0.14f, 0f, 1.3f, random);
            Mix(data, Sweep(0.2f, 1f, 1f, Wave.Sine, 0.05f, 1f, 0f, random, 0.3f), 0, 0.6f);
            var clap = new float[(int)(0.15f * Rate)];
            Clap(clap, 0, 0.5f, random);
            Mix(data, clap, 0, 1f);
            return Done(data, 0.95f);
        }

        // A kick for punch, a fast saw-and-square run, and for big clears a shimmer and a held chord.
        static float[] BuildClear(float[] run, float[] chord, float gain, System.Random random)
        {
            const float Spacing = 0.035f;
            float tail = chord != null ? 0.55f : 0.22f;
            var data = new float[(int)((Spacing * run.Length + tail + 0.05f) * Rate)];
            Mix(data, Sweep(0.15f, 150f, 50f, Wave.Sine, 0.05f, 0f, 0.6f, random), 0, 0.7f);
            for (int n = 0; n < run.Length; n++)
            {
                int start = (int)(n * Spacing * Rate);
                AddTone(data, start, 0.2f, run[n], 0.45f, Wave.Saw, 0.07f, false);
                AddTone(data, start, 0.2f, run[n] * 2f, 0.15f, Wave.Square, 0.04f, false);
            }
            if (run.Length >= 5)
                Mix(data, Sweep(0.45f, 1f, 1f, Wave.Sine, 0.16f, 1f, 0f, random, 0.9f), 0, 0.12f);
            if (chord != null)
            {
                int start = (int)(run.Length * Spacing * Rate);
                foreach (float note in chord)
                    AddTone(data, start, tail, note, 0.3f, Wave.Square, 0.22f, false, 0.012f);
            }
            return Done(data, gain);
        }

        static float[] BuildLevelUp()
        {
            float[] run = { 523f, 659f, 784f, 1047f, 1319f, 1568f };
            const float Spacing = 0.045f;
            var data = new float[(int)((Spacing * run.Length + 0.6f) * Rate)];
            for (int n = 0; n < run.Length; n++)
                AddTone(data, (int)(n * Spacing * Rate), 0.16f, run[n], 0.5f, Wave.Square, 0.05f, false);
            int end = (int)(run.Length * Spacing * Rate);
            foreach (float note in new[] { 1047f, 1319f, 1568f })
                AddTone(data, end, 0.55f, note, 0.32f, Wave.Saw, 0.2f, false, 0.015f);
            return Done(data, 0.75f);
        }

        static float[] BuildGameOver(System.Random random)
        {
            float[] notes = { 392f, 349f, 311f, 262f, 196f };
            const float Spacing = 0.17f;
            var data = new float[(int)((Spacing * notes.Length + 0.9f) * Rate)];
            for (int n = 0; n < notes.Length; n++)
            {
                bool last = n == notes.Length - 1;
                AddTone(data, (int)(n * Spacing * Rate), last ? 0.9f : 0.22f, notes[n], 0.5f, Wave.Saw, last ? 0.35f : 0.09f, false, last ? 0.02f : 0f);
            }
            Mix(data, Sweep(0.6f, 120f, 30f, Wave.Sine, 0.2f, 0f, 0.8f, random), (int)((notes.Length - 1) * Spacing * Rate), 0.7f);
            return Done(data, 0.75f);
        }

        // A drop impact: distorted sub boom, a falling noise sweep and a crash.
        static float[] BuildBomb(System.Random random)
        {
            float[] data = Sweep(1f, 95f, 26f, Wave.Sine, 0.32f, 0f, 1.6f, random);
            int length = data.Length;
            float smooth = 0f;
            for (int i = 0; i < length; i++)
            {
                float share = (float)i / length;
                smooth += Mathf.Lerp(0.8f, 0.02f, share) * (((float)random.NextDouble() * 2f - 1f) - smooth);
                data[i] += smooth * (1f - share) * (1f - share) * 1.1f;
            }
            Mix(data, HighPass(Sweep(0.6f, 1f, 1f, Wave.Sine, 0.18f, 1f, 0f, random)), 0, 0.5f);
            return Done(data, 1f);
        }

        // A laser shot: a fast falling saw, bit-crushed, with a bright crackle.
        static float[] BuildZap(System.Random random)
        {
            float[] data = Sweep(0.3f, 2600f, 110f, Wave.Saw, 0.09f, 0f, 0.6f, random, 1f, 0.25f);
            Mix(data, Sweep(0.3f, 5200f, 220f, Wave.Square, 0.05f, 0f, 0f, random), 0, 0.25f);
            Mix(data, HighPass(Sweep(0.18f, 1f, 1f, Wave.Sine, 0.04f, 1f, 0f, random)), 0, 0.4f);
            return Done(Crush(data, 3, 16), 0.9f);
        }

        // A punchy kick with a clap and a short wobble of bass.
        static float[] BuildBotHit(System.Random random)
        {
            float[] data = Sweep(0.16f, 240f, 60f, Wave.Sine, 0.05f, 0f, 1f, random);
            var clap = new float[(int)(0.12f * Rate)];
            Clap(clap, 0, 0.45f, random);
            Mix(data, clap, 0, 1f);
            Mix(data, Sweep(0.12f, 110f, 80f, Wave.Saw, 0.05f, 0f, 0.5f, random), 0, 0.3f);
            return Done(data, 0.8f);
        }

        // A supersaw diving down an octave and more, then a sub impact.
        static float[] BuildBotDown(System.Random random)
        {
            float[] data = Sweep(0.8f, 880f, 70f, Wave.SuperSaw, 0.28f, 0f, 0.6f, random);
            LowPass(data, 5000f);
            Mix(data, Sweep(0.75f, 90f, 28f, Wave.Sine, 0.22f, 0f, 1.4f, random), (int)(0.1f * Rate), 1.1f);
            Mix(data, Sweep(0.5f, 1f, 1f, Wave.Sine, 0.12f, 1f, 0f, random, 0.15f), (int)(0.1f * Rate), 0.7f);
            return Done(data, 1f);
        }

        // --- music ----------------------------------------------------------------------------

        const float Bpm = 126f;
        const float Sixteenth = 60f / Bpm / 4f;

        /// <summary>
        /// An original electronic loop in the EDM style: rolling filtered bass, a sub, off-beat supersaw stabs
        /// and a plucked arpeggio, all pumping against the beat, with light drums.
        /// The "drive" layer is the same length and holds the heavy drums only; it is faded in as the game heats up.
        /// </summary>
        static float[] BuildMusic(bool drive, System.Random random)
        {
            int[] roots = { 45, 41, 48, 43 };                    // A, F, C, G
            int[][] chords =
            {
                new[] { 69, 72, 76 },
                new[] { 65, 69, 72 },
                new[] { 67, 72, 76 },
                new[] { 67, 71, 74 },
            };
            int[] arp = { 0, 1, 2, 3, 2, 1, 0, 1, 0, 1, 2, 3, 2, 1, 2, 3 };
            const int Bars = 8, Steps = 16;
            var data = new float[(int)(Bars * Steps * Sixteenth * Rate)];

            // Synths first, so the pump below shapes them and not the drums.
            if (!drive)
            {
                for (int bar = 0; bar < Bars; bar++)
                {
                    int chordIndex = bar % roots.Length;
                    int[] chord = chords[chordIndex];
                    bool secondHalf = bar >= Bars / 2;
                    for (int step = 0; step < Steps; step++)
                    {
                        int start = (int)((bar * Steps + step) * Sixteenth * Rate);
                        int root = roots[chordIndex];
                        // Rolling bass: every sixteenth except the one the kick takes.
                        if (step % 4 != 0)
                            AddTone(data, start, 0.1f, Midi(root), 0.42f, Wave.Saw, 0.07f, true, 0f, 750f);
                        if (step % 4 == 0)
                            AddTone(data, start, 0.42f, Midi(root - 12), 0.4f, Wave.Sine, 0.3f, true);
                        // Off-beat stabs, the classic lift of the style.
                        if (step % 4 == 2)
                        {
                            foreach (int note in chord)
                                AddTone(data, start, 0.15f, Midi(note - 12), 0.2f, Wave.SuperSaw, 0.08f, true, 0f, 2600f);
                        }
                        // The pluck arpeggio comes in at half speed, then plays every sixteenth the second time round.
                        if (secondHalf || step % 2 == 0)
                        {
                            int tone = arp[step];
                            int note = tone == 3 ? chord[0] + 12 : chord[tone];
                            AddTone(data, start, 0.14f, Midi(note), secondHalf ? 0.16f : 0.12f, Wave.Saw, 0.06f, true, 0f, 3200f);
                        }
                    }
                }
                Pump(data, (int)(4 * Sixteenth * Rate), 0.6f, 0.11f);
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                bool turnaround = bar % roots.Length == roots.Length - 1;
                for (int step = 0; step < Steps; step++)
                {
                    int start = (int)((bar * Steps + step) * Sixteenth * Rate);
                    if (!drive)
                    {
                        if (step == 0 || step == 8)
                            Kick(data, start, 0.65f, random);
                        if (step % 4 == 2)
                            Hat(data, start, 0.14f, 0.012f, random);
                        continue;
                    }
                    if (step % 4 == 0)
                        Kick(data, start, 1f, random);
                    if (step == 4 || step == 12)
                        Clap(data, start, 0.6f, random);
                    Hat(data, start, step % 4 == 2 ? 0.26f : 0.09f, step % 4 == 2 ? 0.05f : 0.01f, random);
                    // A clap roll and a noise rise lead into each new round of the chords; a crash marks its start.
                    if (turnaround && step >= 8)
                        Clap(data, start, 0.15f + (step - 8) * 0.04f, random);
                    if (turnaround && step == 0)
                        Riser(data, start, Steps * Sixteenth, 0.32f, random);
                    if (bar % roots.Length == 0 && step == 0)
                        Crash(data, start, 0.22f, random);
                }
            }
            return Done(data, 0.9f);
        }

        static void Kick(float[] data, int start, float gain, System.Random random)
        {
            int length = (int)(0.25f * Rate);
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                phase += 2.0 * Math.PI * (45f + 115f * Mathf.Exp(-t / 0.03f)) / Rate;
                float body = (float)Math.Tanh(Math.Sin(phase) * 2.0) * Mathf.Exp(-t / 0.11f) * (1f - (float)i / length);
                float click = i < 90 ? ((float)random.NextDouble() * 2f - 1f) * (1f - i / 90f) * 0.4f : 0f;
                Add(data, start + i, gain * (body + click));
            }
        }

        static void Snare(float[] data, int start, float gain, System.Random random)
        {
            int length = (int)(0.18f * Rate);
            float smooth = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                smooth += 0.6f * (((float)random.NextDouble() * 2f - 1f) - smooth);
                float tone = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t / 0.04f) * 0.5f;
                float fade = 1f - (float)i / length;
                Add(data, start + i, gain * fade * (smooth * Mathf.Exp(-t / 0.06f) + tone));
            }
        }

        static void Hat(float[] data, int start, float gain, float decay, System.Random random)
        {
            int length = (int)((decay * 4f + 0.01f) * Rate);
            float previous = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                float bright = noise - previous;
                previous = noise;
                float t = i / (float)Rate;
                Add(data, start + i, gain * 0.6f * bright * Mathf.Exp(-t / decay) * (1f - (float)i / length));
            }
        }

        // Three quick bursts of noise and a tail, like hands clapping together.
        static void Clap(float[] data, int start, float gain, System.Random random)
        {
            int length = (int)(0.16f * Rate), gap = (int)(0.011f * Rate);
            float low = 0f, previous = 0f;
            for (int i = 0; i < length; i++)
            {
                low += 0.45f * (((float)random.NextDouble() * 2f - 1f) - low);
                float band = low - previous;
                previous = low;
                float t = i / (float)Rate;
                float burst = i < gap * 3 ? Mathf.Exp(-(i % gap) / (float)Rate / 0.004f) : Mathf.Exp(-(t - gap * 3f / Rate) / 0.07f);
                Add(data, start + i, gain * 2.2f * band * burst * (1f - (float)i / length));
            }
        }

        static void Crash(float[] data, int start, float gain, System.Random random)
        {
            int length = (int)(1.2f * Rate);
            float previous = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                float bright = noise - previous * 0.5f;
                previous = noise;
                float t = i / (float)Rate;
                Add(data, start + i, gain * bright * Mathf.Exp(-t / 0.35f) * (1f - (float)i / length));
            }
        }

        // Sidechain-style pumping: the level dips on every beat and swells back up.
        static void Pump(float[] data, int beat, float depth, float release)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float since = (i % beat) / (float)Rate;
                data[i] *= 1f - depth * Mathf.Exp(-since / release);
            }
        }

        static void Riser(float[] data, int start, float seconds, float gain, System.Random random)
        {
            int length = (int)(seconds * Rate);
            float smooth = 0f;
            for (int i = 0; i < length; i++)
            {
                float share = (float)i / length;
                smooth += Mathf.Lerp(0.03f, 0.8f, share) * (((float)random.NextDouble() * 2f - 1f) - smooth);
                Add(data, start + i, gain * smooth * share * share);
            }
        }

        // --- building blocks ------------------------------------------------------------------

        static float Midi(int note)
        {
            return 440f * Mathf.Pow(2f, (note - 69) / 12f);
        }

        /// <summary>One sample of a wave. Harmonics stop below the Nyquist limit, so high notes stay clean.</summary>
        static float Shape(Wave wave, double phase, float frequency)
        {
            if (wave == Wave.Sine)
                return (float)Math.Sin(phase);
            if (wave == Wave.SuperSaw)
                return (Shape(Wave.Saw, phase * 0.988, frequency) + Shape(Wave.Saw, phase, frequency) + Shape(Wave.Saw, phase * 1.012, frequency)) * 0.45f;
            int limit = Mathf.Max(1, (int)(Rate * 0.45f / Mathf.Max(1f, frequency)));
            double sum = 0;
            if (wave == Wave.Square)
            {
                for (int k = 1; k <= Mathf.Min(limit, 9); k += 2)
                    sum += Math.Sin(k * phase) / k;
                return (float)(sum * 1.1);
            }
            for (int k = 1; k <= Mathf.Min(limit, 8); k++)
                sum += Math.Sin(k * phase) / k;
            return (float)(sum * 0.6);
        }

        /// <summary>
        /// A pitch glide (exponential, from → to) or filtered noise (noise 1), with a sharp attack and a fast,
        /// exponential fall. drive saturates for punch; jitter makes the pitch crackle; lowpass 1 keeps noise bright.
        /// </summary>
        static float[] Sweep(float seconds, float from, float to, Wave wave, float decay, float noise, float drive,
            System.Random random, float lowpass = 1f, float jitter = 0f)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            double phase = 0;
            float smooth = 0f;
            float driveGain = 1f + drive * 4f, driveNorm = (float)Math.Tanh(driveGain);
            for (int i = 0; i < count; i++)
            {
                float share = (float)i / count, t = i / (float)Rate;
                float frequency = from * Mathf.Pow(to / from, share);
                if (jitter > 0f)
                    frequency *= 1f + jitter * ((float)random.NextDouble() * 2f - 1f);
                phase += 2.0 * Math.PI * frequency / Rate;
                float sample = noise < 1f ? Shape(wave, phase, frequency) : 0f;
                if (noise > 0f)
                {
                    smooth += lowpass * (((float)random.NextDouble() * 2f - 1f) - smooth);
                    sample = Mathf.Lerp(sample, smooth, noise);
                }
                if (drive > 0f)
                    sample = (float)Math.Tanh(sample * driveGain) / driveNorm;
                float envelope = Mathf.Clamp01(i / (0.002f * Rate)) * Mathf.Exp(-t / decay) * (1f - share);
                data[i] = sample * envelope;
            }
            return data;
        }

        static float[] Arp(float[] notes, float spacing, float ring, Wave wave, float decay)
        {
            var data = new float[(int)((spacing * notes.Length + ring) * Rate)];
            for (int n = 0; n < notes.Length; n++)
                AddTone(data, (int)(n * spacing * Rate), ring, notes[n], 1f, wave, decay, false);
            return data;
        }

        /// <param name="cutoff">A low-pass filter in hertz that closes as the note fades, for a pluck; 0 leaves the tone bright.</param>
        static void AddTone(float[] data, int start, float seconds, float frequency, float gain, Wave wave, float decay, bool wrap,
            float vibrato = 0f, float cutoff = 0f)
        {
            int length = (int)(seconds * Rate);
            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length)
                {
                    if (!wrap)
                        return;
                    index -= data.Length;
                }
                float t = i / (float)Rate;
                double phase = 2.0 * Math.PI * frequency * t;
                if (vibrato > 0f)
                    phase += vibrato * frequency / 6f * Math.Sin(2.0 * Math.PI * 6.0 * t);
                float envelope = Mathf.Clamp01(i / (0.003f * Rate)) * Mathf.Exp(-t / decay) * (1f - (float)i / length);
                float sample = Shape(wave, phase, frequency);
                if (cutoff > 0f)
                {
                    float open = cutoff * (0.25f + 0.75f * Mathf.Exp(-t / decay));
                    filtered += (1f - Mathf.Exp(-2f * Mathf.PI * open / Rate)) * (sample - filtered);
                    sample = filtered;
                }
                data[index] += gain * envelope * sample;
            }
        }

        // Adds one sample; past the end it wraps, so loops stay seamless.
        static void Add(float[] data, int index, float value)
        {
            data[index % data.Length] += value;
        }

        static void Mix(float[] target, float[] source, int offset, float gain)
        {
            for (int i = 0; i < source.Length && offset + i < target.Length; i++)
                target[offset + i] += source[i] * gain;
        }

        static void LowPass(float[] data, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), state = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                state += a * (data[i] - state);
                data[i] = state;
            }
        }

        /// <summary>Bit-crusher: holds each sample for a few steps and rounds it to a few levels, for a digital grit.</summary>
        static float[] Crush(float[] data, int hold, int levels)
        {
            float max = 0.0001f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            float held = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                if (i % hold == 0)
                    held = Mathf.Round(data[i] / max * levels) / levels * max;
                data[i] = held;
            }
            return data;
        }

        /// <summary>Repeats the sound a few times, each quieter, like a delay pedal.</summary>
        static float[] Echo(float[] data, float delay, float feedback, int repeats)
        {
            int step = (int)(delay * Rate);
            var result = new float[data.Length + step * repeats];
            float gain = 1f;
            for (int r = 0; r <= repeats; r++, gain *= feedback)
                Mix(result, data, r * step, gain);
            return result;
        }

        static float[] HighPass(float[] data)
        {
            float previous = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float current = data[i];
                data[i] = current - previous;
                previous = current;
            }
            return data;
        }

        static float[] Done(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
            return data;
        }
    }

    public enum GameStateForAudio { Playing, Paused, Stopped }
}
