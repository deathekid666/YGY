using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// A minimal "< Label >" selector: two buttons cycle through a list,
    /// a Text shows the current choice. Used instead of Unity's built-in
    /// Dropdown because Dropdown's template hierarchy is painful to build
    /// from code — this does the same job for a short list with far less code.
    ///
    /// Values and labels are separate arrays so you can show a friendly name
    /// ("Dark Magician") while Current returns the underlying id
    /// ("dark_magician").
    /// </summary>
    public class OptionCycler : MonoBehaviour
    {
        public Text label;
        public Button prevButton;
        public Button nextButton;

        private string[] labels;
        private string[] values;
        private int index;

        public string Current => (values != null && values.Length > 0) ? values[index] : null;

        private void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(string[] displayLabels, string[] underlyingValues)
        {
            labels = displayLabels;
            values = underlyingValues;
            index = 0;
            Refresh();
        }

        private void Step(int delta)
        {
            if (values == null || values.Length == 0) return;
            index = (index + delta + values.Length) % values.Length;
            Refresh();
        }

        private void Refresh()
        {
            if (label != null)
                label.text = (labels != null && labels.Length > 0) ? labels[index] : "-";
        }
    }
}
