using UnityEngine;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Drop on a Button labeled with a zone name (e.g. "Scan Monster_1").
    /// Set zoneName in the Inspector to match a FieldZone's zoneName.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ZoneScanButton : MonoBehaviour
    {
        public string zoneName;
        public CardVisionScanner scanner;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Scan);
        }

        public void Scan()
        {
            if (scanner == null)
            {
                Debug.LogWarning("[ZoneScanButton] No CardVisionScanner assigned.");
                return;
            }
            scanner.ScanZone(zoneName);
        }
    }
}
