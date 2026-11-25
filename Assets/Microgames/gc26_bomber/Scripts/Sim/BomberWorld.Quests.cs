using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    // NPC quests, Overload difficulty levels and permanent Mk weapon upgrades (prototype qProg, placeQuestItem, OVERLOAD, MK_COST).
    public sealed partial class BomberWorld
    {
        // ================= quests =================
        void QProgress(string type, string target, int n = 1)
        {
            if (Mode != BomberMode.Run) return;
            foreach (var q in BomberMetaData.Quests)
            {
                if (!Meta.QActive(q) || q.Type != type || (q.Target != null && q.Target != "any" && q.Target != target)) continue;
                int p = Meta.QProg(q.Id); if (p >= q.Need) continue;
                p = Mathf.Min(q.Need, p + n); Meta.SetQProg(q.Id, p);
                if (p >= q.Need) Toast?.Invoke($"Квест выполнен: «{q.Name}». Вернись к {BomberMetaData.Npc(q.Npc).Name} в хаб за наградой.", 3.2f);
                SaveMeta();
            }
        }

        /// <summary>An item for an active fetch quest may lie somewhere in a room (38% of rooms).</summary>
        void PlaceQuestItem()
        {
            if (Mode != BomberMode.Run) return;
            var need = new List<BomberQuestDef>();
            foreach (var q in BomberMetaData.Quests) if (q.Type == "fetch" && Meta.QActive(q) && Meta.QProg(q.Id) < q.Need) need.Add(q);
            if (need.Count == 0 || _rngC.Next() > 0.38f) return;
            var qd = _rngC.Pick(need);
            var far = FreeCells().FindAll(c => Dist(c.x * Cell + 16f, c.y * Cell + 16f, P.X, P.Y) > 260f);
            if (far.Count == 0) return;
            var cell = far[_rngC.Range(far.Count)];
            Pickups.Add(new BomberPickup { Kind = "quest", Id = qd.Id, X = cell.x * Cell + 16f, Y = cell.y * Cell + 16f });
        }

        void CollectQuestItem(BomberPickup p)
        {
            var q = BomberMetaData.Quest(p.Id); if (q == null || !Meta.QActive(q)) return;
            int pr = Mathf.Min(q.Need, Meta.QProg(q.Id) + 1); Meta.SetQProg(q.Id, pr); SaveMeta();
            for (int i = 0; i < 20; i++) Spark(p.X, p.Y, BomberData.C("#b48cff"), 220f);
            Toast?.Invoke(pr >= q.Need ? $"{q.Item}: {q.Need}/{q.Need}. Квест «{q.Name}» выполнен, вернись к {BomberMetaData.Npc(q.Npc).Name}." : $"Найдено: {q.Item} {pr}/{q.Need}", 2.6f);
        }

        public bool TakeQuest(string id)
        {
            var q = BomberMetaData.Quest(id); if (q == null || Meta.QTaken(id) || Meta.QDone.Contains(id) || Meta.QuestOf(q.Npc) != q) return false;
            Meta.SetQProg(id, 0); SaveMeta(); Toast?.Invoke($"Квест взят: «{q.Name}»", 2f); return true;
        }

        public bool TurnInQuest(string id)
        {
            var q = BomberMetaData.Quest(id); if (!Meta.QReady(q)) return false;
            Meta.QDone.Add(id); Meta.Quests.RemoveAll(x => x.Id == id); Meta.Data += q.RewardData;
            SaveMeta(); RefreshHubPlayer();
            Toast?.Invoke($"Награда: {BomberMetaData.RewardText(q)}" + (q.RewardWeapon != null ? ". Оружие появится в наградах и магазинах, его можно взять на старт в Арсенале." : ""), 3.5f);
            return true;
        }

        /// <summary>Hub: survivors that have a quest to give (!) or to turn in (✓).</summary>
        public string QuestMark(string npc)
        {
            var q = Meta.QuestOf(npc); if (q == null) return null;
            return Meta.QReady(q) ? "ready" : Meta.QTaken(q.Id) ? "active" : "new";
        }

        // ================= overload =================
        /// <summary>Overload level of the current run (chosen in the hub, fixed at the start of a run).</summary>
        public int Ol { get; private set; }
        float OlHp => 1f + 0.12f * Ol;

        public bool SetOverload(int lvl) { if (lvl < 0 || lvl > Mathf.Min(5, Meta.OlMax)) return false; Meta.Ol = lvl; SaveMeta(); return true; }

        // ================= Mk upgrades =================
        public int MkPrice(string id)
        {
            int l = Meta.Mk(id); if (l >= BomberMetaData.MkCost.Length) return -1;
            return Mathf.RoundToInt(BomberMetaData.MkCost[l] * (Meta.IsRescued("mechanic") ? 0.8f : 1f));
        }

        public bool BuyMk(string id)
        {
            int p = MkPrice(id); if (p < 0 || !Meta.WeaponUnlocked(id) || Meta.Data < p) return false;
            Meta.AddMk(id); return Spend(p);
        }
    }
}
