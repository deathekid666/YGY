using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Minimal UI companion to TargetSelectionManager. Shows one button per
    /// valid target id, and calls ConfirmTarget(id) when clicked.
    ///
    /// Wire up once: Canvas -> Panel "TargetSelectPanel" (start inactive),
    /// a Button prefab with a child Text, and a container Transform for
    /// spawned buttons. Attach this script and hook Show()/Hide() to
    /// TargetSelectionManager's request lifecycle (see ShowFor below).
    /// </summary>
    public class TargetSelectionUI : MonoBehaviour
    {
        public GameObject panelRoot;
        public Button buttonPrefab;
        public Transform buttonContainer;

        private readonly List<GameObject> spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>
        /// Call this right after TargetSelectionManager.RequestTarget so the
        /// player sees buttons for each valid id. Each button click both
        /// confirms the selection on the manager and hides this panel.
        /// Labels are resolved from CardDatabase, so this is for targets
        /// that ARE card ids (e.g. Monster Reborn's graveyard targets).
        /// </summary>
        public void ShowFor(string[] validCardIds)
        {
            var labels = new string[validCardIds.Length];
            for (int i = 0; i < validCardIds.Length; i++)
                labels[i] = CardDatabase.Get(validCardIds[i])?.displayName ?? validCardIds[i];

            ShowForCustom(validCardIds, labels);
        }

        /// <summary>
        /// Like ShowFor, but for targets that aren't card ids — e.g. zone
        /// names, where the label needs to be resolved by the caller (so two
        /// duplicate cards in different zones show as distinct options
        /// instead of colliding into one).
        /// </summary>
        public void ShowForCustom(string[] values, string[] labels)
        {
            ClearButtons();

            for (int i = 0; i < values.Length; i++)
            {
                var btn = Instantiate(buttonPrefab, buttonContainer);
                btn.GetComponentInChildren<Text>().text = labels[i];
                string capturedValue = values[i];
                btn.onClick.AddListener(() => OnTargetChosen(capturedValue));
                spawnedButtons.Add(btn.gameObject);
            }

            if (panelRoot != null)
                panelRoot.SetActive(true);
        }

        private void OnTargetChosen(string cardId)
        {
            TargetSelectionManager.Instance.ConfirmTarget(cardId);
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
