using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

namespace YugiohAR
{
    /// <summary>
    /// Shows the phone's rear camera as a full-screen live background via a
    /// RawImage, and exposes the live WebCamTexture for CardVisionScanner to
    /// capture frames from. This is the actual "camera view" the whole app
    /// is supposed to be built around — without this, there's nothing real
    /// to scan.
    /// </summary>
    public class CameraFeedBackground : MonoBehaviour
    {
        public static CameraFeedBackground Instance { get; private set; }

        public RawImage display;

        public WebCamTexture ActiveTexture { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                // Give the OS permission dialog a moment; a production build
                // should instead listen for the callback rather than a fixed wait.
                yield return new WaitForSeconds(1f);
            }
#endif
            yield return StartCamera();
        }

        private IEnumerator StartCamera()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[CameraFeedBackground] No camera device found.");
                yield break;
            }

            // Prefer a rear-facing camera if the device reports one.
            string deviceName = devices[0].name;
            foreach (var d in devices)
            {
                if (!d.isFrontFacing) { deviceName = d.name; break; }
            }

            ActiveTexture = new WebCamTexture(deviceName, 1280, 720, 30);
            ActiveTexture.Play();

            // Wait until the texture actually has real dimensions before using it.
            while (ActiveTexture.width <= 16)
                yield return null;

            if (display != null)
            {
                display.texture = ActiveTexture;
                FitDisplayToTexture();
            }
        }

        private void FitDisplayToTexture()
        {
            // Rotate/flip to match device orientation, and correct the aspect
            // ratio so the feed doesn't look stretched.
            var rt = display.rectTransform;
            rt.localEulerAngles = new Vector3(0, 0, -ActiveTexture.videoRotationAngle);

            float videoAspect = (float)ActiveTexture.width / ActiveTexture.height;
            display.uvRect = ActiveTexture.videoVerticallyMirrored
                ? new Rect(0, 1, 1, -1)
                : new Rect(0, 0, 1, 1);

            var aspectFitter = display.GetComponent<AspectRatioFitter>();
            if (aspectFitter != null)
                aspectFitter.aspectRatio = videoAspect;
        }

        private void OnDestroy()
        {
            if (ActiveTexture != null && ActiveTexture.isPlaying)
                ActiveTexture.Stop();
        }
    }
}
