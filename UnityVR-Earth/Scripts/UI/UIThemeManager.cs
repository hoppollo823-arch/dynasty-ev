// ============================================================================
// DynastyVR Earth — UI Theme Manager (Runtime style loading)
// 东方皇朝元宇宙部 | DynastyEV VR Earth UI Theme
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Utility to attach the shared USS stylesheet to any UIDocument's root.
    /// Call <see cref="ApplyTheme"/> from OnEnable of any UI component,
    /// or let this manager handle all UIDocuments in the scene.
    /// 
    /// Place USS at:  Assets/Resources/UI/MainUI.uss
    /// Place UXML at: Assets/Resources/UI/MainUI.uxml (optional – for prefab UIs)
    /// </summary>
    public class UIThemeManager : MonoBehaviour
    {
        public static UIThemeManager Instance { get; private set; }

        [Tooltip("Assign the shared stylesheet. If null, loads from Resources/UI/MainUI.")]
        public StyleSheet sharedStyleSheet;

        private StyleSheet loadedStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            loadedStyle = sharedStyleSheet != null
                ? sharedStyleSheet
                : Resources.Load<StyleSheet>("UI/MainUI");
        }

        /// <summary>
        /// Apply the shared theme stylesheet to a UIDocument.
        /// Safe to call multiple times – won't add duplicates.
        /// </summary>
        public void ApplyTheme(UIDocument doc)
        {
            if (doc == null || loadedStyle == null) return;
            if (!doc.rootVisualElement.styleSheets.Contains(loadedStyle))
                doc.rootVisualElement.styleSheets.Add(loadedStyle);
        }

        /// <summary>
        /// Apply the shared theme to any VisualElement tree.
        /// </summary>
        public void ApplyTheme(VisualElement root)
        {
            if (root == null || loadedStyle == null) return;
            if (!root.styleSheets.Contains(loadedStyle))
                root.styleSheets.Add(loadedStyle);
        }

        /// <summary>
        /// Static convenience – apply theme to any UIDocument using the singleton.
        /// </summary>
        public static void Apply(UIDocument doc) => Instance?.ApplyTheme(doc);
    }
}
