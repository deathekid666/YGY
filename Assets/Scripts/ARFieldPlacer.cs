// This file only compiles once AR Foundation + an ARCore/ARKit XR Plugin
// are installed (Window > Package Manager) AND you've added the
// AR_FOUNDATION_ENABLED scripting define symbol (Project Settings > Player
// > Other Settings > Scripting Define Symbols). Without both, this file is
// skipped entirely and the rest of the project keeps working on the
// default WebCamTexture path (CameraFeedBackground) — nothing else in the
// scaffold depends on AR Foundation being present.
#if AR_FOUNDATION_ENABLED
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

namespace YugiohAR
{
    /// <summary>
    /// Real AR overlay: detects a horizontal plane (your table) and, on tap,
    /// anchors the entire field (FieldZoneManager and all 22 child zones) to
    /// that real-world position. After placement, plane visualization turns
    /// off so you're just left with the field grid sitting on your table
    /// through the camera — the "overlay look" the earlier mockups showed.
    ///
    /// Attach this to your XR Origin, alongside an ARRaycastManager and an
    /// ARPlaneManager. SceneAutoSetup's AR-mode build wires all of this for
    /// you when the AR_FOUNDATION_ENABLED symbol is present.
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class ARFieldPlacer : MonoBehaviour
    {
        [Tooltip("The FieldZoneManager's own Transform — everything moves with it")]
        public Transform fieldRoot;

        [Tooltip("Turned off automatically once the field is placed")]
        public ARPlaneManager planeManager;

        public bool IsPlaced { get; private set; }

        private ARRaycastManager raycastManager;
        private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        private void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
        }

        private void Update()
        {
            if (IsPlaced) return; // place once; re-enable manually if you want to reposition

            Vector2 tapPosition;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                tapPosition = Input.GetTouch(0).position;
            else if (Input.GetMouseButtonDown(0)) // lets you test placement in the Editor too
                tapPosition = Input.mousePosition;
            else
                return;

            if (raycastManager.Raycast(tapPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                var pose = hits[0].pose;
                fieldRoot.SetPositionAndRotation(pose.position, pose.rotation);
                IsPlaced = true;

                if (planeManager != null)
                {
                    planeManager.enabled = false;
                    foreach (var plane in planeManager.trackables)
                        plane.gameObject.SetActive(false);
                }

                Debug.Log("[ARFieldPlacer] Field placed at detected surface. Physically arrange your " +
                          "cards to match the zone layout you see overlaid on the table.");
            }
        }

        /// <summary>Call this (e.g. from a "Reposition Field" button) to place it again.</summary>
        public void ResetPlacement()
        {
            IsPlaced = false;
            if (planeManager != null)
                planeManager.enabled = true;
        }
    }
}
#endif
