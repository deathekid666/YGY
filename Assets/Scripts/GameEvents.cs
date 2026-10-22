using System;

namespace YugiohAR
{
    /// <summary>
    /// Lightweight global event bus. In the real game, "opponent declares an
    /// attack" would come from actual turn/battle logic — for the prototype,
    /// wire a UI button to call GameEvents.DeclareAttack() so you can test
    /// the trap set -> trigger flow end to end before turn logic exists.
    /// </summary>
    public static class GameEvents
    {
        public static event Action OnAttackDeclared;

        public static void DeclareAttack()
        {
            OnAttackDeclared?.Invoke();
        }
    }
}
