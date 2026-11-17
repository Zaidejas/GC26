using System.Collections.Generic;
using GC26.Lines.View;
using UnityEngine;

namespace GC26.Lines.Haptics
{
    /// <summary>The vibration "feels" the game uses (GDD → Controls and feedback → Vibration).</summary>
    public enum LinesFeel
    {
        /// <summary>The lightest tick: Vibration switched on in Settings.</summary>
        Tick,
        /// <summary>A firm tap: a line cleared.</summary>
        Clear,
        /// <summary>A stronger tap: a line of 7 or more.</summary>
        ClearBig,
        /// <summary>Two quick soft knocks, the phone's own "error" feel: no path.</summary>
        Error,
        /// <summary>The phone's own "success" feel: a new record.</summary>
        Success,
        /// <summary>One heavy thump: game over.</summary>
        GameOver,
        /// <summary>L16: a puffball bursts: one heavy thump.</summary>
        Burst,
        /// <summary>L16: a puffball earned: a light then a firmer tap ("reward").</summary>
        Reward,
    }

    /// <summary>
    /// Task L02: vibration through the phone's own light, firm and heavy feels. On the iPhone that is its haptics
    /// engine (a small piece of our own native code, <c>LinesHapticsIOS.mm</c>), which also works with the silent
    /// switch on; on Android the matching vibration effects. Unity's own <c>Handheld.Vibrate</c> is one long, strong
    /// buzz, so it is used only to make Unity add Android's vibration permission. In the editor nothing vibrates,
    /// but every feel is remembered in <see cref="Played"/> (for tests).
    /// </summary>
    public sealed class LinesHaptics
    {
        /// <summary>Seconds between the taps when one move clears two or more lines.</summary>
        public const float RepeatGap = 0.12f;
        /// <summary>A line of this length or longer gets the stronger tap.</summary>
        public const int BigLine = 7;

        /// <summary>Follows the Vibration switch in Settings.</summary>
        public bool Enabled { get; set; }

        /// <summary>Every feel asked for while on, in order (repeats included).</summary>
        public readonly List<LinesFeel> Played = new List<LinesFeel>();

        public LinesHaptics(bool enabled) { Enabled = enabled; }

        /// <summary>Plays <paramref name="feel"/> <paramref name="times"/> times, <see cref="RepeatGap"/> apart.</summary>
        public void Play(LinesFeel feel, int times = 1)
        {
            if (!Enabled) return;
            Fire(feel);
            for (int i = 1; i < times; i++)
                Tween.Delay(i * RepeatGap, () => { if (Enabled) Fire(feel); });
        }

        /// <summary>The feel for clearing lines, the longest of them <paramref name="longest"/> long.</summary>
        public static LinesFeel ForLine(int longest) => longest >= BigLine ? LinesFeel.ClearBig : LinesFeel.Clear;

        void Fire(LinesFeel feel)
        {
            Played.Add(feel);
#if UNITY_IOS && !UNITY_EDITOR
            LinesHaptics_Play((int)feel);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Android.Play(feel);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LinesHaptics_Play(int feel);
#endif

        // Never called: its mere presence makes Unity add the VIBRATE permission to the Android app.
        static void NeedsVibratePermission() => Handheld.Vibrate();

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Android's vibrator, with the predefined click effects on Android 10+ and short pulses before.</summary>
        static class Android
        {
            static AndroidJavaObject vibrator;
            static AndroidJavaClass effects;
            static int sdk;
            static bool ready, failed;

            static bool Ready()
            {
                if (ready || failed) return ready;
                try
                {
                    sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    effects = new AndroidJavaClass("android.os.VibrationEffect");
                    ready = vibrator != null && vibrator.Call<bool>("hasVibrator");
                    failed = !ready;
                }
                catch (System.Exception e)
                {
                    failed = true;
                    Debug.LogWarning("Lines: no vibration on this phone: " + e.Message);
                }
                return ready;
            }

            public static void Play(LinesFeel feel)
            {
                if (!Ready()) return;
                try
                {
                    AndroidJavaObject effect;
                    // Predefined effects (Android 10+): EFFECT_CLICK 0, EFFECT_DOUBLE_CLICK 1, EFFECT_TICK 2, EFFECT_HEAVY_CLICK 5.
                    if (sdk >= 29 && feel != LinesFeel.Success && feel != LinesFeel.GameOver && feel != LinesFeel.Reward)
                        effect = effects.CallStatic<AndroidJavaObject>("createPredefined",
                            feel == LinesFeel.Tick ? 2 : feel == LinesFeel.Clear ? 0 : feel == LinesFeel.ClearBig || feel == LinesFeel.Burst ? 5 : 1);
                    else
                    {
                        long[] times; int[] strengths;
                        switch (feel)
                        {
                            case LinesFeel.Tick: times = new long[] { 8 }; strengths = new[] { 90 }; break;
                            case LinesFeel.Clear: times = new long[] { 22 }; strengths = new[] { 180 }; break;
                            case LinesFeel.ClearBig: times = new long[] { 35 }; strengths = new[] { 255 }; break;
                            case LinesFeel.Error: times = new long[] { 14, 60, 14 }; strengths = new[] { 140, 0, 140 }; break;
                            case LinesFeel.Success: times = new long[] { 12, 70, 12, 70, 28 }; strengths = new[] { 110, 0, 160, 0, 230 }; break;
                            case LinesFeel.Reward: times = new long[] { 14, 60, 30 }; strengths = new[] { 110, 0, 220 }; break;
                            default: times = new long[] { 70 }; strengths = new[] { 255 }; break; // game over
                        }
                        effect = effects.CallStatic<AndroidJavaObject>("createWaveform", times, strengths, -1);
                    }
                    using (effect) vibrator.Call("vibrate", effect);
                }
                catch (System.Exception e)
                {
                    failed = true; ready = false;
                    Debug.LogWarning("Lines: vibration failed: " + e.Message);
                }
            }
        }
#endif
    }
}
