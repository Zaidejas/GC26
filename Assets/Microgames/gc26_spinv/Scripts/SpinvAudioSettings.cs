using UnityEngine;

namespace GC26.Spinv
{
    // Громкость из окна настроек: два ползунка, 0..1, запоминаются между запусками.
    // SOUND - игровые звуки (выстрелы, смерть пришельцев, бомбы, попадание по кораблю, монеты и сердечки),
    // SFX - всё остальное (интерфейс, печать, фон, музыка, боссы, способности, сценки).
    // Множитель поверх громкости, подобранной для каждого звука: на 100% всё звучит как задумано
    public static class SpinvAudioSettings
    {
        const string SoundKey = SpinvMicrogame.Id + "_SoundVolume";
        const string SfxKey = SpinvMicrogame.Id + "_SfxVolume";

        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat(SoundKey, 1f);
            set => PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value)); // сохраняет окно настроек при закрытии
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        }

        // Какие эффекты идут на ползунок SOUND (остальные - на SFX)
        public static bool IsSound(Sfx sfx) =>
            sfx == Sfx.Shoot || sfx == Sfx.EnemyShot || sfx == Sfx.InvaderDeath ||
            sfx == Sfx.BombExplosion || sfx == Sfx.PlayerHit ||
            sfx == Sfx.Coin || sfx == Sfx.Heal;
    }
}
