using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Два ползунка громкости - SOUND и SFX (в окне настроек меню и на паузе).
    // Громкость меняется сразу, пока тянешь; сохраняется, когда окно/пауза закрывается
    public class SpinvVolumeSliders : MonoBehaviour
    {
        public Slider soundSlider;
        public Slider sfxSlider;
        public TMP_Text soundValue;
        public TMP_Text sfxValue;

        void Awake()
        {
            soundSlider.onValueChanged.AddListener(v => { SpinvAudioSettings.SoundVolume = v; ShowValues(); });
            sfxSlider.onValueChanged.AddListener(v => { SpinvAudioSettings.SfxVolume = v; ShowValues(); });
        }

        void OnEnable()
        {
            soundSlider.SetValueWithoutNotify(SpinvAudioSettings.SoundVolume);
            sfxSlider.SetValueWithoutNotify(SpinvAudioSettings.SfxVolume);
            ShowValues();
        }

        void OnDisable()
        {
            PlayerPrefs.Save();
        }

        void ShowValues()
        {
            soundValue.text = Mathf.RoundToInt(soundSlider.value * 100f) + "%";
            sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        }
    }
}
