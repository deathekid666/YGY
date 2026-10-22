using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Drop this on a Button in your test scene labeled "Declare Attack."
    /// Lets you test the trap set -> trigger flow before real turn/battle
    /// logic exists. Hook Button.onClick -> DeclareAttack() in the Inspector,
    /// or just attach this script and it wires itself via RequireComponent.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AttackDeclareButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(DeclareAttack);
        }

        public void DeclareAttack()
        {
            Debug.Log("[AttackDeclareButton] Attack declared (test trigger).");
            GameEvents.DeclareAttack();
        }
    }
}
