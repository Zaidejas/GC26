using UnityEngine;

namespace GC26.Spinv
{
    // Вибрация телефона короткими импульсами разной силы (Android: VibrationEffect, без плагинов).
    // Включается/выключается переключателем в меню и на паузе, выбор запоминается
    public static class SpinvHaptics
    {
        public const string EnabledKey = SpinvMicrogame.Id + "_Vibration";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // Лёгкая (достижение), средняя (урон, сцена самозванца)
        public static void Light() => Vibrate(15, 70);
        public static void Medium() => Vibrate(40, 160);

        // Ссылка на Handheld.Vibrate нужна, чтобы Unity добавила в APK разрешение на вибрацию; сам вызов не выполняется
        static readonly bool requestPermission = false;

        public static void Vibrate(long milliseconds, int amplitude)
        {
            if (!Enabled) return;
            if (requestPermission) Handheld.Vibrate();
    #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                                vibrator.Call("vibrate", oneShot);
                        }
                        else
                        {
                            vibrator.Call("vibrate", milliseconds);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Space Invaders: vibration failed: " + e.Message);
            }
    #endif
        }
    }
}
