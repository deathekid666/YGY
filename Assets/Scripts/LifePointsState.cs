using System;
using UnityEngine;

namespace YugiohAR
{
    public enum PlayerSide { Player, Opponent }

    public static class PlayerSideExtensions
    {
        /// <summary>The other player. Used constantly for "whose turn is it, and who's the opponent."</summary>
        public static PlayerSide Opposite(this PlayerSide side) =>
            side == PlayerSide.Player ? PlayerSide.Opponent : PlayerSide.Player;
    }

    /// <summary>
    /// Simple two-player life point tracker. Raises OnLifeChanged so a UI
    /// (LifePointsUI) can refresh without polling, and OnGameOver once a
    /// side hits zero.
    /// </summary>
    public class LifePointsState : MonoBehaviour
    {
        public static LifePointsState Instance { get; private set; }

        public int startingLifePoints = 8000;

        public int PlayerLP { get; private set; }
        public int OpponentLP { get; private set; }

        public event Action<PlayerSide, int> OnLifeChanged;
        public event Action<PlayerSide> OnGameOver;

        private bool gameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            PlayerLP = startingLifePoints;
            OpponentLP = startingLifePoints;
        }

        public void DealDamage(PlayerSide side, int amount)
        {
            if (gameOver || amount <= 0) return;

            if (side == PlayerSide.Player)
                PlayerLP = Mathf.Max(0, PlayerLP - amount);
            else
                OpponentLP = Mathf.Max(0, OpponentLP - amount);

            Debug.Log($"[LifePointsState] {side} takes {amount} damage. " +
                      $"Player: {PlayerLP}, Opponent: {OpponentLP}");

            OnLifeChanged?.Invoke(side, side == PlayerSide.Player ? PlayerLP : OpponentLP);

            if ((side == PlayerSide.Player && PlayerLP == 0) ||
                (side == PlayerSide.Opponent && OpponentLP == 0))
            {
                gameOver = true;
                var winner = side == PlayerSide.Player ? PlayerSide.Opponent : PlayerSide.Player;
                Debug.Log($"[LifePointsState] Game over — {winner} wins.");
                OnGameOver?.Invoke(winner);
            }
        }
    }
}
