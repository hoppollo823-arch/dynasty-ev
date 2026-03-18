// ============================================================================
// DynastyVR Earth — HUD Manager (Heads-Up Display)
// 东方皇朝元宇宙部 | DynastyEV VR Earth HUD
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Runtime HUD overlay:
    ///   • Top-left  — FPS counter
    ///   • Top-center — arrival location name (auto-fade)
    ///   • Bottom-left  — GPS coordinates + altitude
    ///   • Bottom-right — speed + heading
    /// Attach to a UIDocument. Requires CesiumEarthManager in scene for data.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("Data Sources")]
        [Tooltip("Auto-found if empty. Used for georeference coordinates.")]
        public CesiumEarthManager earthManager;

        [Tooltip("Auto-found if empty. Used for camera transform (speed / heading).")]
        public Transform cameraTransform;

        [Header("HUD Settings")]
        [Tooltip("How many decimal places for GPS coordinates.")]
        public int gpsDecimalPlaces = 4;

        [Tooltip("Refresh interval in seconds for non-critical labels.")]
        public float refreshInterval = 0.15f;

        [Tooltip("Seconds to keep arrival name visible before hiding.")]
        public float arrivalDisplayDuration = 4f;

        [Tooltip("Fade-out duration in seconds.")]
        public float arrivalFadeDuration = 1f;

        // -------------------------------------------------------------------
        // UI refs
        // -------------------------------------------------------------------
        private VisualElement root;

        // Top-left
        private Label fpsLabel;

        // Top-center
        private Label arrivalLabel;
        private VisualElement arrivalContainer;

        // Bottom-left
        private Label gpsLabel;
        private Label altitudeLabel;

        // Bottom-right
        private Label speedLabel;
        private Label headingLabel;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private float fpsTimer;
        private int fpsCount;
        private float currentFPS;
        private float refreshTimer;

        private Vector3 prevPosition;
        private float currentSpeed;          // m/s
        private float arrivalTimer;
        private float arrivalFadeTimer;
        private bool arrivalVisible;

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------
        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            root = uiDoc.rootVisualElement;

            // Optional USS
            var style = Resources.Load<StyleSheet>("UI/MainUI");
            if (style != null) root.styleSheets.Add(style);

            BuildHUD();

            if (earthManager == null)
                earthManager = CesiumEarthManager.Instance;

            prevPosition = GetCameraPosition();
        }

        private void Update()
        {
            UpdateFPS();
            UpdateSpeed();
            RefreshDynamicLabels();
            UpdateArrivalFade();
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------
        private void BuildHUD()
        {
            // --- Top-left: FPS ---
            var topLeft = new VisualElement { name = "hud-top-left" };
            fpsLabel = new Label("FPS: --") { name = "fps-label" };
            topLeft.Add(fpsLabel);

            // --- Top-center: Arrival name ---
            arrivalContainer = new VisualElement { name = "hud-arrival-container" };
            arrivalLabel = new Label("") { name = "hud-arrival-label" };
            arrivalContainer.Add(arrivalLabel);
            arrivalContainer.style.display = DisplayStyle.None;

            // --- Bottom-left: GPS + Altitude ---
            var bottomLeft = new VisualElement { name = "hud-bottom-left" };
            gpsLabel    = new Label("GPS: --, --")    { name = "gps-label" };
            altitudeLabel = new Label("Alt: -- m")     { name = "altitude-label" };
            bottomLeft.Add(gpsLabel);
            bottomLeft.Add(altitudeLabel);

            // --- Bottom-right: Speed + Heading ---
            var bottomRight = new VisualElement { name = "hud-bottom-right" };
            speedLabel   = new Label("Speed: -- km/h") { name = "speed-label" };
            headingLabel = new Label("Heading: --°")   { name = "heading-label" };
            bottomRight.Add(speedLabel);
            bottomRight.Add(headingLabel);

            // Assemble
            root.Add(topLeft);
            root.Add(arrivalContainer);
            root.Add(bottomLeft);
            root.Add(bottomRight);
        }

        // -------------------------------------------------------------------
        // FPS
        // -------------------------------------------------------------------
        private void UpdateFPS()
        {
            fpsCount++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                currentFPS = fpsCount / fpsTimer;
                fpsLabel.text = $"FPS: {currentFPS:F0}";
                fpsCount = 0;
                fpsTimer = 0f;
            }
        }

        // -------------------------------------------------------------------
        // Speed (from camera movement)
        // -------------------------------------------------------------------
        private void UpdateSpeed()
        {
            Vector3 pos = GetCameraPosition();
            float delta = Vector3.Distance(pos, prevPosition);
            // Smoothed speed
            float instant = delta / Mathf.Max(Time.deltaTime, 0.0001f);
            currentSpeed = Mathf.Lerp(currentSpeed, instant, Time.deltaTime * 5f);
            prevPosition = pos;
        }

        // -------------------------------------------------------------------
        // Dynamic label refresh
        // -------------------------------------------------------------------
        private void RefreshDynamicLabels()
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer < refreshInterval) return;
            refreshTimer = 0f;

            // GPS
            if (earthManager != null && earthManager.georeference != null)
            {
                double lon = earthManager.georeference.longitude;
                double lat = earthManager.georeference.latitude;
                double alt = earthManager.georeference.height;
                gpsLabel.text      = $"GPS: {lat:F{gpsDecimalPlaces}}°, {lon:F{gpsDecimalPlaces}}°";
                altitudeLabel.text = $"Alt: {alt:F1} m";
            }

            // Speed
            float kmh = currentSpeed * 3.6f;
            speedLabel.text = $"Speed: {kmh:F1} km/h";

            // Heading from camera forward
            if (cameraTransform != null)
            {
                float heading = GetCameraHeading();
                headingLabel.text = $"Heading: {heading:F1}°";
            }
        }

        // -------------------------------------------------------------------
        // Arrival popup
        // -------------------------------------------------------------------

        /// <summary>
        /// Call when the user arrives at (or selects) a location.
        /// </summary>
        public void ShowArrivalNotification(string locationName)
        {
            arrivalLabel.text = locationName;
            arrivalContainer.style.display = DisplayStyle.Flex;
            arrivalContainer.style.opacity = 1f;
            arrivalTimer = 0f;
            arrivalFadeTimer = 0f;
            arrivalVisible = true;
        }

        private void UpdateArrivalFade()
        {
            if (!arrivalVisible) return;

            arrivalTimer += Time.deltaTime;

            if (arrivalTimer > arrivalDisplayDuration)
            {
                arrivalFadeTimer += Time.deltaTime;
                float t = Mathf.Clamp01(arrivalFadeTimer / arrivalFadeDuration);
                arrivalContainer.style.opacity = 1f - t;

                if (t >= 1f)
                {
                    arrivalContainer.style.display = DisplayStyle.None;
                    arrivalVisible = false;
                }
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private Vector3 GetCameraPosition()
        {
            if (cameraTransform != null) return cameraTransform.position;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        private float GetCameraHeading()
        {
            var cam = cameraTransform != null ? cameraTransform : Camera.main?.transform;
            if (cam == null) return 0f;
            Vector3 forward = cam.forward;
            float angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            return angle;
        }
    }
}
