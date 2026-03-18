// ============================================================================
// DynastyVR Earth — Scene Bootstrap
// 自动创建场景中所有必需的对象（挂到任意空GameObject即可）
// ============================================================================

using UnityEngine;
using DynastyVR.Earth;
using DynastyVR.Earth.Route;
using DynastyVR.Earth.UI;

namespace DynastyVR.Earth
{
    /// <summary>
    /// Bootstrap script that automatically sets up the complete VR Earth scene.
    /// Add this to any GameObject in a new scene, hit Play, and everything
    /// gets wired up automatically.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        [Header("Quick Setup")]
        [Tooltip("If true, all managers are auto-created on Start")]
        public bool autoSetup = true;

        [Tooltip("Cesium ion access token")]
        public string cesiumToken = "YOUR_CESIUM_ION_ACCESS_TOKEN";

        [Tooltip("Starting location")]
        public CesiumEarthManager.InitialLocation startLocation = CesiumEarthManager.InitialLocation.Shanghai;

        private void Start()
        {
            if (!autoSetup) return;

            // 1. Cesium Earth
            var earthObj = GameObject.Find("CesiumEarth");
            if (earthObj == null)
            {
                earthObj = new GameObject("CesiumEarth");
                var earth = earthObj.AddComponent<CesiumEarthManager>();
                earth.cesiumIonAccessToken = cesiumToken;
                earth.startingLocation = startLocation;
            }

            // 2. Camera (if not exists)
            if (Camera.main == null)
            {
                var camObj = new GameObject("MainCamera");
                camObj.tag = "MainCamera";
                var cam = camObj.AddComponent<Camera>();
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 100000000f;
                camObj.AddComponent<AudioListener>();
            }

            // 3. Camera Fly Controller
            var camGo = Camera.main.gameObject;
            if (camGo.GetComponent<CameraFlyController>() == null)
            {
                camGo.AddComponent<CameraFlyController>();
            }

            // 4. Location Search
            if (LocationSearch.Instance == null)
            {
                var searchObj = new GameObject("LocationSearch");
                searchObj.AddComponent<LocationSearch>();
            }

            // 5. Route Display
            if (RouteDisplay.Instance == null)
            {
                var routeObj = new GameObject("RouteDisplay");
                routeObj.AddComponent<RouteDisplay>();
            }

            // 6. Main UI Manager
            if (MainUIManager.Instance == null)
            {
                var uiObj = new GameObject("MainUIManager");
                uiObj.AddComponent<MainUIManager>();
            }

            Debug.Log("[SceneBootstrap] DynastyVR Earth scene fully initialized! 🌍");
        }
    }
}
