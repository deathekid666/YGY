using System;
using UnityEngine;

namespace YugiohAR
{
    /// <summary>
    /// Handles "this spell needs the player to pick a target" without a full
    /// UI system. Call RequestTarget() with the list of valid card ids and a
    /// callback; wire a UI button per candidate card to call ConfirmTarget(id).
    /// Replace this with a real tap-to-select-in-AR-space flow later —
    /// the important part is the request/confirm contract other scripts use.
    /// </summary>
    public class TargetSelectionManager : MonoBehaviour
    {
        public static TargetSelectionManager Instance { get; private set; }

        private Action<string> pendingCallback;
        public bool IsAwaitingSelection => pendingCallback != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RequestTarget(string[] validCardIds, Action<string> onSelected)
        {
            if (validCardIds == null || validCardIds.Length == 0)
            {
                Debug.LogWarning("[TargetSelectionManager] No valid targets available.");
                return;
            }

            pendingCallback = onSelected;
            Debug.Log($"[TargetSelectionManager] Awaiting target selection from: {string.Join(", ", validCardIds)}");
            // TODO: show UI buttons/highlight for each id in validCardIds.
        }

        public void ConfirmTarget(string cardId)
        {
            if (pendingCallback == null)
            {
                Debug.LogWarning("[TargetSelectionManager] ConfirmTarget called with no pending request.");
                return;
            }

            var callback = pendingCallback;
            pendingCallback = null;
            callback.Invoke(cardId);
        }
    }
}
