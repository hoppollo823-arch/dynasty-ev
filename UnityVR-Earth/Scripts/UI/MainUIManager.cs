// ============================================================================
// DynastyVR Earth — Main UI Manager
// 东方皇朝元宇宙部 | DynastyEV VR Earth UI System
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Main overlay UI: top search bar, left quick-locations, bottom mode switcher,
    /// top-right settings button.  Uses UI Toolkit (UXML + USS).
    /// Attach to a UIDocument GameObject in the scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainUIManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("UXML / USS")]
        [Tooltip("Optional: assign a custom UXML. If null, built-in is loaded from Resources/UI/MainUI.uxml")]
        public VisualTreeAsset uxmlAsset;

        [Tooltip("Optional: assign a custom USS. If null, built-in is loaded from Resources/UI/MainUI.uss")]
        public StyleSheet ussStyleSheet;

        [Header("Quick Locations")]
        public List<QuickLocation> quickLocations = new List<QuickLocation>
        {
            new QuickLocation { displayName = "上海\nShanghai",     key = "Shanghai"   },
            new QuickLocation { displayName = "纽约\nNew York",     key = "NewYork"    },
            new QuickLocation { displayName = "班公湖\nPangong Lake",key = "PangongLake"},
            new QuickLocation { displayName = "冈仁波齐\nMt. Kailash",key = "MtKailash" },
            new QuickLocation { displayName = "暗夜公园\nDark Sky Park",key = "DarkSkyPark"},
        };

        // -------------------------------------------------------------------
        // Events (external listeners)
        // -------------------------------------------------------------------
        public event System.Action<string> OnSearchRequested;       // search text
        public event System.Action<string> OnQuickLocationClicked;  // location key
        public event System.Action<string> OnModeChanged;           // "Fly" / "Drive" / "Browse"
        public event System.Action OnSettingsClicked;

        // -------------------------------------------------------------------
        // UI References
        // -------------------------------------------------------------------
        private VisualElement root;
        private TextField searchField;
        private Button searchButton;
        private VisualElement locationListContainer;
        private VisualElement modeBar;
        private Button settingsButton;

        private Label currentModeLabel;

        // -------------------------------------------------------------------
        // Data
        // -------------------------------------------------------------------
        [System.Serializable]
        public struct QuickLocation
        {
            public string displayName;
            public string key;           // matches CesiumEarthManager.InitialLocation or custom key
        }

        private readonly string[] modes = { "飞行 Fly", "自驾 Drive", "浏览 Browse" };
        private int currentModeIndex = 0;

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------
        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            root = uiDoc.rootVisualElement;

            // Load stylesheet
            if (ussStyleSheet != null)
            {
                root.styleSheets.Add(ussStyleSheet);
            }
            else
            {
                var sheet = Resources.Load<StyleSheet>("UI/MainUI");
                if (sheet != null) root.styleSheets.Add(sheet);
            }

            BuildUI();
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------
        private void BuildUI()
        {
            // ---------- Top bar ----------
            var topBar = new VisualElement { name = "top-bar" };

            searchField = new TextField { name = "search-field", placeholderText = "🔍 搜索地点 Search location..." };
            searchField.RegisterValueChangedCallback(_ => { });
            searchField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    FireSearch();
            });

            searchButton = new Button(FireSearch) { text = "搜索", name = "search-button" };

            topBar.Add(searchField);
            topBar.Add(searchButton);

            // ---------- Settings button (top-right) ----------
            settingsButton = new Button(FireSettings) { text = "⚙️", name = "settings-button" };
            settingsButton.AddToClassList("icon-button");
            var settingsWrapper = new VisualElement { name = "settings-wrapper" };
            settingsWrapper.Add(settingsButton);

            // ---------- Left quick-location list ----------
            locationListContainer = new VisualElement { name = "location-list" };
            foreach (var loc in quickLocations)
            {
                var btn = new Button(() => FireQuickLocation(loc.key))
                {
                    text = loc.displayName,
                    name = $"btn-loc-{loc.key}"
                };
                btn.AddToClassList("location-button");
                locationListContainer.Add(btn);
            }

            // ---------- Bottom mode bar ----------
            modeBar = new VisualElement { name = "mode-bar" };
            currentModeLabel = new Label(modes[0]) { name = "current-mode-label" };

            var prevBtn = new Button(() => SwitchMode(-1)) { text = "◀", name = "mode-prev" };
            var nextBtn = new Button(() => SwitchMode(1))  { text = "▶", name = "mode-next" };
            prevBtn.AddToClassList("icon-button");
            nextBtn.AddToClassList("icon-button");

            modeBar.Add(prevBtn);
            modeBar.Add(currentModeLabel);
            modeBar.Add(nextBtn);

            // ---------- Assemble ----------
            root.Add(topBar);
            root.Add(settingsWrapper);
            root.Add(locationListContainer);
            root.Add(modeBar);
        }

        // -------------------------------------------------------------------
        // Event binding
        // -------------------------------------------------------------------
        private void BindEvents() { }
        private void UnbindEvents() { }

        // -------------------------------------------------------------------
        // Actions
        // -------------------------------------------------------------------
        private void FireSearch()
        {
            string query = searchField.value?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                Debug.Log($"[MainUI] Search: {query}");
                OnSearchRequested?.Invoke(query);
            }
        }

        private void FireQuickLocation(string key)
        {
            Debug.Log($"[MainUI] Quick location: {key}");
            OnQuickLocationClicked?.Invoke(key);
        }

        private void SwitchMode(int delta)
        {
            currentModeIndex = (currentModeIndex + delta + modes.Length) % modes.Length;
            currentModeLabel.text = modes[currentModeIndex];

            string modeKey = currentModeIndex switch
            {
                0 => "Fly",
                1 => "Drive",
                2 => "Browse",
                _ => "Fly"
            };
            Debug.Log($"[MainUI] Mode: {modeKey}");
            OnModeChanged?.Invoke(modeKey);
        }

        private void FireSettings()
        {
            Debug.Log("[MainUI] Settings clicked");
            OnSettingsClicked?.Invoke();
        }

        // -------------------------------------------------------------------
        // Public helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Show or hide the entire main UI overlay.
        /// </summary>
        public void SetUIVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Programmatically set the search text.
        /// </summary>
        public void SetSearchText(string text)
        {
            if (searchField != null)
                searchField.SetValueWithoutNotify(text);
        }
    }
}
