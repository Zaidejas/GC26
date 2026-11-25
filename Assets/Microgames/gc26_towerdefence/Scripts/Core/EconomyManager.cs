using System;
using UnityEngine;

namespace GC26.Towerdefence
{
    /// <summary>
    /// The player's gold. LevelFlow sets the starting amount from LevelData (250 in the prototype level).
    /// UI listens to OnGoldChanged instead of reading Gold every frame.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        /// <summary>Raised with the new amount whenever gold changes (also once on ResetGold).</summary>
        public event Action<int> OnGoldChanged;

        public int Gold { get; private set; }
        /// <summary>Sets the starting amount (new level or restart).</summary>
        public void ResetGold(int startGold)
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
