using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Spinv
{
    // Круглая ячейка способности: цена на «монетке», кольцо перезарядки, по нажатию покупает способность
    public class AbilityButton : MonoBehaviour
    {
        public AbilityManager manager;
        public int index;
        public Button button;
        public Image background;
        public Image icon;
        public Image cooldownRing;
        public TMP_Text cooldownText;
        public Image costBadge;
        public TMP_Text costText;

        public Color normalColor = Color.white;
        public Color activeColor = new Color(0.65f, 1f, 0.75f);
        [Tooltip("Обводка, пока идёт перезарядка: по ней бежит яркое кольцо")]
        public Color cooldownColor = new Color(0.25f, 0.5f, 0.3f);

        [Header("Монетка с ценой")]
        public Color badgeColor = new Color(0.22f, 1f, 0.42f);
        public Color badgeTextColor = new Color(0.024f, 0.082f, 0.043f);
        public Color waitBadgeColor = new Color(0.12f, 0.35f, 0.19f);
        public Color waitBadgeTextColor = new Color(0.62f, 0.91f, 0.69f);
        public Color noCoinsBadgeColor = new Color(0.35f, 0.1f, 0.1f);
        public Color noCoinsTextColor = new Color(1f, 0.42f, 0.42f);

        [Range(0f, 1f)] public float dimmedIconAlpha = 0.35f;
        [Tooltip("Прозрачность всей ячейки: сквозь неё видно игровое поле")]
        [Range(0f, 1f)] public float panelAlpha = 0.7f;

        void Start()
        {
            gameObject.AddComponent<CanvasGroup>().alpha = panelAlpha;
            button.onClick.AddListener(() => manager.TryUse(index));
            costText.text = manager.abilities[index].cost.ToString();
        }

        void Update()
        {
            Ability ability = manager.abilities[index];
            bool hasCoins = SpinvGameManager.Instance.Coins >= ability.cost;
            bool cooling = ability.IsOnCooldown;

            background.color = ability.IsActive ? activeColor : cooling ? cooldownColor : normalColor;

            // Во время перезарядки вместо значка - секунды
            Color iconColor = icon.color;
            iconColor.a = cooling ? 0f : manager.CanUse(index) ? 1f : dimmedIconAlpha;
            icon.color = iconColor;

            cooldownRing.fillAmount = cooling ? 1f - ability.cooldownLeft / ability.cooldown : 0f;
            cooldownText.text = cooling ? Mathf.CeilToInt(ability.cooldownLeft).ToString() : "";

            if (!hasCoins)
            {
                costBadge.color = noCoinsBadgeColor;
                costText.color = noCoinsTextColor;
            }
            else if (cooling || ability.IsActive)
            {
                costBadge.color = waitBadgeColor;
                costText.color = waitBadgeTextColor;
            }
            else
            {
                costBadge.color = badgeColor;
                costText.color = badgeTextColor;
            }
        }
    }
}
