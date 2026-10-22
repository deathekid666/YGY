using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Drop this on a UI GameObject with two Text children assigned below.
    /// Subscribes to LifePointsState so the numbers update the instant
    /// damage is dealt, no polling needed.
    /// </summary>
    public class LifePointsUI : MonoBehaviour
    {
        public Text playerLifeText;
        public Text opponentLifeText;

        private void OnEnable()
        {
            if (LifePointsState.Instance != null)
            {
                LifePointsState.Instance.OnLifeChanged += HandleLifeChanged;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (LifePointsState.Instance != null)
                LifePointsState.Instance.OnLifeChanged -= HandleLifeChanged;
        }

        private void HandleLifeChanged(PlayerSide side, int newValue) => Refresh();

        private void Refresh()
        {
            if (playerLifeText != null)
                playerLifeText.text = $"Player: {LifePointsState.Instance.PlayerLP}";
            if (opponentLifeText != null)
                opponentLifeText.text = $"Opponent: {LifePointsState.Instance.OpponentLP}";
        }
    }
}
