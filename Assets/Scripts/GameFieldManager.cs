using System.Collections.Generic;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Singleton that owns "what's currently on the field." Every CardTargetHandler
    /// reports to this. Put this on one empty GameObject in your scene.
    /// </summary>
    public class GameFieldManager : MonoBehaviour
    {
        public static GameFieldManager Instance { get; private set; }

        [Tooltip("Drag the CardEffectRouter component here in the Inspector")]
        public CardEffectRouter effectRouter;

        // Tracks which cards are currently visible on the field and where.
        private readonly Dictionary<string, Transform> activeCards = new Dictionary<string, Transform>();

        // Tracks which zones currently hold an unidentified face-down card,
        // so a later specific-card recognition in that same zone can be
        // detected as a genuine flip rather than a fresh placement.
        private readonly HashSet<string> faceDownZones = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private (ZoneType type, string zoneName) GetZone(Vector3 worldPos)
        {
            return FieldZoneManager.Instance != null
                ? FieldZoneManager.Instance.GetZoneForPosition(worldPos)
                : (ZoneType.None, null);
        }

        public void OnCardRecognized(string cardId, Transform cardTransform)
        {
            var data = CardDatabase.Get(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[GameFieldManager] Unknown card id '{cardId}' — check CardDatabase.");
                return;
            }

            activeCards[cardId] = cardTransform;

            var zone = GetZone(cardTransform.position);

            // If this zone currently holds an unidentified face-down card,
            // this recognition means that card was just flipped face-up.
            bool wasFlip = zone.zoneName != null && faceDownZones.Contains(zone.zoneName);
            if (wasFlip)
                faceDownZones.Remove(zone.zoneName);

            Debug.Log($"[GameFieldManager] Recognized: {data.displayName} ({data.effect}) in zone {zone.type}" +
                      (wasFlip ? " [FLIPPED from face-down]" : ""));

            effectRouter.HandleRecognition(data, cardTransform, zone.type, zone.zoneName, wasFlip);
        }

        public void OnCardLost(string cardId)
        {
            if (activeCards.ContainsKey(cardId))
            {
                activeCards.Remove(cardId);
                var data = CardDatabase.Get(cardId);
                Debug.Log($"[GameFieldManager] Lost tracking: {data?.displayName ?? cardId}");
                effectRouter.HandleCardLost(cardId);
            }
        }

        public void OnFaceDownCardPlaced(Transform markerTransform)
        {
            var zone = GetZone(markerTransform.position);
            if (zone.zoneName == null)
            {
                Debug.LogWarning("[GameFieldManager] Face-down card placed outside any known zone.");
                return;
            }

            faceDownZones.Add(zone.zoneName);
            Debug.Log($"[GameFieldManager] Card set face-down in {zone.zoneName} ({zone.type}).");
            effectRouter.HandleFaceDownSet(zone.type, zone.zoneName);
        }

        public void OnFaceDownCardRemoved(Transform markerTransform)
        {
            var zone = GetZone(markerTransform.position);
            // Only clear the flag if it wasn't already consumed by a flip
            // (OnCardRecognized removes it first in that case).
            if (zone.zoneName != null)
                faceDownZones.Remove(zone.zoneName);
        }

        public bool IsCardOnField(string cardId) => activeCards.ContainsKey(cardId);
    }
}
