using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Круглая кнопка вибрации (меню и пауза): телефон с волнами - включено;
    // без волн, тусклый и перечёркнутый красным - выключено. Тап переключает, выбор общий для обеих кнопок
    [RequireComponent(typeof(PressableButton))]
    public class SpinvVibrationToggle : MonoBehaviour
    {
        public Image icon;
        public Sprite onSprite;
        public Sprite offSprite;
        public GameObject slash;
        [Range(0f, 1f)] public float offAlpha = 0.45f;

        PressableButton button;
        Color baseLabelColor;

        void Awake()
        {
            button = GetComponent<PressableButton>();
            baseLabelColor = button.normalLabelColor;
            button.onPress.AddListener(Toggle);
        }

        void OnEnable() => Refresh();

        void Toggle()
        {
            SpinvHaptics.Enabled = !SpinvHaptics.Enabled;
            Refresh();
            SpinvHaptics.Medium(); // включил - сразу почувствовал
        }

        void Refresh()
        {
            if (button == null) return;
            bool on = SpinvHaptics.Enabled;
            if (icon != null) icon.sprite = on ? onSprite : offSprite;
            if (slash != null) slash.SetActive(!on);
            Color c = baseLabelColor;
            c.a = on ? baseLabelColor.a : offAlpha;
            button.normalLabelColor = c;
            if (icon != null) icon.color = c;
        }
    }
}
