using UnityEngine;

namespace GC26.Spinv
{
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

        // Способность куплена (номер ячейки) - по нему обучение идёт дальше
        public event System.Action<int> Used;

        WaveManager waves;
        int[] usedOnWave; // на какой волне способность применяли последний раз (достижение SHOPAHOLIC)

        void Start()
        {
            waves = FindAnyObjectByType<WaveManager>();
            usedOnWave = new int[abilities.Length];
        }

        void Update()
        {
            foreach (Ability ability in abilities)
            {
                ability.cooldownLeft = Mathf.Max(0f, ability.cooldownLeft - Time.deltaTime);

                if (!ability.IsActive) continue;

                ability.activeLeft -= Time.deltaTime;
                if (ability.type == AbilityType.Shield)
                    player.shieldTimeLeft = ability.activeLeft;
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
            return !ability.IsOnCooldown && SpinvGameManager.Instance.Coins >= ability.cost;
        }

        public bool TryUse(int index)
        {
            // На паузе и после конца игры Time.timeScale = 0
            if (Time.timeScale == 0f || !CanUse(index)) return false;

            Ability ability = abilities[index];
            SpinvGameManager.Instance.SpendCoins(ability.cost);
            ability.cooldownLeft = ability.cooldown;
            ability.activeLeft = ability.duration;
            SetEffect(ability.type, true);
            SpinvSoundManager.Play(Sfx.Coin);
            TrackForAchievement(index);
            Used?.Invoke(index);
            return true;
        }

        // SHOPAHOLIC: все способности применены за одну волну
        void TrackForAchievement(int index)
        {
            if (waves == null || waves.CurrentWave <= 0) return;
            usedOnWave[index] = waves.CurrentWave;
            foreach (int wave in usedOnWave)
                if (wave != waves.CurrentWave) return;
            Achievements.Award(AchievementId.Shopaholic);
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
}
