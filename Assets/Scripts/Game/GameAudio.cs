using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockDrop.View
{
    /// <summary>Sound effects and a looping track, all synthesised at start-up. No audio files.</summary>
    public sealed class GameAudio
    {
        const int Rate = 22050;
        const string MutedKey = "BlockDrop.Muted";
        const float MusicVolume = 0.2f;
        // Leaves headroom: a hard drop, a clear and the music can all sound at once without clipping.
        const float EffectsVolume = 0.45f;

        readonly AudioSource[] voices = new AudioSource[10];
        readonly AudioSource music;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        int nextVoice;
        bool muted;
        bool persistMute;
        float musicLevel = 1f;

        public bool Muted { get { return muted; } }

        /// <param name="silent">Forces silence without touching the saved setting (for test runs).</param>
        public GameAudio(GameObject host, bool silent)
        {
            foreach (KeyValuePair<string, float[]> sound in BuildAllSounds())
            {
                AudioClip clip = AudioClip.Create(sound.Key, sound.Value.Length, 1, Rate, false);
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

            persistMute = !silent;
            muted = silent || PlayerPrefs.GetInt(MutedKey, 0) == 1;
            music.mute = muted;
            music.volume = MusicVolume;
            music.Play();
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
            float levelSpeed = 1f + Mathf.Min(level - 1, 10) * 0.012f;
            float wanted = levelSpeed + tension * 0.12f;
            music.pitch = Mathf.MoveTowards(music.pitch, wanted, deltaTime * 0.15f);
        }

        // --- synthesis: public so the rules tests can check every sound -----------------------

        public static Dictionary<string, float[]> BuildAllSounds()
        {
            var random = new System.Random(4242);
            return new Dictionary<string, float[]>
            {
                { "move", Tone(0.035f, 880f, 760f, 0f, 0.3f, random) },
                { "rotate", Tone(0.06f, 520f, 880f, 0f, 0.4f, random) },
                { "hold", Notes(new[] { 587f, 784f }, 0.06f, 0.22f, 0.5f) },
                { "lock", Tone(0.09f, 190f, 90f, 0.25f, 0.7f, random) },
                { "harddrop", Tone(0.17f, 150f, 45f, 0.5f, 1f, random) },
                { "clear1", Notes(new[] { 523f, 659f }, 0.055f, 0.3f, 0.65f) },
                { "clear2", Notes(new[] { 523f, 659f, 784f }, 0.05f, 0.34f, 0.7f) },
                { "clear3", Notes(new[] { 523f, 659f, 784f, 1047f }, 0.05f, 0.4f, 0.75f) },
                { "clear4", Notes(new[] { 523f, 659f, 784f, 1047f, 1319f, 1568f, 2093f }, 0.045f, 0.65f, 0.85f) },
                { "levelup", Notes(new[] { 392f, 494f, 587f, 784f, 988f, 1175f }, 0.075f, 0.6f, 0.75f) },
                { "gameover", Notes(new[] { 392f, 349f, 311f, 262f, 196f }, 0.16f, 0.7f, 0.75f) },
                { "bomb", Tone(0.55f, 110f, 28f, 0.72f, 1f, random) },
                { "zap", Tone(0.3f, 2600f, 240f, 0.65f, 0.9f, random) },
                { "music", BuildMusic() },
            };
        }

        static float[] Tone(float seconds, float from, float to, float noiseMix, float gain, System.Random random)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += 2.0 * Math.PI * Mathf.Lerp(from, to, t) / Rate;
                float envelope = (1f - t) * (1f - t) * Mathf.Clamp01(i / 30f);
                float hiss = (float)random.NextDouble() * 2f - 1f;
                data[i] = gain * envelope * Mathf.Lerp(Mathf.Sin((float)phase), hiss, noiseMix);
            }
            return data;
        }

        static float[] Notes(float[] notes, float spacing, float ring, float gain)
        {
            int count = (int)((spacing * notes.Length + ring) * Rate);
            var data = new float[count];
            for (int n = 0; n < notes.Length; n++)
                AddNote(data, (int)(n * spacing * Rate), ring, notes[n], 1f, false);
            Normalize(data, gain);
            return data;
        }

        // An original slow arpeggio over a minor progression. Notes wrap so the loop is seamless.
        static float[] BuildMusic()
        {
            const float Step = 0.3f;
            int[][] chords =
            {
                new[] { 57, 60, 64, 69 },
                new[] { 53, 57, 60, 65 },
                new[] { 48, 55, 60, 64 },
                new[] { 55, 59, 62, 67 },
            };
            int[] pattern = { 0, 2, 1, 3, 2, 1, 3, 2 };
            int bars = chords.Length * 2;
            int steps = bars * pattern.Length;
            var data = new float[(int)(steps * Step * Rate)];
            for (int s = 0; s < steps; s++)
            {
                int bar = s / pattern.Length;
                int[] chord = chords[bar % chords.Length];
                int start = (int)(s * Step * Rate);
                AddNote(data, start, 0.75f, Midi(chord[pattern[s % pattern.Length]] + 12), 0.42f, true);
                if (s % pattern.Length == 0)
                    AddNote(data, start, Step * pattern.Length, Midi(chord[0] - 12), 0.75f, true);
                else if (s % pattern.Length == 4)
                    AddNote(data, start, 1.2f, Midi(chord[2]), 0.3f, true);
            }
            Normalize(data, 0.9f);
            return data;
        }

        static float Midi(int note)
        {
            return 440f * Mathf.Pow(2f, (note - 69) / 12f);
        }

        static void AddNote(float[] data, int start, float seconds, float frequency, float gain, bool wrap)
        {
            int length = (int)(seconds * Rate);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length)
                {
                    if (!wrap)
                        return;
                    index -= data.Length;
                }
                float time = i / (float)Rate;
                float envelope = Mathf.Clamp01(i / 90f) * Mathf.Pow(1f - (float)i / length, 2f);
                data[index] += gain * envelope *
                    (Mathf.Sin(2f * Mathf.PI * frequency * time) + 0.22f * Mathf.Sin(4f * Mathf.PI * frequency * time));
            }
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }
    }

    public enum GameStateForAudio { Playing, Paused, Stopped }
}
