using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace YugiohAR
{
    /// <summary>
    /// The "easier" recognition path: instead of pre-registering each card as
    /// a Vuforia Image Target (photograph + upload + download a database
    /// before anything works), this captures the CURRENT LIVE CAMERA FRAME
    /// and asks a vision-capable AI model "which of these cards is this?"
    /// No per-card setup, no Vuforia account.
    ///
    /// Two capture modes, chosen automatically:
    ///  - If arFrameCamera is assigned (AR Foundation mode — see
    ///    ARFieldPlacer), the frame is rendered straight from that camera,
    ///    which includes the real AR passthrough video via ARCameraBackground.
    ///  - Otherwise it reads the current frame from CameraFeedBackground's
    ///    WebCamTexture (the default, non-AR path).
    ///
    /// Either way, "which zone" is simply whatever zoneName the player's
    /// tapped Scan button says it is — there's no per-zone image cropping,
    /// so point the phone at one physical card and tap the matching zone's
    /// button.
    /// </summary>
    public class CardVisionScanner : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("Your Anthropic API key. For a real build, fetch this from " +
                 "your own backend instead of shipping it in the app — anyone " +
                 "can extract a hardcoded key from a client build.")]
        public string apiKey;

        [Header("Optional: AR Foundation capture")]
        [Tooltip("Assign the AR Camera here if you've added ARFieldPlacer/AR " +
                 "Foundation. Leave empty to use the default WebCamTexture path.")]
        public Camera arFrameCamera;

        public event Action<string> OnScanFailed;

        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string Model = "claude-sonnet-4-6";

        /// <summary>
        /// Captures the current live camera frame and asks the vision model
        /// to identify what's in front of the camera right now. Calls back
        /// into GameFieldManager exactly as CardTargetHandler would.
        /// </summary>
        public void ScanZone(string zoneName)
        {
            if (arFrameCamera == null && (CameraFeedBackground.Instance == null || CameraFeedBackground.Instance.ActiveTexture == null))
            {
                Debug.LogWarning("[CardVisionScanner] No live camera feed available yet.");
                return;
            }

            StartCoroutine(ScanRoutine(zoneName));
        }

        private IEnumerator ScanRoutine(string zoneName)
        {
            var frame = arFrameCamera != null ? CaptureFromArCamera(arFrameCamera) : CaptureFromWebCamTexture();

            var jpgBytes = frame.EncodeToJPG(85);
            Destroy(frame);
            var base64Jpg = Convert.ToBase64String(jpgBytes);

            var cardIdList = string.Join(", ", CardDatabase.Cards.Keys);
            var prompt =
                $"You are identifying a physical Yu-Gi-Oh card in a photo for an AR game. " +
                $"Reply with EXACTLY ONE of the following tokens and nothing else — no punctuation, " +
                $"no explanation: one of [{cardIdList}], or \"card_back\" if you see the plain card " +
                $"back design, or \"empty\" if no card is visible.";

            yield return SendVisionRequest(base64Jpg, prompt, result =>
            {
                HandleScanResult(zoneName, result);
            });
        }

        private Texture2D CaptureFromWebCamTexture()
        {
            var tex = CameraFeedBackground.Instance.ActiveTexture;
            var frame = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
            frame.SetPixels32(tex.GetPixels32());
            frame.Apply();
            return frame;
        }

        private Texture2D CaptureFromArCamera(Camera cam)
        {
            // Render the AR camera (including its ARCameraBackground passthrough)
            // into an offscreen RenderTexture, then read it back. This works
            // without needing to touch AR Foundation's raw XRCpuImage APIs.
            int w = Screen.width;
            int h = Screen.height;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var frame = new Texture2D(w, h, TextureFormat.RGB24, false);
            frame.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            frame.Apply();

            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(rt);

            return frame;
        }

        private IEnumerator SendVisionRequest(string base64Jpg, string prompt, Action<string> onResult)
        {
            var bodyJson = "{" +
                $"\"model\":\"{Model}\"," +
                "\"max_tokens\":20," +
                "\"messages\":[{\"role\":\"user\",\"content\":[" +
                "{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"" + base64Jpg + "\"}}," +
                "{\"type\":\"text\",\"text\":\"" + EscapeJson(prompt) + "\"}" +
                "]}]}";

            var request = new UnityWebRequest(ApiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.SetRequestHeader("anthropic-version", "2023-06-01");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CardVisionScanner] API request failed: {request.error}");
                OnScanFailed?.Invoke(request.error);
                yield break;
            }

            // Minimal extraction — pull the first "text" field's value out of
            // the response JSON without a full JSON parser dependency.
            var responseText = request.downloadHandler.text;
            var token = ExtractFirstTextField(responseText);
            onResult?.Invoke(token?.Trim().ToLowerInvariant());
        }

        private void HandleScanResult(string zoneName, string token)
        {
            var zoneTransform = FieldZoneManager.Instance != null
                ? FieldZoneManager.Instance.GetZoneTransform(zoneName)
                : null;

            if (zoneTransform == null)
            {
                Debug.LogWarning($"[CardVisionScanner] No zone transform found for {zoneName}.");
                return;
            }

            if (string.IsNullOrEmpty(token) || token == "empty")
            {
                Debug.Log($"[CardVisionScanner] Nothing recognized for {zoneName}.");
                return;
            }

            if (token == "card_back")
            {
                GameFieldManager.Instance.OnFaceDownCardPlaced(zoneTransform);
                return;
            }

            if (CardDatabase.Get(token) != null)
            {
                GameFieldManager.Instance.OnCardRecognized(token, zoneTransform);
            }
            else
            {
                Debug.LogWarning($"[CardVisionScanner] Model returned unrecognized token '{token}'.");
            }
        }

        // --- tiny helpers, no external JSON package required ---

        private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string ExtractFirstTextField(string json)
        {
            const string marker = "\"text\":\"";
            int start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
        }
    }
}
