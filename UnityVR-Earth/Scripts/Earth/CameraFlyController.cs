// ============================================================================
// DynastyVR Earth — Camera Fly Controller
// 鼠标滚轮缩放 / 拖拽旋转 / 点击飞行 / 自由飞行(WASD)
// ============================================================================

using UnityEngine;

namespace DynastyVR.Earth
{
    /// <summary>
    /// Provides globe navigation: scroll zoom, drag rotation, smooth fly-to,
    /// and free-flight mode (WASD + mouse look).
    /// </summary>
    public class CameraFlyController : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("Zoom Settings")]
        [Tooltip("Mouse scroll zoom speed (units per scroll tick)")]
        public float zoomSpeed = 5000f;

        [Tooltip("Minimum distance from ground")]
        public float minDistance = 100f;

        [Tooltip("Maximum altitude")]
        public float maxDistance = 20000000f;

        [Tooltip("Zoom smoothing factor (higher = faster response)")]
        public float zoomSmoothing = 8f;

        [Header("Rotation Settings")]
        [Tooltip("Drag rotation sensitivity")]
        public float rotationSpeed = 0.3f;

        [Tooltip("Rotation smoothing factor")]
        public float rotationSmoothing = 10f;

        [Header("Fly-To Settings")]
        [Tooltip("Fly-to animation speed (seconds to complete)")]
        public float flyToDuration = 2.0f;

        [Tooltip("Curve for fly-to easing")]
        public AnimationCurve flyToCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Free Flight Mode")]
        [Tooltip("Free flight move speed (m/s)")]
        public float freeFlightSpeed = 5000f;

        [Tooltip("Free flight fast speed multiplier")]
        public float fastMultiplier = 3f;

        [Tooltip("Mouse look sensitivity")]
        public float mouseLookSensitivity = 2f;

        [Tooltip("Key for toggling free-flight mode")]
        public KeyCode freeFlightToggleKey = KeyCode.F;

        [Tooltip("Key for ascending in free-flight")]
        public KeyCode ascendKey = KeyCode.Space;

        [Tooltip("Key for descending in free-flight")]
        public KeyCode descendKey = KeyCode.LeftControl;

        [Header("References")]
        [Tooltip("Camera to control (auto-finds Main Camera if empty)")]
        public Camera mainCamera;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        public bool IsFreeFlightMode { get; private set; }
        public bool IsFlying { get; private set; }

        // Internal state
        private float currentDistance;
        private float targetDistance;
        private Quaternion targetRotation;
        private Vector3 targetPosition;
        private float flyTimer;
        private float flyDuration;
        private Vector3 flyStartPos;
        private Quaternion flyStartRot;

        // Free flight
        private float freeFlightPitch;
        private float freeFlightYaw;

        // Drag state
        private bool isDragging;
        private Vector2 lastMousePos;

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------
        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("[CameraFlyController] No camera found!");
                enabled = false;
                return;
            }

            // Initialize state
            currentDistance = 100000f;
            targetDistance = currentDistance;
            targetRotation = transform.rotation;
            targetPosition = transform.position;

            // Start looking at the globe from the CesiumGeoreference origin
            if (CesiumEarthManager.Instance != null)
            {
                var loc = CesiumEarthManager.Instance.GetCurrentLocation();
                currentDistance = (float)loc.distance;
                targetDistance = currentDistance;
            }

            // Initial position setup
            UpdateCameraPosition();
        }

        private void Update()
        {
            if (IsFlying)
            {
                UpdateFlyTo();
                return;
            }

            HandleZoom();
            HandleDragRotation();
            HandleFreeFlightToggle();
            HandleClickToFly();

            if (IsFreeFlightMode)
            {
                UpdateFreeFlight();
            }
            else
            {
                UpdateCameraPosition();
            }
        }

        // -------------------------------------------------------------------
        // Zoom (Mouse Scroll)
        // -------------------------------------------------------------------
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // Exponential zoom for natural feel
                float zoomFactor = 1f - scroll * 2f;
                targetDistance *= zoomFactor;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }

            // Smooth interpolation
            currentDistance = Mathf.Lerp(currentDistance, targetDistance,
                                          Time.deltaTime * zoomSmoothing);
        }

        // -------------------------------------------------------------------
        // Drag Rotation
        // -------------------------------------------------------------------
        private void HandleDragRotation()
        {
            // Right mouse button or middle mouse button for rotation
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                isDragging = true;
                lastMousePos = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            {
                isDragging = false;
            }

            if (isDragging && !IsFreeFlightMode)
            {
                Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePos;
                lastMousePos = Input.mousePosition;

                // Horizontal rotation (yaw) around world up
                Quaternion yawRotation = Quaternion.AngleAxis(
                    mouseDelta.x * rotationSpeed, Vector3.up);

                // Vertical rotation (pitch) around local right
                Quaternion pitchRotation = Quaternion.AngleAxis(
                    -mouseDelta.y * rotationSpeed, transform.right);

                targetRotation = yawRotation * pitchRotation * targetRotation;
                targetRotation = Quaternion.Euler(
                    ClampPitch(targetRotation.eulerAngles.x),
                    targetRotation.eulerAngles.y,
                    0);
            }
        }

        /// <summary>
        /// Clamp pitch to prevent flipping.
        /// </summary>
        private float ClampPitch(float pitch)
        {
            return Mathf.Clamp(pitch > 180 ? pitch - 360 : pitch, -89f, 89f);
        }

        // -------------------------------------------------------------------
        // Click-to-Fly (Left click on globe)
        // -------------------------------------------------------------------
        private void HandleClickToFly()
        {
            if (Input.GetMouseButtonDown(0) && !isDragging)
            {
                // Don't intercept clicks when UI is in front
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
                {
                    StartFlyTo(hit.point);
                }
            }
        }

        // -------------------------------------------------------------------
        // Smooth Fly-To
        // -------------------------------------------------------------------
        /// <summary>
        /// Start a smooth camera flight to a world position.
        /// </summary>
        public void StartFlyTo(Vector3 worldPosition, float duration = -1)
        {
            flyStartPos = transform.position;
            flyStartRot = transform.rotation;

            targetPosition = worldPosition;
            IsFlying = true;
            flyTimer = 0f;
            flyDuration = duration > 0 ? duration : flyToDuration;
        }

        /// <summary>
        /// Fly to a specific distance looking at the globe.
        /// </summary>
        public void StartFlyToDistance(float distance)
        {
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            currentDistance = targetDistance;
        }

        private void UpdateFlyTo()
        {
            flyTimer += Time.deltaTime;
            float t = flyTimer / flyDuration;

            if (t >= 1f)
            {
                IsFlying = false;
                currentDistance = targetDistance;
                return;
            }

            float easedT = flyToCurve.Evaluate(t);
            transform.position = Vector3.Lerp(flyStartPos, targetPosition, easedT);
            transform.rotation = Quaternion.Slerp(flyStartRot, targetRotation, easedT);
        }

        // -------------------------------------------------------------------
        // Free Flight Mode (WASD + Mouse Look)
        // -------------------------------------------------------------------
        private void HandleFreeFlightToggle()
        {
            if (Input.GetKeyDown(freeFlightToggleKey))
            {
                ToggleFreeFlight();
            }
        }

        public void ToggleFreeFlight()
        {
            IsFreeFlightMode = !IsFreeFlightMode;

            if (IsFreeFlightMode)
            {
                // Initialize free flight orientation from current rotation
                freeFlightYaw = transform.eulerAngles.y;
                freeFlightPitch = -transform.eulerAngles.x;
                if (freeFlightPitch > 180) freeFlightPitch -= 360;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("[CameraFlyController] Free Flight Mode: ON");
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("[CameraFlyController] Free Flight Mode: OFF");
            }
        }

        private void UpdateFreeFlight()
        {
            // Mouse look (only when locked)
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                freeFlightYaw += Input.GetAxis("Mouse X") * mouseLookSensitivity;
                freeFlightPitch -= Input.GetAxis("Mouse Y") * mouseLookSensitivity;
                freeFlightPitch = Mathf.Clamp(freeFlightPitch, -89f, 89f);

                transform.rotation = Quaternion.Euler(freeFlightPitch, freeFlightYaw, 0);
            }

            // WASD movement
            float speed = freeFlightSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= fastMultiplier;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(ascendKey)) move += Vector3.up;
            if (Input.GetKey(descendKey)) move -= Vector3.up;

            transform.position += move.normalized * speed;
        }

        // -------------------------------------------------------------------
        // Camera Position Update (orbit mode)
        // -------------------------------------------------------------------
        private void UpdateCameraPosition()
        {
            // Position the camera at the current distance from origin,
            // behind the rotation target
            Vector3 direction = targetRotation * Vector3.back;
            transform.position = direction * currentDistance;
            transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        }

        // -------------------------------------------------------------------
        // Public Utility
        // -------------------------------------------------------------------

        /// <summary>
        /// Teleport camera to a position and rotation (no animation).
        /// </summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            targetRotation = rotation;
            IsFlying = false;
        }

        /// <summary>
        /// Reset camera to overview (high altitude looking down).
        /// </summary>
        public void ResetToOverview()
        {
            targetDistance = 1000000f;
            currentDistance = targetDistance;
            targetRotation = Quaternion.Euler(0, 0, 0);
            IsFlying = false;
            IsFreeFlightMode = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
