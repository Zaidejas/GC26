using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Экран достижений: звёздная карта. Полученные звёзды горят, по тапу описание печатается в окне.
    // Аркадные звёзды и их линии не показываются, пока аркада не открыта
    public class StarMapScreen : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text counterText;
        [Tooltip("Звёзды в порядке AchievementId")]
        public Image[] stars;
        public RectTransform selectionRing;
        public Image[] lines;
        [Tooltip("Какие две звезды соединяет линия с тем же номером")]
        public int[] lineFrom;
        public int[] lineTo;
        public TMP_Text dialogTitle;
        public TMP_Text dialogDescription;
        public PressableButton backButton;

        public Color litColor = new Color(0.22f, 1f, 0.42f);
        public Color lockedColor = new Color(0.11f, 0.29f, 0.17f);
        public float lockedScale = 0.75f;
        public float charTime = 0.03f;

        Coroutine typing;

        void Start()
        {
            for (int i = 0; i < stars.Length; i++)
            {
                int index = i;
                stars[i].GetComponent<Button>().onClick.AddListener(() => Select(index));
            }
            backButton.onPress.AddListener(() => root.SetActive(false));
            root.SetActive(false);
        }

        public void Open()
        {
            root.SetActive(true);
            counterText.text = $"{Achievements.UnlockedVisibleCount} / {Achievements.VisibleCount}";

            int first = -1;
            for (int i = 0; i < stars.Length; i++)
            {
                AchievementDef def = Achievements.All[i];
                bool visible = Achievements.IsVisible(def);
                stars[i].gameObject.SetActive(visible);
                if (!visible) continue;
                if (first < 0) first = i;

                bool unlocked = Achievements.IsUnlocked(def.id);
                stars[i].color = unlocked ? litColor : lockedColor;
                stars[i].transform.localScale = Vector3.one * (unlocked ? 1f : lockedScale);
            }
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].gameObject.SetActive(
                    Achievements.IsVisible(Achievements.All[lineFrom[i]]) && Achievements.IsVisible(Achievements.All[lineTo[i]]));
            }
            if (first >= 0) Select(first);
        }

        void Select(int index)
        {
            AchievementDef def = Achievements.All[index];
            selectionRing.anchoredPosition = stars[index].rectTransform.anchoredPosition;
            dialogTitle.text = Achievements.TitleOf(def);
            if (typing != null) StopCoroutine(typing);
            typing = StartCoroutine(TypeDescription(Achievements.DescriptionOf(def)));
        }

        IEnumerator TypeDescription(string text)
        {
            for (int k = 1; k <= text.Length; k++)
            {
                dialogDescription.text = text.Substring(0, k) + (k < text.Length ? "_" : "");
                SpinvUiSound.TypeTick(text[k - 1]);
                yield return new WaitForSecondsRealtime(charTime);
            }
        }
    }
}
