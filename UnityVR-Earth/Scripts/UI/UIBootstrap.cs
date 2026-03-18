// ============================================================================
// DynastyVR Earth — UI Bootstrap (Scene Setup Helper)
// 东方皇朝元宇宙部 | DynastyEV VR Earth Scene Bootstrap
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Bootstraps all UI panels at runtime if no UIDocument objects exist in scene.
    /// Add this component to an empty GameObject called "[UI Bootstrap]".
    /// 
    /// In production, you'd create UIDocument GameObjects in the scene editor
    /// and assign components directly. This script is a convenience for
    /// programmatic / prototype setup.
    /// </summary>
    public class UIBootstrap : MonoBehaviour
    {
        [Header("Auto-create UIDocument objects")]
        public bool createMainUI = true;
        public bool createHUD = true;
        public bool createSettings = true;
        public bool createLocationInfo = true;

        [Header("References")]
        public CesiumEarthManager earthManager;

        private void Start()
        {
            if (earthManager == null)
                earthManager = CesiumEarthManager.Instance;

            if (createMainUI)
                SetupMainUI();

            if (createHUD)
                SetupHUD();

            if (createSettings)
                SetupSettings();

            if (createLocationInfo)
                SetupLocationInfo();

            // Wire events between panels
            WireEvents();

            Debug.Log("[UIBootstrap] All UI panels initialized.");
        }

        // -------------------------------------------------------------------
        // Panel creation helpers
        // -------------------------------------------------------------------

        private UIDocument CreatePanel(string name, out UIDocument doc)
        {
            var go = new GameObject(name);
            doc = go.AddComponent<UIDocument>();
            doc.sortingOrder = 100; // ensure overlay on top
            return doc;
        }

        private void SetupMainUI()
        {
            var go = new GameObject("[MainUI]");
            var doc = go.AddComponent<UIDocument>();
            doc.sortingOrder = 100;
            go.AddComponent<MainUIManager>();
        }

        private void SetupHUD()
        {
            var go = new GameObject("[HUD]");
            var doc = go.AddComponent<UIDocument>();
            doc.sortingOrder = 101;
            var hud = go.AddComponent<HUDManager>();
            hud.earthManager = earthManager;
        }

        private void SetupSettings()
        {
            var go = new GameObject("[Settings]");
            var doc = go.AddComponent<UIDocument>();
            doc.sortingOrder = 200;
            go.AddComponent<SettingsPanel>();
        }

        private void SetupLocationInfo()
        {
            var go = new GameObject("[LocationInfo]");
            var doc = go.AddComponent<UIDocument>();
            doc.sortingOrder = 200;
            go.AddComponent<LocationInfoPanel>();
        }

        // -------------------------------------------------------------------
        // Event wiring
        // -------------------------------------------------------------------
        private void WireEvents()
        {
            var mainUI  = FindObjectOfType<MainUIManager>();
            var settings = FindObjectOfType<SettingsPanel>();
            var locInfo  = FindObjectOfType<LocationInfoPanel>();
            var hud      = FindObjectOfType<HUDManager>();

            if (mainUI == null) return;

            // Search → Location lookup
            mainUI.OnSearchRequested += query =>
            {
                var results = LocationDatabase.Search(query);
                if (results.Count > 0)
                {
                    var entry = results[0];
                    FlyToLocation(entry);
                    ShowLocationInfo(entry);
                    hud?.ShowArrivalNotification(LocationDatabase.GetDisplayName(entry, true));
                }
                else
                {
                    Debug.Log($"[UI] No location found for: {query}");
                }
            };

            // Quick location click
            mainUI.OnQuickLocationClicked += key =>
            {
                var entry = LocationDatabase.GetByKey(key);
                if (entry != null)
                {
                    FlyToLocation(entry);
                    ShowLocationInfo(entry);
                    hud?.ShowArrivalNotification(LocationDatabase.GetDisplayName(entry, true));
                }
            };

            // Settings button
            mainUI.OnSettingsClicked += () =>
            {
                settings?.Show();
            };
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private void FlyToLocation(LocationDatabase.LocationEntry entry)
        {
            if (earthManager != null)
            {
                earthManager.FlyToCoordinates(entry.longitude, entry.latitude, entry.height);
            }
        }

        private void ShowLocationInfo(LocationDatabase.LocationEntry entry)
        {
            var locInfo = FindObjectOfType<LocationInfoPanel>();
            if (locInfo != null)
            {
                bool useChinese = true; // default; could come from settings
                locInfo.Show(
                    LocationDatabase.GetDisplayName(entry, useChinese),
                    LocationDatabase.GetDescription(entry, useChinese),
                    entry.photoFileName
                );
            }
        }
    }
}
