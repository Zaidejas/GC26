using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ячейка панели способностей: показывает цену, перезарядку и состояние, по нажатию покупает способность
public class AbilityButton : MonoBehaviour
{
    public AbilityManager manager;
    public int index;
    public Button button;
    public Image background;
    public Image icon;
    public Image cooldownOverlay;
    public TMP_Text cooldownText;
    public TMP_Text costText;

    public Color normalColor = Color.white;
    public Color activeColor = new Color(0.65f, 1f, 0.75f);
    public Color costColor = new Color(0.78f, 1f, 0.84f);
    public Color noCoinsColor = new Color(0.13f, 0.45f, 0.2f);
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
        bool hasCoins = GameManager.Instance.Coins >= ability.cost;

        background.color = ability.IsActive ? activeColor : normalColor;
        costText.color = hasCoins ? costColor : noCoinsColor;

        Color iconColor = icon.color;
        iconColor.a = manager.CanUse(index) ? 1f : dimmedIconAlpha;
        icon.color = iconColor;

        cooldownOverlay.fillAmount = ability.IsOnCooldown ? ability.cooldownLeft / ability.cooldown : 0f;
        cooldownText.text = ability.IsOnCooldown ? Mathf.CeilToInt(ability.cooldownLeft).ToString() : "";
    }
}
