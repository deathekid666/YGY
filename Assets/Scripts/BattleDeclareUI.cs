using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Lets you pick a specific attacker (from the current turn player's
    /// monsters) and a specific defender (from the opponent's monsters, or
    /// "direct attack" for none), then calls the real
    /// TurnManager.DeclareAttack() battle resolution. Refreshes its options
    /// automatically whenever the Battle phase is entered.
    /// </summary>
    public class BattleDeclareUI : MonoBehaviour
    {
        public OptionCycler attackerCycler;
        public OptionCycler defenderCycler;

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }

        private void Start()
        {
            RefreshOptions();
        }

        private void HandlePhaseChanged(TurnPhase phase)
        {
            if (phase == TurnPhase.Battle)
                RefreshOptions();
        }

        public void RefreshOptions()
        {
            if (TurnManager.Instance == null || MonsterFieldState.Instance == null) return;

            var side = TurnManager.Instance.CurrentTurnPlayer;

            var mine = MonsterFieldState.Instance.GetAllMonsters(side).ToArray();
            if (mine.Length == 0)
            {
                attackerCycler.SetOptions(new[] { "(no monsters)" }, new string[] { null });
            }
            else
            {
                var labels = mine.Select(m => $"{m.zoneName} ({CardDatabase.Get(m.cardId)?.displayName})").ToArray();
                var values = mine.Select(m => m.zoneName).ToArray();
                attackerCycler.SetOptions(labels, values);
            }

            var theirs = MonsterFieldState.Instance.GetAllMonsters(side.Opposite()).ToArray();
            var defenderLabels = new List<string> { "(direct attack)" };
            var defenderValues = new List<string> { null };
            foreach (var m in theirs)
            {
                defenderLabels.Add($"{m.zoneName} ({CardDatabase.Get(m.cardId)?.displayName})");
                defenderValues.Add(m.zoneName);
            }
            defenderCycler.SetOptions(defenderLabels.ToArray(), defenderValues.ToArray());
        }

        public void DeclareAttack()
        {
            var attackerZone = attackerCycler.Current;
            if (string.IsNullOrEmpty(attackerZone))
            {
                Debug.LogWarning("[BattleDeclareUI] No attacker available.");
                return;
            }

            var defenderZone = defenderCycler.Current; // null means direct attack
            TurnManager.Instance.DeclareAttack(attackerZone, defenderZone);
            RefreshOptions(); // whatever was destroyed should drop out of the lists
        }
    }
}
