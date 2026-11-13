using System;
using System.Collections.Generic;

namespace GC26.Lines.Audio
{
    /// <summary>
    /// A tiny offline synthesizer: it writes notes into a buffer of samples, then adds reverb and evens out the
    /// loudness. It copies the prototype's Web Audio recipes (tone, bell, pluck, noise, pad), so Unity sounds the
    /// same without any sound files. Plain C# (no UnityEngine), so it can run on a background thread.
    /// </summary>
    public sealed class Synth
    {
        public enum Wave { Sine, Triangle, Saw }
        public enum FilterType { None, LowPass, BandPass, HighPass }

        public readonly int Rate;
        readonly float[] dry, wet;
        readonly float send; // how much of every note goes to the reverb
        readonly Random random;

        public int Length => dry.Length;

        public Synth(int rate, float seconds, float reverbSend, int seed)
        {
            Rate = rate;
            dry = new float[Math.Max(1, (int)Math.Ceiling(seconds * rate))];
            wet = new float[dry.Length];
            send = reverbSend;
            random = new Random(seed);
        }

        /// <summary>A note number (MIDI: 69 = A4 = 440 Hz) in hertz.</summary>
        public static float Midi(float note) => 440f * (float)Math.Pow(2.0, (note - 69.0) / 12.0);

        public double NextRandom() => random.NextDouble();

        // ---- Voices ----

        /// <summary>
        /// One oscillator note: rises to <paramref name="gain"/> in <paramref name="attack"/> seconds, then fades
        /// exponentially until <paramref name="seconds"/>. <paramref name="glide"/> are (time, Hz) points the pitch
        /// slides through exponentially, like Web Audio's exponentialRampToValueAtTime.
        /// </summary>
        public void Tone(Wave wave, float hz, float start, float seconds, float gain, float attack = 0.005f,
            FilterType filter = FilterType.None, float filterHz = 0f, float q = 0.7f, (float time, float hz)[] glide = null)
        {
            int from = (int)(start * Rate), count = (int)(seconds * Rate);
            var biquad = filter == FilterType.None ? null : new Biquad(filter, filterHz, q, Rate);
            double phase = 0;
            for (int n = 0; n < count; n++)
            {
                int i = from + n;
                if (i < 0) continue;
                if (i >= dry.Length) break;
                float t = n / (float)Rate;
                float f = glide == null ? hz : Glide(glide, t);
                phase += f / Rate;
                phase -= Math.Floor(phase);
                float x = Oscillator(wave, phase);
                if (biquad != null) x = biquad.Process(x);
                Add(i, x * Envelope(t, seconds, gain, attack));
            }
        }

        /// <summary>A glassy bell: a few slightly out-of-tune overtones that fade at different speeds.</summary>
        public void Bell(float hz, float start, float seconds, float gain)
        {
            Tone(Wave.Sine, hz, start, seconds, gain, 0.002f);
            Tone(Wave.Sine, hz * 2.01f, start, seconds * 0.7f, gain * 0.45f, 0.002f);
            Tone(Wave.Sine, hz * 3.0f, start, seconds * 0.5f, gain * 0.25f, 0.002f);
            Tone(Wave.Sine, hz * 4.17f, start, seconds * 0.35f, gain * 0.15f, 0.002f);
            Tone(Wave.Sine, hz * 5.43f, start, seconds * 0.25f, gain * 0.08f, 0.002f);
        }

        /// <summary>A plucked string (kalimba, harp): a sine, a short bright triangle and a tiny click.</summary>
        public void Pluck(float hz, float start, float gain, float seconds = 0.7f)
        {
            Tone(Wave.Sine, hz, start, seconds, gain, 0.002f);
            Tone(Wave.Triangle, hz * 2f, start, seconds * 0.25f, gain * 0.35f, 0.002f);
            Noise(start, 0.015f, gain * 0.3f, 0.005f, FilterType.BandPass, hz * 3f, 2f);
        }

        /// <summary>Filtered white noise with the same rise and fade as <see cref="Tone"/>.</summary>
        public void Noise(float start, float seconds, float gain, float attack, FilterType filter, float filterHz, float q = 0.7f)
        {
            int from = (int)(start * Rate), count = (int)(seconds * Rate);
            var biquad = new Biquad(filter, filterHz, q, Rate);
            for (int n = 0; n < count; n++)
            {
                int i = from + n;
                if (i < 0) continue;
                if (i >= dry.Length) break;
                float x = biquad.Process((float)(random.NextDouble() * 2.0 - 1.0));
                Add(i, x * Envelope(n / (float)Rate, seconds, gain, attack));
            }
        }

        /// <summary>A soft chord: two slightly detuned saws per note through a low-pass filter, slowly in and out.</summary>
        public void Pad(IReadOnlyList<int> notes, float start, float seconds, float gain, float cutoff)
        {
            float each = gain / notes.Count, rise = Math.Min(1.5f, seconds * 0.35f);
            foreach (int note in notes)
            foreach (float cents in new[] { -7f, 7f })
            {
                float hz = Midi(note) * (float)Math.Pow(2.0, cents / 1200.0);
                var biquad = new Biquad(FilterType.LowPass, cutoff, 0.7f, Rate);
                int from = (int)(start * Rate), count = (int)(seconds * Rate);
                double phase = 0;
                for (int n = 0; n < count; n++)
                {
                    int i = from + n;
                    if (i < 0) continue;
                    if (i >= dry.Length) break;
                    float t = n / (float)Rate;
                    phase += hz / Rate;
                    phase -= Math.Floor(phase);
                    float level = t < rise ? each * t / rise : t > seconds - rise ? each * (seconds - t) / rise : each;
                    Add(i, biquad.Process(Oscillator(Wave.Saw, phase)) * level);
                }
            }
        }

        // ---- Finishing ----

        /// <summary>
        /// Mixes the notes with their reverb, applies the master level and a gentle compressor (as the prototype's
        /// Web Audio chain does), and returns the samples. A loop (<paramref name="loopLength"/> > 0) folds
        /// everything that rings past its end back onto its start, so it repeats without a seam.
        /// </summary>
        public float[] Finish(float reverbScale, int loopLength = 0, bool trim = true)
        {
            var reverb = new Reverb(Rate);
            var mixed = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++)
                mixed[i] = (dry[i] + reverb.Process(wet[i]) * reverbScale * ReverbLevel) * MasterLevel;

            if (loopLength > 0 && loopLength < mixed.Length)
            {
                var loop = new float[loopLength];
                for (int i = 0; i < mixed.Length; i++) loop[i % loopLength] += mixed[i];
                mixed = loop;
            }
            Compress(mixed);
            return trim && loopLength <= 0 ? TrimSilence(mixed) : mixed;
        }

        /// <summary>
        /// The reverb's loudness, measured once: its echo of a single click carries as much energy as the click
        /// itself (like Web Audio's normalised reverb). Multiply <see cref="Finish"/>'s reverb by it.
        /// </summary>
        public static float ReverbScale(int rate)
        {
            var reverb = new Reverb(rate);
            double energy = 0;
            for (int i = 0; i < rate * 4; i++)
            {
                float y = reverb.Process(i == 0 ? 1f : 0f);
                energy += y * y;
            }
            return energy > 0 ? (float)(1.0 / Math.Sqrt(energy)) : 0f;
        }

        public const float MasterLevel = 0.9f, ReverbLevel = 0.32f;

        // ---- Helpers ----

        void Add(int i, float x)
        {
            dry[i] += x;
            wet[i] += x * send;
        }

        static float Oscillator(Wave wave, double phase)
        {
            switch (wave)
            {
                case Wave.Triangle: return (float)(1.0 - 4.0 * Math.Abs(phase - 0.5));
                case Wave.Saw: return (float)(2.0 * phase - 1.0);
                default: return (float)Math.Sin(2.0 * Math.PI * phase);
            }
        }

        // Web Audio: linear from almost nothing to the gain, then exponential down to almost nothing.
        static float Envelope(float t, float seconds, float gain, float attack)
        {
            const float floor = 0.0001f;
            if (t < attack) return floor + (gain - floor) * t / attack;
            float p = (t - attack) / Math.Max(1e-4f, seconds - attack);
            return gain * (float)Math.Pow(floor / gain, Math.Min(1f, p));
        }

        static float Glide((float time, float hz)[] points, float t)
        {
            if (t <= points[0].time) return points[0].hz;
            for (int k = 1; k < points.Length; k++)
            {
                if (t > points[k].time) continue;
                var (t0, f0) = points[k - 1];
                var (t1, f1) = points[k];
                float p = (t - t0) / Math.Max(1e-5f, t1 - t0);
                return f0 * (float)Math.Pow(f1 / f0, p);
            }
            return points[points.Length - 1].hz;
        }

        // Evens out loud and quiet moments: above -14 dB the level grows 4 times slower (like the prototype's
        // compressor), then it is lifted back up a little; nothing goes past full scale.
        void Compress(float[] samples)
        {
            const float threshold = 0.2f, ratio = 4f, makeUp = 1.6f;
            float level = 0f;
            float rise = 1f - (float)Math.Exp(-1.0 / (0.003 * Rate)), fall = 1f - (float)Math.Exp(-1.0 / (0.25 * Rate));
            for (int i = 0; i < samples.Length; i++)
            {
                float x = Math.Abs(samples[i]);
                level += (x > level ? rise : fall) * (x - level);
                float gain = level > threshold ? (threshold * (float)Math.Pow(level / threshold, 1f / ratio)) / level : 1f;
                float y = samples[i] * gain * makeUp;
                samples[i] = Math.Abs(y) < 0.9f ? y : Math.Sign(y) * (0.9f + 0.1f * (float)Math.Tanh((Math.Abs(y) - 0.9f) / 0.1f));
            }
        }

        static float[] TrimSilence(float[] samples)
        {
            int end = samples.Length;
            while (end > 1 && Math.Abs(samples[end - 1]) < 1e-4f) end--;
            if (end == samples.Length) return samples;
            var trimmed = new float[end];
            Array.Copy(samples, trimmed, end);
            return trimmed;
        }

        /// <summary>A standard two-pole filter (the "Audio EQ Cookbook" recipes, as Web Audio uses).</summary>
        sealed class Biquad
        {
            readonly float b0, b1, b2, a1, a2;
            float x1, x2, y1, y2;

            public Biquad(FilterType type, float hz, float q, int rate)
            {
                hz = Math.Max(10f, Math.Min(hz, rate * 0.45f));
                double w = 2.0 * Math.PI * hz / rate, cos = Math.Cos(w), sin = Math.Sin(w);
                // Web Audio gives low-pass and high-pass resonance in decibels.
                double qLinear = type == FilterType.BandPass ? Math.Max(0.01, q) : Math.Pow(10.0, q / 20.0);
                double alpha = sin / (2.0 * qLinear), a0 = 1.0 + alpha;
                double n0, n1, n2;
                switch (type)
                {
                    case FilterType.HighPass: n0 = (1 + cos) / 2; n1 = -(1 + cos); n2 = (1 + cos) / 2; break;
                    case FilterType.BandPass: n0 = alpha; n1 = 0; n2 = -alpha; break;
                    default: n0 = (1 - cos) / 2; n1 = 1 - cos; n2 = (1 - cos) / 2; break;
                }
                b0 = (float)(n0 / a0); b1 = (float)(n1 / a0); b2 = (float)(n2 / a0);
                a1 = (float)(-2.0 * cos / a0); a2 = (float)((1.0 - alpha) / a0);
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>A small room reverb (the classic "Freeverb": eight echoing delays, then four smearing ones), about 3 s long.</summary>
        sealed class Reverb
        {
            static readonly int[] CombDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            static readonly int[] AllPassDelays = { 556, 441, 341, 225 };
            const float Feedback = 0.86f, Damping = 0.3f;

            readonly float[][] combs, allPasses;
            readonly int[] combAt, allPassAt;
            readonly float[] combLow;

            public Reverb(int rate)
            {
                float scale = rate / 44100f;
                combs = new float[CombDelays.Length][];
                for (int k = 0; k < combs.Length; k++) combs[k] = new float[Math.Max(1, (int)(CombDelays[k] * scale))];
                allPasses = new float[AllPassDelays.Length][];
                for (int k = 0; k < allPasses.Length; k++) allPasses[k] = new float[Math.Max(1, (int)(AllPassDelays[k] * scale))];
                combAt = new int[combs.Length];
                allPassAt = new int[allPasses.Length];
                combLow = new float[combs.Length];
            }

            public float Process(float x)
            {
                float sum = 0f;
                for (int k = 0; k < combs.Length; k++)
                {
                    var line = combs[k];
                    float y = line[combAt[k]];
                    combLow[k] = y * (1f - Damping) + combLow[k] * Damping;
                    line[combAt[k]] = x + combLow[k] * Feedback;
                    combAt[k] = (combAt[k] + 1) % line.Length;
                    sum += y;
                }
                for (int k = 0; k < allPasses.Length; k++)
                {
                    var line = allPasses[k];
                    float delayed = line[allPassAt[k]];
                    float y = -sum + delayed;
                    line[allPassAt[k]] = sum + delayed * 0.5f;
                    allPassAt[k] = (allPassAt[k] + 1) % line.Length;
                    sum = y;
                }
                return sum;
            }
        }
    }
}
