using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>Generates every sound in code, so the project needs no audio files yet.</summary>
    public sealed class SoundBank
    {
        const int Rate = 22050;

        public bool Muted;

        readonly AudioSource source;
        readonly AudioClip shot;
        readonly AudioClip enemyShot;
        readonly AudioClip spark;
        readonly AudioClip blast;
        readonly AudioClip bigBlast;
        readonly AudioClip pickup;

        public SoundBank(GameObject owner)
        {
            source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            shot = Sweep("shot", 0.09f, 620f, 180f, 0.25f, 0.5f);
            enemyShot = Sweep("enemyShot", 0.08f, 380f, 140f, 0.2f, 0.22f);
            spark = Sweep("spark", 0.05f, 1400f, 900f, 0.5f, 0.2f);
            blast = Sweep("blast", 0.3f, 160f, 40f, 0.85f, 0.6f);
            bigBlast = Sweep("bigBlast", 0.7f, 110f, 26f, 0.9f, 0.9f);
            pickup = Sweep("pickup", 0.22f, 440f, 1320f, 0f, 0.45f);
        }

        public void Shot(bool player) => Play(player ? shot : enemyShot);
        public void Spark() => Play(spark);
        public void Blast() => Play(blast);
        public void BigBlast() => Play(bigBlast);
        public void Pickup() => Play(pickup);

        void Play(AudioClip clip)
        {
            if (!Muted) source.PlayOneShot(clip);
        }

        static AudioClip Sweep(string name, float seconds, float fromHz, float toHz, float noise, float volume)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                phase += Mathf.Lerp(fromHz, toHz, t) / Rate;
                float tone = Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
                float hiss = (float)random.NextDouble() * 2f - 1f;
                data[i] = Mathf.Lerp(tone, hiss, noise) * volume * (1f - t) * (1f - t);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
