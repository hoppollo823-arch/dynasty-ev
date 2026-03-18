// ============================================================================
// DynastyVR Earth — Settings Panel
// 东方皇朝元宇宙部 | DynastyEV VR Earth Settings
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Settings overlay panel with:
    ///   • Quality preset (Low / Medium / High)
    ///   • Time of day (Day / Sunset / Night)
    ///   • Weather (Clear / Cloudy / Foggy)
    ///   • Language (中文 / English)
    /// Toggle visibility with Show() / Hide() or the close button.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsPanel : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Enums
        // -------------------------------------------------------------------
        public enum QualityLevel { Low, Medium, High }
        public enum TimeOfDay    { Day, Sunset, Night }
        public enum WeatherType  { Clear, Cloudy, Foggy }
        public enum Language      { Chinese, English }

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------
        public event Action<QualityLevel> OnQualityChanged;
        public event Action<TimeOfDay>    OnTimeChanged;
        public event Action<WeatherType>  OnWeatherChanged;
        public event Action<Language>     OnLanguageChanged;
        public event Action              OnPanelClosed;

        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("References")]
        [Tooltip("Main UI Manager – used to show/hide main UI when settings opens.")]
        public MainUIManager mainUI;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private QualityLevel currentQuality = QualityLevel.Medium;
        private TimeOfDay    currentTime    = TimeOfDay.Day;
        private WeatherType  currentWeather = WeatherType.Clear;
        private Language     currentLang    = Language.Chinese;

        // -------------------------------------------------------------------
        // UI refs
        // -------------------------------------------------------------------
        private VisualElement root;
        private VisualElement panel;

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------
        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            root = uiDoc.rootVisualElement;

            var style = Resources.Load<StyleSheet>("UI/MainUI");
            if (style != null) root.styleSheets.Add(style);

            BuildPanel();
            Hide(); // start hidden
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------
        private void BuildPanel()
        {
            panel = new VisualElement { name = "settings-panel" };

            // Header
            var header = new VisualElement { name = "settings-header" };
            var title = new Label("⚙️ 设置 Settings") { name = "settings-title" };
            var closeBtn = new Button(Hide) { text = "✕", name = "settings-close" };
            closeBtn.AddToClassList("icon-button");
            header.Add(title);
            header.Add(closeBtn);
            panel.Add(header);

            // Quality
            panel.Add(BuildSection("画质 Quality", new[] { "低 Low", "中 Medium", "高 High" },
                (int)currentQuality, idx => SetQuality((QualityLevel)idx)));

            // Time
            panel.Add(BuildSection("时间 Time", new[] { "白天 Day", "日落 Sunset", "夜晚 Night" },
                (int)currentTime, idx => SetTime((TimeOfDay)idx)));

            // Weather
            panel.Add(BuildSection("天气 Weather", new[] { "☀️ 晴 Clear", "☁️ 多云 Cloudy", "🌫 雾 Foggy" },
                (int)currentWeather, idx => SetWeather((WeatherType)idx)));

            // Language
            panel.Add(BuildSection("语言 Language", new[] { "中文", "English" },
                (int)currentLang, idx => SetLanguage((Language)idx)));

            root.Add(panel);
        }

        private VisualElement BuildSection(string label, string[] options, int selectedIndex, Action<int> callback)
        {
            var section = new VisualElement { name = $"section-{label.Replace(" ", "").ToLower()}" };
            section.AddToClassList("settings-section");

            var sectionLabel = new Label(label) { name = "section-label" };
            section.Add(sectionLabel);

            var row = new VisualElement { name = "option-row" };
            row.AddToClassList("option-row");

            for (int i = 0; i < options.Length; i++)
            {
                int idx = i; // capture
                var btn = new Button(() => { SelectButton(row, idx); callback(idx); })
                {
                    text = options[i],
                    name = $"opt-{idx}"
                };
                btn.AddToClassList("option-button");
                if (idx == selectedIndex)
                    btn.AddToClassList("selected");
                row.Add(btn);
            }

            section.Add(row);
            return section;
        }

        // -------------------------------------------------------------------
        // Actions
        // -------------------------------------------------------------------
        private void SetQuality(QualityLevel q)
        {
            currentQuality = q;
            QualitySettings.SetQualityLevel((int)q, true);
            Debug.Log($"[Settings] Quality → {q}");
            OnQualityChanged?.Invoke(q);
        }

        private void SetTime(TimeOfDay t)
        {
            currentTime = t;
            Debug.Log($"[Settings] Time → {t}");
            OnTimeChanged?.Invoke(t);
            // Example: adjust DirectionalLight or Skybox here
        }

        private void SetWeather(WeatherType w)
        {
            currentWeather = w;
            Debug.Log($"[Settings] Weather → {w}");
            OnWeatherChanged?.Invoke(w);
        }

        private void SetLanguage(Language l)
        {
            currentLang = l;
            Debug.Log($"[Settings] Language → {l}");
            OnLanguageChanged?.Invoke(l);
        }

        private static void SelectButton(VisualElement row, int index)
        {
            foreach (var child in row.Children())
            {
                child.RemoveFromClassList("selected");
            }
            if (index < row.childCount)
                row[index].AddToClassList("selected");
        }

        // -------------------------------------------------------------------
        // Show / Hide
        // -------------------------------------------------------------------

        /// <summary>
        /// Show the settings panel. Optionally hides the main UI.
        /// </summary>
        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
            mainUI?.SetUIVisible(false);
        }

        /// <summary>
        /// Hide the settings panel and restore the main UI.
        /// </summary>
        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
            mainUI?.SetUIVisible(true);
            OnPanelClosed?.Invoke();
        }

        public bool IsVisible => panel != null && panel.style.display == DisplayStyle.Flex;
    }
}
