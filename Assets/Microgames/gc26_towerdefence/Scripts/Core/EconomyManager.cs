using System;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The player's gold. Starts at 250 (prototype CONFIG.startGold).
    /// UI listens to OnGoldChanged instead of reading Gold every frame.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        [SerializeField, Min(0)] int startGold = 250;

        /// <summary>Raised with the new amount whenever gold changes (also once on ResetGold).</summary>
        public event Action<int> OnGoldChanged;

        public int Gold { get; private set; }
        public int StartGold => startGold;

        void Awake() => Gold = startGold;

        /// <summary>Back to the starting amount (new level or restart).</summary>
        public void ResetGold()
        {
            Gold = startGold;
            OnGoldChanged?.Invoke(Gold);
        }

        public bool CanAfford(int cost) => cost <= Gold;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        /// <summary>Takes the gold if there is enough. Returns false and changes nothing otherwise.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || cost > Gold) return false;
            if (cost == 0) return true;
            Gold -= cost;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }
    }
}
