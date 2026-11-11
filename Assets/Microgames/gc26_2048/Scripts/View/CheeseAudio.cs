using UnityEngine;

namespace GC26._2048
{
    public enum CheeseSound
    {
        Rustle,
        Chime,
        Moo,
        Fanfare,
        Beep
    }

    /// <summary>
    /// Placeholder sounds generated at start (rustle, chime, moo, fanfare with a squeak, warning beep).
    /// Replace with real clips from Kenney / freesound in week 6.
    /// </summary>
    public class CheeseAudio : MonoBehaviour
    {
        const int Rate = 44100;

        AudioSource source;
        AudioClip rustle, moo, fanfare, beep;
        readonly AudioClip[] chimes = new AudioClip[CheeseRules.CheeseLevel + 1];

        public bool Enabled { get; set; } = true;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            float lastNoise = 0f;
            rustle = Make("rustle", 0.12f, t =>
            {
                float noise = Random.Range(-1f, 1f);
                float high = noise - lastNoise; // crude high-pass for a dry rustle
                lastNoise = noise;
                return high * 0.35f * Mathf.Pow(Mathf.Clamp01(1f - t / 0.12f), 1.5f);
            });
            moo = Make("moo", 0.65f, Moo);
            beep = Make("beep", 0.12f, t => Square(330f, t) * 0.25f * Env(t, 0.12f));
            fanfare = Make("fanfare", 0.85f, Fanfare);
            for (int level = 1; level <= CheeseRules.CheeseLevel; level++)
            {
                float f = 420f * Mathf.Pow(1.16f, level);
                chimes[level] = Make("chime", 0.35f, t =>
                    (Tri(f, t) * 0.6f + Mathf.Sin(2 * Mathf.PI * f * 2 * t) * 0.2f) * Env(t, 0.35f));
            }
        }

        void OnDestroy()
        {
            Destroy(rustle);
            Destroy(moo);
            Destroy(beep);
            Destroy(fanfare);
            foreach (var c in chimes)
                if (c != null) Destroy(c);
        }

        public void Play(CheeseSound sound, int level = 1)
        {
            if (!Enabled || source == null) return;
            switch (sound)
            {
                case CheeseSound.Rustle: source.PlayOneShot(rustle, 0.5f); break;
                case CheeseSound.Moo: source.PlayOneShot(moo, 0.8f); break;
                case CheeseSound.Fanfare: source.PlayOneShot(fanfare, 0.8f); break;
                case CheeseSound.Beep: source.PlayOneShot(beep, 0.6f); break;
                default: source.PlayOneShot(chimes[Mathf.Clamp(level, 1, CheeseRules.CheeseLevel)], 0.7f); break;
            }
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(wave((float)i / Rate), -1f, 1f);
            var clip = AudioClip.Create("gc26_2048_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float length) => Mathf.Clamp01(t / 0.012f) * Mathf.Pow(Mathf.Clamp01(1f - t / length), 2f);
        static float Tri(float f, float t) => 2f * Mathf.Abs(2f * (t * f - Mathf.Floor(t * f + 0.5f))) - 1f;
        static float Square(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t) >= 0 ? 1f : -1f;

        static float Moo(float t)
        {
            float f = Mathf.Lerp(150f, 125f, t / 0.6f);
            float saw = 2f * (t * f - Mathf.Floor(t * f + 0.5f));
            float soft = saw * 0.5f + Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f;
            float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((0.65f - t) / 0.25f);
            return soft * 0.45f * env;
        }

        static float Fanfare(float t)
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float local = t - i * 0.1f;
                if (local >= 0f && local < 0.3f) sum += Tri(notes[i], local) * 0.3f * Env(local, 0.3f);
            }
            // two quick mouse squeaks after the chord
            for (int i = 0; i < 2; i++)
            {
                float local = t - 0.45f - i * 0.15f;
                if (local >= 0f && local < 0.09f)
                {
                    float f = Mathf.Lerp(1700f + i * 200f, 2600f + i * 300f, local / 0.09f);
                    sum += Mathf.Sin(2 * Mathf.PI * f * local) * 0.25f * Env(local, 0.09f);
                }
            }
            return sum;
        }
    }
}
