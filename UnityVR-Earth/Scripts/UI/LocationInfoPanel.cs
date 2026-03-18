// ============================================================================
// DynastyVR Earth — Location Info Panel
// 东方皇朝元宇宙部 | DynastyEV VR Earth Location Info
// ============================================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Detail panel for a selected location:
    ///   • Location name (large title)
    ///   • Description text
    ///   • Photo (loaded from Resources, or placeholder if missing)
    ///   • Close button
    /// 
    /// Call <see cref="Show(string, string, string)"/> to display.
    /// Placeholder images go in:  Assets/Resources/UI/LocationPhotos/
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LocationInfoPanel : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("Photo Settings")]
        [Tooltip("Subfolder under Resources/ for location photos.")]
        public string photoResourcesFolder = "UI/LocationPhotos";

        [Tooltip("Fallback placeholder name (loaded from Resources/UI/).")]
        public string placeholderImageName = "PlaceholderLocation";

        // -------------------------------------------------------------------
        // UI refs
        // -------------------------------------------------------------------
        private VisualElement root;
        private VisualElement panel;
        private Label nameLabel;
        private Label descriptionLabel;
        private VisualElement photoContainer;
        private Image photoImage;
        private Button closeButton;

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
            Hide();
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------
        private void BuildPanel()
        {
            panel = new VisualElement { name = "location-info-panel" };

            // Scrollable content wrapper
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "info-scroll" };
            scroll.AddToClassList("info-scrollview");

            // Photo
            photoContainer = new VisualElement { name = "info-photo-container" };
            photoImage = new Image { name = "info-photo" };
            photoImage.AddToClassList("info-photo");
            photoContainer.Add(photoImage);
            scroll.Add(photoContainer);

            // Name
            nameLabel = new Label("") { name = "info-name" };

            // Description
            descriptionLabel = new Label("") { name = "info-description" };
            descriptionLabel.AddToClassList("info-desc");

            scroll.Add(nameLabel);
            scroll.Add(descriptionLabel);

            // Close button
            closeButton = new Button(Hide) { text = "关闭 Close", name = "info-close" };

            panel.Add(scroll);
            panel.Add(closeButton);

            root.Add(panel);
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Show the location info panel with the given data.
        /// </summary>
        /// <param name="name">Location display name.</param>
        /// <param name="description">Intro / description text.</param>
        /// <param name="photoFileName">File name under Resources/{photoResourcesFolder} (without extension). 
        /// If null/empty, a placeholder is used.</param>
        public void Show(string name, string description, string photoFileName = null)
        {
            nameLabel.text = name;
            descriptionLabel.text = description;
            LoadPhoto(photoFileName);
            panel.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Hide the panel.
        /// </summary>
        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        public bool IsVisible => panel != null && panel.style.display == DisplayStyle.Flex;

        // -------------------------------------------------------------------
        // Photo loading
        // -------------------------------------------------------------------
        private void LoadPhoto(string fileName)
        {
            Sprite sprite = null;

            if (!string.IsNullOrEmpty(fileName))
            {
                string path = System.IO.Path.Combine(photoResourcesFolder, fileName);
                sprite = Resources.Load<Sprite>(path);
            }

            if (sprite == null)
            {
                // Try placeholder
                sprite = Resources.Load<Sprite>($"UI/{placeholderImageName}");
            }

            if (sprite != null)
            {
                photoImage.sprite = sprite;
                photoImage.visible = true;
            }
            else
            {
                // Hide photo area if nothing available
                photoImage.visible = false;
                Debug.Log("[LocationInfo] No photo found – showing text only.");
            }
        }
    }
}
