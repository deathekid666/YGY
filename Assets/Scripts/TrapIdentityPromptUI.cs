using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Minimal UI: when a card is set face-down in a Spell/Trap zone, this
    /// panel pops up asking "which trap did you set?" with one button per
    /// trap card in CardDatabase. Picking one calls CardEffectRouter.ArmKnownTrap.
    ///
    /// Wire this up once in your scene:
    ///  1. Create a Canvas -> Panel called "TrapIdentityPanel", start inactive.
    ///  2. Add one Button per trap card under it (or let this script spawn
    ///     them at runtime from buttonPrefab — see below).
    ///  3. Attach this script to the panel, drag CardEffectRouter in.
    /// </summary>
    public class TrapIdentityPromptUI : MonoBehaviour
    {
        public GameObject panelRoot;
        public Button buttonPrefab;
        public Transform buttonContainer;
        public CardEffectRouter effectRouter;

        private string pendingZoneName;
        private Transform pendingTransform;
        private readonly List<GameObject> spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>
        /// Call this from GameFieldManager/CardEffectRouter when a face-down
        /// card appears in a Spell/Trap zone and its identity needs confirming.
        /// </summary>
        public void Show(string zoneName, Transform cardTransform)
        {
            pendingZoneName = zoneName;
            pendingTransform = cardTransform;

            ClearButtons();

            var trapCards = CardDatabase.Cards.Values
                .Where(c => c.effect == EffectCategory.TrapSetTrigger)
                .ToList();

            foreach (var trap in trapCards)
            {
                var btn = Instantiate(buttonPrefab, buttonContainer);
                btn.GetComponentInChildren<Text>().text = trap.displayName;
                string capturedId = trap.id; // avoid closure-over-loop-variable bug
                btn.onClick.AddListener(() => OnTrapChosen(capturedId));
                spawnedButtons.Add(btn.gameObject);
            }

            if (panelRoot != null)
                panelRoot.SetActive(true);
        }

        private void OnTrapChosen(string cardId)
        {
            effectRouter.ArmKnownTrap(cardId, pendingZoneName, pendingTransform);
            Hide();
        }

        private void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            ClearButtons();
        }

        private void ClearButtons()
        {
            foreach (var b in spawnedButtons)
                Destroy(b);
            spawnedButtons.Clear();
        }
    }
}
