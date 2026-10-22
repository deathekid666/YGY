using UnityEngine;
using Vuforia;

namespace YugiohAR
{
    /// <summary>
    /// Attach to a Vuforia Image Target trained on your card BACK design
    /// (the plain Yu-Gi-Oh card-back pattern, same for every card).
    /// This is what lets the game know "something was set face-down here"
    /// without knowing which card it is yet — required to detect a real
    /// flip later, instead of just "a card became visible."
    /// </summary>
    [RequireComponent(typeof(ObserverBehaviour))]
    public class FaceDownMarkerHandler : MonoBehaviour
    {
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
                GameFieldManager.Instance.OnFaceDownCardPlaced(transform);
            }
            else if (!nowTracked && isCurrentlyTracked)
            {
                isCurrentlyTracked = false;
                GameFieldManager.Instance.OnFaceDownCardRemoved(transform);
            }
        }
    }
}
