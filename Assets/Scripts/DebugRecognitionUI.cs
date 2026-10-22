using System.Linq;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Simulates exactly what CardVisionScanner would report — "this card
    /// (or a card-back, or nothing) is in this zone" — without a camera or
    /// an API call. Wired to GameFieldManager the same way real recognition
    /// is, so anything you test here behaves identically once you switch to
    /// real scanning. Build and test the whole game for free; add the API
    /// key only once you're happy with how everything behaves.
    /// </summary>
    public class DebugRecognitionUI : MonoBehaviour
    {
        public OptionCycler zoneCycler;
        public OptionCycler cardCycler;

        private void Start()
        {
            if (FieldZoneManager.Instance != null)
            {
                var zoneNames = FieldZoneManager.Instance.zones
                    .Where(z => z.zoneType != ZoneType.Graveyard)
                    .Select(z => z.zoneName)
                    .ToArray();
                zoneCycler.SetOptions(zoneNames, zoneNames);
            }

            var cardLabels = CardDatabase.Cards.Values.Select(c => c.displayName).ToArray();
            var cardIds = CardDatabase.Cards.Values.Select(c => c.id).ToArray();
            cardCycler.SetOptions(cardLabels, cardIds);
        }

        /// <summary>Simulates a successful scan of the currently selected card in the currently selected zone.</summary>
        public void SimulateScan()
        {
            var zoneTransform = GetSelectedZoneTransform();
            if (zoneTransform == null) return;

            GameFieldManager.Instance.OnCardRecognized(cardCycler.Current, zoneTransform);
        }

        /// <summary>Simulates scanning an unidentified card-back in the selected zone — same as CardVisionScanner would report for a set trap/monster.</summary>
        public void SimulateSetFaceDown()
        {
            var zoneTransform = GetSelectedZoneTransform();
            if (zoneTransform == null) return;

            GameFieldManager.Instance.OnFaceDownCardPlaced(zoneTransform);
        }

        /// <summary>Simulates the camera losing sight of whatever is in the selected zone (picking the card up, or it moving out of frame).</summary>
        public void SimulateRemove()
        {
            var zoneName = zoneCycler.Current;
            var zoneTransform = GetSelectedZoneTransform();
            if (zoneTransform == null) return;

            var cardId = MonsterFieldState.Instance != null ? MonsterFieldState.Instance.GetCardAt(zoneName) : null;
            if (cardId != null)
                GameFieldManager.Instance.OnCardLost(cardId);
            else
                GameFieldManager.Instance.OnFaceDownCardRemoved(zoneTransform);
        }

        private Transform GetSelectedZoneTransform()
        {
            var zoneName = zoneCycler.Current;
            if (zoneName == null)
            {
                Debug.LogWarning("[DebugRecognitionUI] No zone selected.");
                return null;
            }

            var t = FieldZoneManager.Instance.GetZoneTransform(zoneName);
            if (t == null)
                Debug.LogWarning($"[DebugRecognitionUI] Zone '{zoneName}' has no Transform.");
            return t;
        }
    }
}
