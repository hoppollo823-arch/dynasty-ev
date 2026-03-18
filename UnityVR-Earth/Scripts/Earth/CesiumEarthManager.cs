// ============================================================================
// DynastyVR Earth — Cesium Earth Initialization Manager
// 东方皇朝元宇宙部 | DynastyEV President Office
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;

namespace DynastyVR.Earth
{
    /// <summary>
    /// Manages Cesium globe initialization, tileset loading, and location switching.
    /// Attach to a GameObject in the scene. Will auto-create CesiumGeoreference
    /// and Cesium3DTileset if they don't already exist.
    /// </summary>
    public class CesiumEarthManager : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------
        public static CesiumEarthManager Instance { get; private set; }

        // -------------------------------------------------------------------
        // Inspector Fields
        // -------------------------------------------------------------------
        [Header("Cesium Configuration")]
        [Tooltip("Cesium ion access token. Get one at https://cesium.com/ion/tokens")]
        public string cesiumIonAccessToken = "YOUR_CESIUM_ION_ACCESS_TOKEN";

        [Header("Tileset Settings")]
        [Tooltip("Google Earth Photorealistic Tiles asset ID (usually 1)")]
        public int photorealisticTilesAssetId = 1;

        [Tooltip("Cesium OSM Buildings asset ID")]
        public int osmBuildingsAssetId = 96188;

        [Tooltip("Use photorealistic tiles instead of OSM buildings")]
        public bool usePhotorealisticTiles = true;

        [Header("Initial Location")]
        [Tooltip("Select a predefined starting location")]
        public InitialLocation startingLocation = InitialLocation.Shanghai;

        [Header("Cesium Component References (auto-found if empty)")]
        public CesiumGeoreference georeference;
        public Cesium3DTileset tileset;

        // -------------------------------------------------------------------
        // Enums
        // -------------------------------------------------------------------
        public enum InitialLocation
        {
            Shanghai,
            NewYork,
            PangongLake,
            MtKailash,
            Custom
        }

        // -------------------------------------------------------------------
        // Predefined Locations
        // -------------------------------------------------------------------
        [Serializable]
        public struct LocationPreset
        {
            public string name;
            public double longitude;
            public double latitude;
            public double height;
            public float heading;
            public float pitch;
            public float distance;
        }

        public static readonly Dictionary<InitialLocation, LocationPreset> LocationPresets
            = new Dictionary<InitialLocation, LocationPreset>
        {
            {
                InitialLocation.Shanghai,
                new LocationPreset
                {
                    name = "Shanghai (上海)",
                    longitude = 121.4737,
                    latitude = 31.2304,
                    height = 100,
                    heading = 0,
                    pitch = -30,
                    distance = 50000
                }
            },
            {
                InitialLocation.NewYork,
                new LocationPreset
                {
                    name = "New York (纽约)",
                    longitude = -74.0060,
                    latitude = 40.7128,
                    height = 100,
                    heading = 0,
                    pitch = -30,
                    distance = 50000
                }
            },
            {
                InitialLocation.PangongLake,
                new LocationPreset
                {
                    name = "Pangong Lake (班公湖)",
                    longitude = 78.7375,
                    latitude = 33.7167,
                    height = 4250,
                    heading = 45,
                    pitch = -45,
                    distance = 100000
                }
            },
            {
                InitialLocation.MtKailash,
                new LocationPreset
                {
                    name = "Mt. Kailash (冈仁波齐)",
                    longitude = 81.3061,
                    latitude = 31.1667,
                    height = 6638,
                    heading = 0,
                    pitch = -20,
                    distance = 80000
                }
            }
        };

        // Custom location (set via code or inspector)
        [HideInInspector] public double customLongitude;
        [HideInInspector] public double customLatitude;
        [HideInInspector] public double customHeight;

        // -------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------
        public event Action OnEarthInitialized;
        public event Action<LocationPreset> OnLocationChanged;

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private LocationPreset currentLocation;
        private bool isInitialized;

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeCesiumEarth();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Initialize or re-initialize the Cesium globe.
        /// </summary>
        public void InitializeCesiumEarth()
        {
            SetupGeoreference();
            SetupTileset();
            FlyToInitialLocation();
            isInitialized = true;
            OnEarthInitialized?.Invoke();
            Debug.Log($"[CesiumEarthManager] Earth initialized at {currentLocation.name}");
        }

        /// <summary>
        /// Switch to a predefined location with optional animation.
        /// </summary>
        public void FlyToLocation(InitialLocation location)
        {
            if (!LocationPresets.TryGetValue(location, out LocationPreset preset))
            {
                Debug.LogWarning($"[CesiumEarthManager] Unknown location: {location}");
                return;
            }
            FlyToPreset(preset);
        }

        /// <summary>
        /// Fly to a custom coordinate.
        /// </summary>
        public void FlyToCoordinates(double longitude, double latitude, double height = 0)
        {
            var preset = new LocationPreset
            {
                name = $"Custom ({longitude:F4}, {latitude:F4})",
                longitude = longitude,
                latitude = latitude,
                height = height,
                heading = 0,
                pitch = -30,
                distance = 50000
            };
            FlyToPreset(preset);
        }

        /// <summary>
        /// Update the georeference origin (useful for precision at high zoom).
        /// </summary>
        public void UpdateOrigin(double longitude, double latitude, double height)
        {
            if (georeference != null)
            {
                georeference.longitude = longitude;
                georeference.latitude = latitude;
                georeference.height = height;
            }
        }

        /// <summary>
        /// Get the current location preset.
        /// </summary>
        public LocationPreset GetCurrentLocation() => currentLocation;

        /// <summary>
        /// Check if Cesium is fully initialized and ready.
        /// </summary>
        public bool IsInitialized => isInitialized;

        // -------------------------------------------------------------------
        // Private Methods
        // -------------------------------------------------------------------

        private void SetupGeoreference()
        {
            georeference = GetComponent<CesiumGeoreference>();
            if (georeference == null)
            {
                georeference = gameObject.AddComponent<CesiumGeoreference>();
            }

            var preset = GetSelectedPreset();
            georeference.longitude = preset.longitude;
            georeference.latitude = preset.latitude;
            georeference.height = preset.height;
        }

        private void SetupTileset()
        {
            // Remove existing tilesets to avoid duplicates
            var existingTilesets = GetComponents<Cesium3DTileset>();
            foreach (var existing in existingTilesets)
            {
                DestroyImmediate(existing);
            }

            tileset = gameObject.AddComponent<Cesium3DTileset>();

            if (usePhotorealisticTiles)
            {
                tileset.tilesetSource = Cesium3DTileset.TilesetSource.FromCesiumIon;
                tileset.ionAssetId = photorealisticTilesAssetId;
                tileset.ionAccessToken = cesiumIonAccessToken;
            }
            else
            {
                tileset.tilesetSource = Cesium3DTileset.TilesetSource.FromCesiumIon;
                tileset.ionAssetId = osmBuildingsAssetId;
                tileset.ionAccessToken = cesiumIonAccessToken;
            }

            // LOD and quality settings
            tileset.maximumScreenSpaceError = 16f;
            tileset.maximumCacheSizeBytes = 512 * 1024 * 1024; // 512 MB
            tileset.enabled = true;
        }

        private LocationPreset GetSelectedPreset()
        {
            if (startingLocation == InitialLocation.Custom)
            {
                return new LocationPreset
                {
                    name = "Custom",
                    longitude = customLongitude,
                    latitude = customLatitude,
                    height = customHeight,
                    heading = 0,
                    pitch = -30,
                    distance = 50000
                };
            }
            return LocationPresets[startingLocation];
        }

        private void FlyToInitialLocation()
        {
            currentLocation = GetSelectedPreset();
            OnLocationChanged?.Invoke(currentLocation);
        }

        private void FlyToPreset(LocationPreset preset)
        {
            currentLocation = preset;
            UpdateOrigin(preset.longitude, preset.latitude, preset.height);
            OnLocationChanged?.Invoke(preset);
            Debug.Log($"[CesiumEarthManager] Flying to: {preset.name}");
        }
    }
}
