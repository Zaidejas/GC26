using System.Collections.Generic;
using GC26.Core;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Sound effects synthesised in code like the prototype's WebAudio sfx (square / sine / triangle / saw tones with a
    /// pitch slide and exponential decay, low-passed noise bursts), plus a quiet ambient pad per sector.
    /// No audio assets. Clips are created on first use and destroyed with the context.
    /// </summary>
    public sealed class BomberAudio : MonoBehaviour
    {
        const int Rate = 22050, Voices = 14;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly List<AudioSource> _src = new List<AudioSource>();
        AudioSource _music; string _musicKey;
        int _next;

        public float Volume { get; private set; } = 0.8f;
        public bool Music { get; private set; } = true;
        static string Pref(string k) => MicrogameSave.Key(BomberGame.Id, k);

        public void Init()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Pref("volume"), 0.8f));
            Music = PlayerPrefs.GetInt(Pref("music"), 1) == 1;
            for (int i = 0; i < Voices; i++) { var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0f; _src.Add(s); }
            _music = gameObject.AddComponent<AudioSource>(); _music.loop = true; _music.playOnAwake = false; _music.spatialBlend = 0f;
        }

        public void SetVolume(float v) { Volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(Pref("volume"), Volume); PlayerPrefs.Save(); _music.volume = MusicVol; }
        public void SetMusic(bool on) { Music = on; PlayerPrefs.SetInt(Pref("music"), on ? 1 : 0); PlayerPrefs.Save(); if (!on) _music.Stop(); _musicKey = null; }
        float MusicVol => Volume * 0.35f;

        void OnDestroy() { foreach (var c in _clips.Values) if (c) Destroy(c); _clips.Clear(); }

        /// <summary>Plays a named effect ("hit", "boom", "shoot:rail", "combo:12" ...). The same name at most every 35 ms.</summary>
        public void Play(string name)
        {
            if (Volume <= 0f || string.IsNullOrEmpty(name)) return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out var t) && now - t < 0.035f) return;
            _last[name] = now;
            var clip = Clip(name); if (clip == null) return;
            var s = _src[_next]; _next = (_next + 1) % _src.Count;
            s.clip = clip; s.volume = Volume; s.Play();
        }

        /// <summary>Ambient pad for the room theme (hub / sector 1..3), looped.</summary>
        public void SetTheme(int theme)
        {
            if (!Music || Volume <= 0f) { if (_music.isPlaying) _music.Stop(); return; }
            var key = "music" + theme;
            if (_musicKey == key && _music.isPlaying) { _music.volume = MusicVol; return; }
            _musicKey = key;
            if (!_clips.TryGetValue(key, out var clip)) _clips[key] = clip = MakePad(theme);
            _music.clip = clip; _music.volume = MusicVol; _music.Play();
        }

        // ================= synthesis =================
        AudioClip Clip(string name)
        {
            if (_clips.TryGetValue(name, out var c)) return c;
            float[] d = null;
            if (name.StartsWith("shoot:")) d = Shoot(name.Substring(6));
            else if (name.StartsWith("combo:")) { int n = int.TryParse(name.Substring(6), out var k) ? k : 1; d = Tone(500 + Mathf.Min(n, 30) * 30, 0.07f, 't', 0.025f, 1f); }
            else switch (name)
            {
                case "hit": d = Tone(300, 0.05f, 't', 0.03f, 0.5f); break;
                case "boom": d = Noise(0.45f, 0.16f, 600); break;
                case "hurt": d = Tone(160, 0.25f, 'w', 0.06f, 0.4f); break;
                case "over": d = Tone(520, 0.5f, 'w', 0.05f, 0.3f); break;
                case "pick": d = Tone(1320, 0.06f, 's', 0.03f, 1.4f); break;
                case "tele": d = Tone(1500, 0.08f, 's', 0.035f, 1f); break;
                case "vent": d = Noise(0.35f, 0.09f, 2600); break;
                case "dash": d = Tone(420, 0.1f, 's', 0.03f, 2f); break;
                case "door": d = Tone(260, 0.25f, 't', 0.05f, 1.5f); break;
                case "ability": d = Tone(300, 0.3f, 't', 0.06f, 3f); break;
                case "buy": d = Mix(Tone(880, 0.08f, 'q', 0.03f, 1.5f), Tone(1320, 0.12f, 's', 0.03f, 1f)); break;
                case "ach": d = Tone(660, 0.4f, 't', 0.06f, 2f); break;
            }
            if (d == null) return null;
            c = AudioClip.Create("gc26_bomber_" + name, d.Length, 1, Rate, false); c.SetData(d, 0);
            _clips[name] = c; return c;
        }

        static float[] Shoot(string id)
        {
            float f = id switch
            {
                "blaster" => 880, "shotgun" => 220, "rifle" => 660, "rail" => 140, "nail" => 1200, "plasma" => 330, "swarm" => 300, "flamer" => 90, "arc" => 1800, "cryo" => 1000,
                "beam" => 1400, "disc" => 500, "minigun" => 900, "stormgun" => 420, "dragon" => 180, "gauss" => 120, "cryoplasma" => 380, "grinder" => 700, "pulsar" => 980, "tesla" => 1500,
                "twin" => 820, "hammer" => 200, "scatter" => 260, "needle" => 1600, "venom" => 600, _ => 700,
            };
            if (id == "flamer" || id == "venom") return Noise(0.12f, 0.03f, 500);
            bool heavy = id == "shotgun" || id == "rail" || id == "gauss" || id == "dragon" || id == "scatter" || id == "hammer";
            var tone = Tone(f, 0.06f, id == "beam" || id == "tesla" ? 's' : 'q', id == "beam" || id == "tesla" ? 0.012f : 0.022f, 0.6f);
            return heavy ? Mix(tone, Noise(0.18f, 0.07f, id == "rail" || id == "gauss" || id == "needle" ? 3000 : 1400)) : tone;
        }

        const float Loud = 5f; // the prototype's WebAudio gains are tiny; scale them to a sensible AudioSource level

        /// <param name="wave">q square, s sine, t triangle, w saw</param>
        static float[] Tone(float freq, float dur, char wave, float vol, float slide)
        {
            int n = Mathf.CeilToInt(Rate * dur); var d = new float[n]; double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n, f = freq * Mathf.Pow(slide, k), g = vol * Loud * Mathf.Pow(0.0001f, k);
                ph += f / Rate; float x = (float)(ph - System.Math.Floor(ph));
                float v = wave == 's' ? Mathf.Sin(x * Mathf.PI * 2f) : wave == 't' ? 1f - 4f * Mathf.Abs(x - 0.5f) : wave == 'w' ? 2f * x - 1f : (x < 0.5f ? 1f : -1f);
                d[i] = Mathf.Clamp(v * g, -1f, 1f);
            }
            return d;
        }

        static float[] Noise(float dur, float vol, float cutoff)
        {
            int n = Mathf.CeilToInt(Rate * dur); var d = new float[n]; float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            var r = new System.Random(n);
            for (int i = 0; i < n; i++) { float x = (float)(r.NextDouble() * 2 - 1) * (1f - i / (float)n); y += a * (x - y); d[i] = Mathf.Clamp(y * vol * Loud * 1.6f, -1f, 1f); }
            return d;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var d = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp((i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f), -1f, 1f);
            return d;
        }

        /// <summary>16 s seamless pad: a minor chord of soft detuned sines with a slow swell, darker in later sectors.</summary>
        static AudioClip MakePad(int theme)
        {
            const float len = 16f; int n = Mathf.RoundToInt(Rate * len); var d = new float[n];
            float root = theme switch { 0 => 110f, 1 => 98f, 2 => 87.3f, _ => 77.8f };
            float[] chord = theme == 0 ? new[] { 1f, 1.25f, 1.5f, 2f } : new[] { 1f, 1.189f, 1.5f, 1.782f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int v = 0; v < chord.Length; v++)
                {
                    // whole number of cycles over the loop so it repeats without a click
                    float f = Mathf.Round(root * chord[v] * len) / len, f2 = Mathf.Round(root * chord[v] * 1.003f * len) / len;
                    float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (t / len * (v + 1)) + v);
                    s += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * f2 * t)) * swell;
                }
                d[i] = s * 0.045f;
            }
            var c = AudioClip.Create("gc26_bomber_pad" + theme, n, 1, Rate, false); c.SetData(d, 0); return c;
        }
    }
}
