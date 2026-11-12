using UnityEngine;

namespace GC26.Spinv
{
    // Окно настроек из меню (шестерёнка в правом верхнем углу): ползунки громкости и кнопка вибрации
    public class SpinvSettingsScreen : MonoBehaviour
    {
        public GameObject root;
        public PressableButton openButton;
        public PressableButton closeButton;

        void Awake()
        {
            openButton.onPress.AddListener(() => root.SetActive(true));
            closeButton.onPress.AddListener(() => root.SetActive(false));
            root.SetActive(false);
        }
    }
}
