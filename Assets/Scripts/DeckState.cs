using System.Collections.Generic;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Each player has their own deck/hand counters. Pot of Greed and the
    /// automatic Draw Phase draw both go through here, per side.
    /// </summary>
    public class DeckState : MonoBehaviour
    {
        public static DeckState Instance { get; private set; }

        [Tooltip("Cards each player's deck starts with")]
        public int startingDeckCount = 40;
        [Tooltip("Cards each player's hand starts with")]
        public int startingHandCount = 5;

        private readonly Dictionary<PlayerSide, int> deckCounts = new Dictionary<PlayerSide, int>();
        private readonly Dictionary<PlayerSide, int> handCounts = new Dictionary<PlayerSide, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (PlayerSide side in new[] { PlayerSide.Player, PlayerSide.Opponent })
            {
                deckCounts[side] = startingDeckCount;
                handCounts[side] = startingHandCount;
            }
        }

        public int GetDeckCount(PlayerSide side) => deckCounts[side];
        public int GetHandCount(PlayerSide side) => handCounts[side];

        public void Draw(PlayerSide side, int amount)
        {
            int actualDraw = Mathf.Min(amount, deckCounts[side]);
            deckCounts[side] -= actualDraw;
            handCounts[side] += actualDraw;
            Debug.Log($"[DeckState] {side} drew {actualDraw} card(s). Deck: {deckCounts[side]}, Hand: {handCounts[side]}");
        }
    }
}
