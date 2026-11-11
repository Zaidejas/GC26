using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Аркадная кнопка: загорается, пока палец на ней, при отпускании мигает как лампа
    // и только потом срабатывает. Если палец увели с кнопки - гаснет без нажатия.
    // Работает на реальном времени: на экранах конца игры Time.timeScale = 0
    public class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Image background;
        public Image glow;
        public TMP_Text label;
        [Tooltip("Значок вместо надписи (стрелки), красится как надпись")]
        public Graphic icon;

        [Header("Обычное состояние")]
        public Sprite normalSprite;
        public Color normalColor = Color.white;
        public Color normalLabelColor = new Color(0.66f, 0.96f, 0.74f);

        [Header("Нажатие")]
        public Sprite litSprite;
        public Color litColor = Color.white;
        public Color litLabelColor = new Color(0.02f, 0.08f, 0.04f);
        [Range(0.8f, 1f)] public float pressedScale = 0.96f;

        public bool interactable = true;
        public UnityEvent onPress = new UnityEvent();

        [Tooltip("Насколько видна неактивная кнопка (SetInteractable(false))")]
        [Range(0f, 1f)] public float disabledAlpha = 0.35f;

        bool held;
        bool firing;

        // Включает/выключает кнопку и показывает это: неактивная - полупрозрачная и не нажимается
        public void SetInteractable(bool on)
        {
            interactable = on;
            if (!on)
            {
                held = false;
                SetLit(false);
            }
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = on ? 1f : disabledAlpha;
        }

        void OnEnable()
        {
            held = firing = false;
            SetLit(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable || firing) return;
            held = true;
            SetLit(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!held) return;
            held = false;
            SetLit(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!held) return;
            held = false;
            StartCoroutine(Fire());
        }

        IEnumerator Fire()
        {
            firing = true;
            SetLit(false);
            yield return new WaitForSecondsRealtime(0.08f);
            SetLit(true);
            yield return new WaitForSecondsRealtime(0.1f);
            SetLit(false);
            firing = false;
            onPress.Invoke();
        }

        void SetLit(bool lit)
        {
            if (background != null)
            {
                if (litSprite != null) background.sprite = lit ? litSprite : normalSprite;
                background.color = lit ? litColor : normalColor;
            }
            if (glow != null) glow.enabled = lit;
            if (label != null) label.color = lit ? litLabelColor : normalLabelColor;
            if (icon != null) icon.color = lit ? litLabelColor : normalLabelColor;
            transform.localScale = Vector3.one * (lit ? pressedScale : 1f);
        }
    }
}
