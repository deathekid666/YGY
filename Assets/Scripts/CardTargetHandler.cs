using UnityEngine;
using Vuforia;

namespace YugiohAR
{
    /// <summary>
    /// Attach this to every Vuforia Image Target GameObject.
    /// Set `cardId` in the Inspector to match an id in CardDatabase.
    ///
    /// This script only talks about Vuforia tracking states (found/lost) —
    /// it deliberately knows nothing about summon rules or animations.
    /// GameFieldManager listens to its events and decides what happens next.
    /// </summary>
    [RequireComponent(typeof(ObserverBehaviour))]
    public class CardTargetHandler : MonoBehaviour
    {
        [Tooltip("Must match an id key in CardDatabase.Cards")]
        public string cardId;

        private ObserverBehaviour observer;
        private bool isCurrentlyTracked;

        private void Awake()
        {
            observer = GetComponent<ObserverBehaviour>();
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }

        private void OnDestroy()
        {
            if (observer != null)
                observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        }

        private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
        {
            bool nowTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

            if (nowTracked && !isCurrentlyTracked)
            {
                isCurrentlyTracked = true;
                GameFieldManager.Instance.OnCardRecognized(cardId, transform);
            }
            else if (!nowTracked && isCurrentlyTracked)
            {
                isCurrentlyTracked = false;
                GameFieldManager.Instance.OnCardLost(cardId);
            }
        }
    }
}
