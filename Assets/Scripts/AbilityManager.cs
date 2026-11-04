using UnityEngine;

public enum AbilityType { Shield, RepairBunkers, RapidFire }

[System.Serializable]
public class Ability
{
    public AbilityType type;
    public int cost;
    public float cooldown;
    [Tooltip("Сколько длится эффект. 0 - срабатывает мгновенно")]
    public float duration;

    [System.NonSerialized] public float cooldownLeft;
    [System.NonSerialized] public float activeLeft;

    public bool IsActive => activeLeft > 0f;
    public bool IsOnCooldown => cooldownLeft > 0f;
}

// Способности покупаются нажатием прямо в бою: списываются монеты, эффект срабатывает, идёт перезарядка
public class AbilityManager : MonoBehaviour
{
    public Ability[] abilities;
    public PlayerController player;
    public Transform bunkers;
    public float rapidFireMultiplier = 2f;

    void Update()
    {
        foreach (Ability ability in abilities)
        {
            ability.cooldownLeft = Mathf.Max(0f, ability.cooldownLeft - Time.deltaTime);

            if (!ability.IsActive) continue;

            ability.activeLeft -= Time.deltaTime;
            if (!ability.IsActive)
                SetEffect(ability.type, false);
        }

        // Клавиши 1, 2, 3 для тестов в редакторе
        for (int i = 0; i < abilities.Length; i++)
        {
            if (GameInput.AbilityPressed(i))
                TryUse(i);
        }
    }

    public bool CanUse(int index)
    {
        Ability ability = abilities[index];
        return !ability.IsOnCooldown && GameManager.Instance.Coins >= ability.cost;
    }

    public bool TryUse(int index)
    {
        // На паузе и после конца игры Time.timeScale = 0
        if (Time.timeScale == 0f || !CanUse(index)) return false;

        Ability ability = abilities[index];
        GameManager.Instance.SpendCoins(ability.cost);
        ability.cooldownLeft = ability.cooldown;
        ability.activeLeft = ability.duration;
        SetEffect(ability.type, true);
        SoundManager.Play(Sfx.Coin);
        return true;
    }

    void SetEffect(AbilityType type, bool on)
    {
        switch (type)
        {
            case AbilityType.Shield:
                player.SetShield(on);
                break;
            case AbilityType.RapidFire:
                player.fireRateMultiplier = on ? rapidFireMultiplier : 1f;
                break;
            case AbilityType.RepairBunkers:
                if (on) RepairBunkers();
                break;
        }
    }

    void RepairBunkers()
    {
        foreach (BunkerBlock block in bunkers.GetComponentsInChildren<BunkerBlock>(true))
            block.Repair();
    }
}
