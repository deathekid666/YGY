using System;
using System.Collections.Generic;
using UnityEngine;

namespace YugiohAR
{
    public enum TurnPhase { Draw, Standby, Main1, Battle, Main2, End }

    /// <summary>
    /// Minimal turn structure: phases cycle in order, draw happens
    /// automatically each Draw phase, and DeclareAttack() resolves actual
    /// battle math (ATK vs ATK/DEF, life point damage, destruction) using
    /// MonsterFieldState + LifePointsState. This is what turns the prototype
    /// from "cards react individually" into "an actual game with a winner."
    ///
    /// Two-player aware: DeclareAttack validates that the attacker actually
    /// belongs to the current turn player and the defender (if any) belongs
    /// to the opponent, via FieldZoneManager's per-zone ownership.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Draw;
        public PlayerSide CurrentTurnPlayer { get; private set; } = PlayerSide.Player;

        public event Action<TurnPhase> OnPhaseChanged;
        public event Action<PlayerSide> OnTurnChanged;

        private readonly Dictionary<PlayerSide, bool> normalSummonUsedThisTurn = new Dictionary<PlayerSide, bool>
        {
            { PlayerSide.Player, false },
            { PlayerSide.Opponent, false }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            EnterPhase(TurnPhase.Draw);
        }

        public void NextPhase()
        {
            switch (CurrentPhase)
            {
                case TurnPhase.Draw:    EnterPhase(TurnPhase.Standby); break;
                case TurnPhase.Standby: EnterPhase(TurnPhase.Main1);   break;
                case TurnPhase.Main1:   EnterPhase(TurnPhase.Battle);  break;
                case TurnPhase.Battle:  EnterPhase(TurnPhase.Main2);   break;
                case TurnPhase.Main2:   EnterPhase(TurnPhase.End);     break;
                case TurnPhase.End:     EndTurn(); break;
            }
        }

        private void EndTurn()
        {
            CurrentTurnPlayer = CurrentTurnPlayer.Opposite();
            Debug.Log($"[TurnManager] Turn passes to {CurrentTurnPlayer}.");
            OnTurnChanged?.Invoke(CurrentTurnPlayer);
            EnterPhase(TurnPhase.Draw);
        }

        private void EnterPhase(TurnPhase phase)
        {
            CurrentPhase = phase;
            Debug.Log($"[TurnManager] --- {CurrentTurnPlayer}'s {phase} phase ---");
            OnPhaseChanged?.Invoke(phase);

            if (phase == TurnPhase.Draw && DeckState.Instance != null)
            {
                DeckState.Instance.Draw(CurrentTurnPlayer, 1);
                normalSummonUsedThisTurn[CurrentTurnPlayer] = false;
            }
        }

        /// <summary>
        /// Whether the given side may Normal Summon right now: it must be
        /// their turn, during one of their Main Phases, and they haven't
        /// already used their one Normal Summon this turn.
        /// </summary>
        public bool CanNormalSummon(PlayerSide side)
        {
            bool correctTurn = side == CurrentTurnPlayer;
            bool correctPhase = CurrentPhase == TurnPhase.Main1 || CurrentPhase == TurnPhase.Main2;
            bool notYetUsed = !normalSummonUsedThisTurn[side];
            return correctTurn && correctPhase && notYetUsed;
        }

        /// <summary>Call after a successful Normal Summon to consume this turn's one allowance.</summary>
        public void MarkNormalSummonUsed(PlayerSide side)
        {
            normalSummonUsedThisTurn[side] = true;
        }

        /// <summary>
        /// Declares an attack from a monster in attackerZoneName. Pass a
        /// defenderZoneName to attack a specific monster, or leave it null
        /// for a direct attack on the opposing player's life points.
        /// The attacker must belong to the current turn player; the
        /// defender (if given) must belong to the opponent.
        /// </summary>
        public void DeclareAttack(string attackerZoneName, string defenderZoneName = null)
        {
            if (CurrentPhase != TurnPhase.Battle)
            {
                Debug.LogWarning("[TurnManager] Can only declare an attack during the Battle phase.");
                return;
            }

            if (FieldZoneManager.Instance.GetZoneOwner(attackerZoneName) != CurrentTurnPlayer)
            {
                Debug.LogWarning($"[TurnManager] {attackerZoneName} doesn't belong to {CurrentTurnPlayer} — can't attack with it.");
                return;
            }

            if (defenderZoneName != null &&
                FieldZoneManager.Instance.GetZoneOwner(defenderZoneName) != CurrentTurnPlayer.Opposite())
            {
                Debug.LogWarning($"[TurnManager] {defenderZoneName} isn't the opponent's zone — invalid attack target.");
                return;
            }

            var attackerId = MonsterFieldState.Instance.GetCardAt(attackerZoneName);
            if (attackerId == null)
            {
                Debug.LogWarning($"[TurnManager] No monster in {attackerZoneName} to attack with.");
                return;
            }

            // Give any armed traps a chance to respond before damage is calculated.
            GameEvents.DeclareAttack();

            // The attacker may have just been destroyed by a trap (e.g. Trap Hole).
            attackerId = MonsterFieldState.Instance.GetCardAt(attackerZoneName);
            if (attackerId == null)
            {
                Debug.Log("[TurnManager] Attacker was destroyed in response — attack cancelled.");
                return;
            }

            var attackerCard = CardDatabase.Get(attackerId);
            var defendingSide = CurrentTurnPlayer.Opposite();

            var defenderId = defenderZoneName != null ? MonsterFieldState.Instance.GetCardAt(defenderZoneName) : null;

            if (defenderId == null)
            {
                Debug.Log($"[TurnManager] {attackerCard.displayName} attacks directly for {attackerCard.atk}.");
                LifePointsState.Instance.DealDamage(defendingSide, attackerCard.atk);
                return;
            }

            var defenderCard = CardDatabase.Get(defenderId);
            var defenderPosition = MonsterFieldState.Instance.GetPosition(defenderZoneName);

            if (defenderPosition == BattlePosition.Attack)
            {
                ResolveAttackVsAttack(attackerCard, attackerZoneName, defenderCard, defenderZoneName, defendingSide);
            }
            else
            {
                ResolveAttackVsDefense(attackerCard, defenderCard, defenderZoneName, defendingSide);
            }
        }

        private void ResolveAttackVsAttack(CardData attacker, string attackerZone,
            CardData defender, string defenderZone, PlayerSide defendingSide)
        {
            var attackingSide = defendingSide.Opposite();

            if (attacker.atk > defender.atk)
            {
                Debug.Log($"[TurnManager] {attacker.displayName} ({attacker.atk}) destroys {defender.displayName} ({defender.atk}).");
                MonsterFieldState.Instance.DestroyZone(defenderZone);
                LifePointsState.Instance.DealDamage(defendingSide, attacker.atk - defender.atk);
            }
            else if (attacker.atk == defender.atk)
            {
                Debug.Log($"[TurnManager] {attacker.displayName} and {defender.displayName} trade — both destroyed.");
                MonsterFieldState.Instance.DestroyZone(attackerZone);
                MonsterFieldState.Instance.DestroyZone(defenderZone);
            }
            else
            {
                Debug.Log($"[TurnManager] {defender.displayName} ({defender.atk}) destroys {attacker.displayName} ({attacker.atk}).");
                MonsterFieldState.Instance.DestroyZone(attackerZone);
                LifePointsState.Instance.DealDamage(attackingSide, defender.atk - attacker.atk);
            }
        }

        private void ResolveAttackVsDefense(CardData attacker, CardData defender, string defenderZone, PlayerSide defendingSide)
        {
            var attackingSide = defendingSide.Opposite();

            if (attacker.atk > defender.def)
            {
                Debug.Log($"[TurnManager] {attacker.displayName} ({attacker.atk}) destroys defense-position {defender.displayName} ({defender.def}).");
                MonsterFieldState.Instance.DestroyZone(defenderZone);
            }
            else if (attacker.atk == defender.def)
            {
                Debug.Log($"[TurnManager] {attacker.displayName} bounces off {defender.displayName} — no destruction.");
            }
            else
            {
                Debug.Log($"[TurnManager] {attacker.displayName} ({attacker.atk}) fails against {defender.displayName}'s defense " +
                          $"({defender.def}) — {attackingSide} takes {defender.def - attacker.atk} damage.");
                LifePointsState.Instance.DealDamage(attackingSide, defender.def - attacker.atk);
            }
        }
    }
}
