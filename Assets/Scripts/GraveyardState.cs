using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Each player has their own graveyard. Monster Reborn can target a card
    /// in either graveyard, but destroy effects always add to the
    /// destroyed monster's OWN controller's graveyard.
    /// </summary>
    public class GraveyardState : MonoBehaviour
    {
        public static GraveyardState Instance { get; private set; }

        private readonly Dictionary<PlayerSide, List<string>> piles = new Dictionary<PlayerSide, List<string>>
        {
            { PlayerSide.Player, new List<string>() },
            { PlayerSide.Opponent, new List<string>() }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Add(PlayerSide side, string cardId)
        {
            piles[side].Add(cardId);
            Debug.Log($"[GraveyardState] Added to {side}'s graveyard: {CardDatabase.Get(cardId)?.displayName ?? cardId}");
        }

        public bool Remove(PlayerSide side, string cardId)
        {
            bool removed = piles[side].Remove(cardId);
            if (removed)
                Debug.Log($"[GraveyardState] Removed from {side}'s graveyard: {CardDatabase.Get(cardId)?.displayName ?? cardId}");
            return removed;
        }

        public string[] GetAll(PlayerSide side) => piles[side].ToArray();

        /// <summary>Both graveyards combined, each tagged with its side — used by effects like Monster Reborn that can target either.</summary>
        public IEnumerable<(PlayerSide side, string cardId)> GetAllBothSides() =>
            piles.SelectMany(kvp => kvp.Value.Select(id => (kvp.Key, id)));

        /// <summary>Finds which side's graveyard currently holds this card id, if any.</summary>
        public PlayerSide? FindOwner(string cardId)
        {
            foreach (var kvp in piles)
                if (kvp.Value.Contains(cardId))
                    return kvp.Key;
            return null;
        }
    }
}
