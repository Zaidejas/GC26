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

    public Color normalColor = new Color(0.12f, 0.14f, 0.2f, 0.9f);
    public Color activeColor = new Color(0.25f, 0.75f, 0.4f, 0.95f);
    public Color costColor = new Color(1f, 0.85f, 0.2f);
    public Color noCoinsColor = new Color(1f, 0.3f, 0.3f);
    [Range(0f, 1f)] public float dimmedIconAlpha = 0.35f;

    void Start()
    {
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
