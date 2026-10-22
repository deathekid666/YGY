using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// The actual "board state" for monsters — which card id sits in which
    /// Monster zone, for BOTH players. Ownership comes entirely from the
    /// zone itself (via FieldZoneManager) — a monster in a zone owned by
    /// PlayerSide.Opponent is the opponent's monster. Effects (flip destroy,
    /// Mirror Force, Trap Hole, Monster Reborn) all read/write through here.
    /// </summary>
    public class MonsterFieldState : MonoBehaviour
    {
        public static MonsterFieldState Instance { get; private set; }

        private readonly Dictionary<string, string> zoneToCardId = new Dictionary<string, string>();
        private readonly Dictionary<string, BattlePosition> zoneToPosition = new Dictionary<string, BattlePosition>();
        private readonly Dictionary<PlayerSide, string> lastSummonedZoneBySide = new Dictionary<PlayerSide, string>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private PlayerSide OwnerOf(string zoneName) =>
            FieldZoneManager.Instance != null ? FieldZoneManager.Instance.GetZoneOwner(zoneName) : PlayerSide.Player;

        public void PlaceMonster(string zoneName, string cardId, BattlePosition position = BattlePosition.Attack)
        {
            zoneToCardId[zoneName] = cardId;
            zoneToPosition[zoneName] = position;
            lastSummonedZoneBySide[OwnerOf(zoneName)] = zoneName;
            Debug.Log($"[MonsterFieldState] {CardDatabase.Get(cardId)?.displayName} placed in {zoneName} ({position}, {OwnerOf(zoneName)}).");
        }

        public string GetCardAt(string zoneName) =>
            zoneToCardId.TryGetValue(zoneName, out var id) ? id : null;

        public BattlePosition GetPosition(string zoneName) =>
            zoneToPosition.TryGetValue(zoneName, out var pos) ? pos : BattlePosition.Attack;

        public void SetPosition(string zoneName, BattlePosition position)
        {
            if (zoneToCardId.ContainsKey(zoneName))
                zoneToPosition[zoneName] = position;
        }

        /// <summary>All (zoneName, cardId) pairs currently occupied, for both players.</summary>
        public IEnumerable<(string zoneName, string cardId)> GetAllMonsters() =>
            zoneToCardId.Select(kvp => (kvp.Key, kvp.Value));

        /// <summary>All (zoneName, cardId) pairs currently occupied that belong to the given side.</summary>
        public IEnumerable<(string zoneName, string cardId)> GetAllMonsters(PlayerSide side) =>
            zoneToCardId.Where(kvp => OwnerOf(kvp.Key) == side).Select(kvp => (kvp.Key, kvp.Value));

        /// <summary>Destroys whichever monster is in the given zone, if any. Returns the destroyed card id's owner side and zone, or null.</summary>
        public string DestroyZone(string zoneName)
        {
            if (!zoneToCardId.TryGetValue(zoneName, out var cardId))
                return null;

            var owner = OwnerOf(zoneName);
            zoneToCardId.Remove(zoneName);
            zoneToPosition.Remove(zoneName);
            GraveyardState.Instance.Add(owner, cardId);
            Debug.Log($"[MonsterFieldState] Destroyed {CardDatabase.Get(cardId)?.displayName} in {zoneName} ({owner}'s graveyard).");
            return cardId;
        }

        /// <summary>Finds and destroys a specific card by id, wherever it is on the field. Returns the zone it was destroyed from, or null if not found.</summary>
        public string DestroyByCardId(string cardId)
        {
            var zone = zoneToCardId.FirstOrDefault(kvp => kvp.Value == cardId).Key;
            if (zone == null) return null;
            DestroyZone(zone);
            return zone;
        }

        /// <summary>Destroys every monster currently controlled by the given side.</summary>
        public void DestroyAll(PlayerSide side)
        {
            foreach (var zoneName in zoneToCardId.Keys.Where(z => OwnerOf(z) == side).ToList())
                DestroyZone(zoneName);
        }

        /// <summary>Destroys the monster from the most recent PlaceMonster call for the given side (Trap Hole).</summary>
        public void DestroyLastSummoned(PlayerSide side)
        {
            if (lastSummonedZoneBySide.TryGetValue(side, out var zoneName))
                DestroyZone(zoneName);
        }

        /// <summary>
        /// Puts a revived card (e.g. from Monster Reborn) into the first empty
        /// Monster zone belonging to the given side. Returns the zone name it
        /// landed in, or null if that side's field is full. Note: this only
        /// updates game STATE — the player still needs to physically place
        /// the real card there for the camera to eventually recognize it.
        /// </summary>
        public string ReviveToFirstEmptyZone(string cardId, PlayerSide side)
        {
            if (FieldZoneManager.Instance == null) return null;

            var emptyMonsterZoneName = FieldZoneManager.Instance
                .GetZoneNames(side, ZoneType.Monster)
                .FirstOrDefault(z => !zoneToCardId.ContainsKey(z));

            if (emptyMonsterZoneName == null)
            {
                Debug.LogWarning($"[MonsterFieldState] No empty Monster zone for {side} to revive into.");
                return null;
            }

            PlaceMonster(emptyMonsterZoneName, cardId);
            return emptyMonsterZoneName;
        }
    }
}
