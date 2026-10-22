using UnityEngine;
using System.Linq;

namespace YugiohAR
{
    public enum ZoneType
    {
        Monster,
        SpellTrap,
        Graveyard,
        None
    }

    /// <summary>
    /// Attach this to a flat empty GameObject (a BoxCollider trigger volume)
    /// placed over your playmat/marker in AR space. One per zone, one owner
    /// per zone — this is what makes two-player play possible: a monster
    /// placed in a zone owned by PlayerSide.Opponent is the opponent's
    /// monster, full stop, no separate ownership bookkeeping needed anywhere
    /// else. Give each zone a unique name (e.g. "Player_Monster_1",
    /// "Opponent_SpellTrap_3"...).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FieldZone : MonoBehaviour
    {
        public string zoneName;
        public ZoneType zoneType;
        public PlayerSide owner = PlayerSide.Player;

        private void Reset()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// Central place to ask "which zone is this card currently in, and whose
    /// is it?" — used by GameFieldManager, CardEffectRouter, MonsterFieldState
    /// and TurnManager so effects and battles can react based on zone type
    /// AND ownership (e.g. Mirror Force only hits the opponent's monsters;
    /// you can only declare an attack with your own monster).
    /// </summary>
    public class FieldZoneManager : MonoBehaviour
    {
        public static FieldZoneManager Instance { get; private set; }

        [Tooltip("Drag every FieldZone in your scene here")]
        public FieldZone[] zones;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Returns the zone whose collider currently contains the given world position,
        /// or a None zone if the card isn't over any defined zone yet.
        /// </summary>
        public (ZoneType type, string zoneName) GetZoneForPosition(Vector3 worldPos)
        {
            foreach (var zone in zones)
            {
                var col = zone.GetComponent<BoxCollider>();
                if (col.bounds.Contains(worldPos))
                    return (zone.zoneType, zone.zoneName);
            }
            return (ZoneType.None, null);
        }

        /// <summary>Returns the Transform of a zone by name, or null if not found.</summary>
        public Transform GetZoneTransform(string zoneName)
        {
            var zone = zones.FirstOrDefault(z => z.zoneName == zoneName);
            return zone != null ? zone.transform : null;
        }

        /// <summary>Returns which player owns the given zone. Defaults to Player if the zone isn't found.</summary>
        public PlayerSide GetZoneOwner(string zoneName)
        {
            var zone = zones.FirstOrDefault(z => z.zoneName == zoneName);
            return zone != null ? zone.owner : PlayerSide.Player;
        }

        /// <summary>All zone names of a given type belonging to a given side.</summary>
        public string[] GetZoneNames(PlayerSide side, ZoneType type)
        {
            return zones.Where(z => z.owner == side && z.zoneType == type)
                        .Select(z => z.zoneName)
                        .ToArray();
        }
    }
}
