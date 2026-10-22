using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Drop this on a Button labeled "Next Phase." Advances TurnManager
    /// through Draw -> Standby -> Main1 -> Battle -> Main2 -> End -> (next turn).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class NextPhaseButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Advance);
        }

        public void Advance()
        {
            TurnManager.Instance.NextPhase();
        }
    }
}
