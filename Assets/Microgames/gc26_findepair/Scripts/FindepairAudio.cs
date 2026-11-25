using System.Collections;
using UnityEngine;

namespace GC26.Findepair
{
    public enum FindepairSfx { Flip, Match, Miss, Win }

    /// <summary>
    /// Sounds, music and vibration. The clips are synthesized at startup with the same notes as the
    /// HTML prototype, so the game needs no audio files; real CC0 clips can replace them later.
    /// </summary>
    public class FindepairAudio : MonoBehaviour
    {
        const int Rate = 44100;

        // Set from code only: lets Unity see Handheld.Vibrate and add the VIBRATE permission on Android.
        static bool _neverTrue = false;

        AudioSource _sfx, _music;
        AudioClip _flip, _match, _miss, _win, _loop;
        FindepairSave _save;

        public void Init(FindepairSave save)
        {
            _save = save;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.8f;

            _flip = Make("flip", 0.12f, FlipSound);
            _match = Make("match", 0.8f, MatchSound);
            _miss = Make("miss", 0.4f, b => Tone(b, 170, 0, 0.32f, Wave.Sine, 0.14f, 4000, 110));
            _win = Make("win", 1.1f, WinSound);
            _loop = Make("music", 8f, MusicLoop);
        }

        public void Play(FindepairSfx sfx)
        {
            if (_save == null || !_save.Sound) return;
            var clip = sfx switch
            {
                FindepairSfx.Flip => _flip,
                FindepairSfx.Match => _match,
                FindepairSfx.Miss => _miss,
                _ => _win,
            };
            _sfx.PlayOneShot(clip);
        }

        public void SetMusic(bool on)
        {
            if (_music == null) return;
            if (on && _save.Music)
            {
                if (!_music.isPlaying) { _music.clip = _loop; _music.Play(); }
            }
            else _music.Stop();
        }

        public void StopAll()
        {
            if (_music != null) _music.Stop();
            if (_sfx != null) _sfx.Stop();
        }

        public void Vibrate(long ms)
        {
            if (_save == null || !_save.Vibro) return;
#if UNITY_ANDROID || UNITY_IOS
            if (_neverTrue) Handheld.Vibrate();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator?.Call("vibrate", ms);
            }
            catch (System.Exception e) { Debug.LogWarning($"[{FindepairGame.Id}] vibrate failed: {e.Message}"); }
#endif
        }

        /// <summary>Win buzz: 60 ms, pause, 60 ms.</summary>
        public void VibrateWin()
        {
            if (_save == null || !_save.Vibro) return;
            StartCoroutine(Twice());
            IEnumerator Twice()
            {
                Vibrate(60);
                yield return new WaitForSecondsRealtime(0.1f);
                Vibrate(60);
            }
        }

        // ---------- Synthesis (same notes as the prototype) ----------

        enum Wave { Sine, Square, Saw, Triangle }

        static AudioClip Make(string name, float seconds, System.Action<float[]> fill)
        {
            var buf = new float[Mathf.CeilToInt(seconds * Rate)];
            fill(buf);
            for (int i = 0; i < buf.Length; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            var clip = AudioClip.Create($"{FindepairGame.Id}_{name}", buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        // flip: Polaroid shutter click
        static void FlipSound(float[] b)
        {
            Noise(b, 0, 0.045f, 0.35f);
            Tone(b, 1800, 0, 0.03f, Wave.Square, 0.05f, 4000);
            Noise(b, 0.06f, 0.03f, 0.2f);
        }

        // pair found: guitar chord (E major, arpeggiated)
        static void MatchSound(float[] b)
        {
            float[] notes = { 164.8f, 246.9f, 329.6f, 415.3f, 493.9f };
            for (int i = 0; i < notes.Length; i++) Tone(b, notes[i], i * 0.018f, 0.7f, Wave.Saw, 0.06f, 1900);
        }

        // win: short pop-punk riff
        static void WinSound(float[] b)
        {
            float[,] riff = { { 329.6f, 0f }, { 392f, .14f }, { 440f, .28f }, { 493.9f, .42f }, { 440f, .56f }, { 587.3f, .7f }, { 659.3f, .84f } };
            for (int i = 0; i < riff.GetLength(0); i++)
            {
                Tone(b, riff[i, 0], riff[i, 1], 0.2f, Wave.Square, 0.07f, 2600);
                Tone(b, riff[i, 0] / 2f, riff[i, 1], 0.2f, Wave.Saw, 0.05f, 900);
            }
        }

        // background: calm four-chord loop, Em - C - G - D, 2 s per bar
        static void MusicLoop(float[] b)
        {
            float[][] chords =
            {
                new[] { 164.8f, 196f, 246.9f }, new[] { 130.8f, 164.8f, 196f },
                new[] { 196f, 246.9f, 293.7f }, new[] { 146.8f, 185f, 220f },
            };
            for (int bar = 0; bar < 4; bar++)
            {
                float t0 = bar * 2f;
                for (int i = 0; i < 3; i++) Tone(b, chords[bar][i], t0 + i * 0.12f, 1.9f, Wave.Triangle, 0.025f, 1200);
                Tone(b, chords[bar][0] / 2f, t0, 1.9f, Wave.Sine, 0.04f, 4000);
            }
        }

        /// <summary>Oscillator with a 10 ms exponential attack and exponential decay, through a one-pole low-pass.</summary>
        static void Tone(float[] b, float freq, float start, float dur, Wave wave, float vol, float cutoff, float glideTo = 0)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            float phase = 0, lp = 0, a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            const float floor = 0.0001f, attack = 0.01f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = glideTo > 0 ? freq * Mathf.Pow(glideTo / freq, t / dur) : freq;
                phase += f / Rate;
                phase -= Mathf.Floor(phase);
                float x = wave switch
                {
                    Wave.Square => phase < 0.5f ? 1f : -1f,
                    Wave.Saw => 2f * phase - 1f,
                    Wave.Triangle => 1f - 4f * Mathf.Abs(phase - 0.5f),
                    _ => Mathf.Sin(2f * Mathf.PI * phase),
                };
                float g = t < attack
                    ? floor * Mathf.Pow(vol / floor, t / attack)
                    : vol * Mathf.Pow(floor / vol, (t - attack) / (dur - attack));
                lp += a * (x - lp);
                b[s0 + i] += lp * g;
            }
        }

        /// <summary>Fading white noise through a one-pole high-pass at 1800 Hz.</summary>
        static void Noise(float[] b, float start, float dur, float vol)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            var rng = new System.Random(7);
            float rc = 1f / (2f * Mathf.PI * 1800f), dt = 1f / Rate, k = rc / (rc + dt);
            float prevX = 0, y = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (1f - i / (float)n);
                y = k * (y + x - prevX);
                prevX = x;
                b[s0 + i] += y * vol;
            }
        }
    }
}
