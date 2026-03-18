// ============================================================================
// DynastyVR Earth — Location Database (shared data for UI + navigation)
// 东方皇朝元宇宙部 | DynastyEV VR Earth Location Data
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Central registry of location presets with display metadata.
    /// Used by MainUIManager (quick-list), LocationInfoPanel (descriptions),
    /// and CesiumEarthManager (fly-to coordinates).
    /// </summary>
    public static class LocationDatabase
    {
        // -------------------------------------------------------------------
        // Data
        // -------------------------------------------------------------------
        [System.Serializable]
        public class LocationEntry
        {
            public string key;
            public string nameCN;
            public string nameEN;
            public double longitude;
            public double latitude;
            public double height;
            public float heading;
            public float pitch;
            public float distance;
            [TextArea(3, 6)]
            public string descriptionCN;
            [TextArea(3, 6)]
            public string descriptionEN;
            public string photoFileName;   // in Resources/UI/LocationPhotos/
        }

        // -------------------------------------------------------------------
        // Preloaded entries
        // -------------------------------------------------------------------
        private static List<LocationEntry> _entries;

        public static List<LocationEntry> Entries
        {
            get
            {
                if (_entries == null) Initialize();
                return _entries;
            }
        }

        // -------------------------------------------------------------------
        // Initialization
        // -------------------------------------------------------------------
        private static void Initialize()
        {
            _entries = new List<LocationEntry>
            {
                new LocationEntry
                {
                    key = "Shanghai",
                    nameCN = "上海", nameEN = "Shanghai",
                    longitude = 121.4737, latitude = 31.2304, height = 100,
                    heading = 0, pitch = -30, distance = 50000,
                    descriptionCN = "上海，中国最大的城市和全球金融中心之一。外滩的万国建筑博览群与陆家嘴的摩天大楼隔江相望，展现了东西方文化的完美融合。",
                    descriptionEN = "Shanghai, China's largest city and one of the world's leading financial centers. The Bund's colonial architecture faces Lujiazui's glittering skyscrapers across the Huangpu River.",
                    photoFileName = "Shanghai"
                },
                new LocationEntry
                {
                    key = "NewYork",
                    nameCN = "纽约", nameEN = "New York",
                    longitude = -74.0060, latitude = 40.7128, height = 100,
                    heading = 0, pitch = -30, distance = 50000,
                    descriptionCN = "纽约，\"大苹果城\"，美国最大的城市。自由女神像、时代广场和中央公园是这座城市的标志性地标。",
                    descriptionEN = "New York City, 'The Big Apple', the largest city in the United States. The Statue of Liberty, Times Square, and Central Park are its iconic landmarks.",
                    photoFileName = "NewYork"
                },
                new LocationEntry
                {
                    key = "PangongLake",
                    nameCN = "班公湖", nameEN = "Pangong Lake",
                    longitude = 78.7375, latitude = 33.7167, height = 4250,
                    heading = 45, pitch = -45, distance = 100000,
                    descriptionCN = "班公湖，位于中国西藏阿里地区与印度争议边境的高原咸水湖。海拔4250米，湖水呈现出令人惊叹的蓝绿色。",
                    descriptionEN = "Pangong Tso, a high-altitude endorheic lake on the China-India border in Tibet/Aksai Chin. At 4,250m, its stunning turquoise waters shift color with the light.",
                    photoFileName = "PangongLake"
                },
                new LocationEntry
                {
                    key = "MtKailash",
                    nameCN = "冈仁波齐", nameEN = "Mt. Kailash",
                    longitude = 81.3061, latitude = 31.1667, height = 6638,
                    heading = 0, pitch = -20, distance = 80000,
                    descriptionCN = "冈仁波齐峰，海拔6638米，被印度教、藏传佛教、耆那教和苯教同时视为神圣之地。是亚洲四大河流的发源地。",
                    descriptionEN = "Mount Kailash (6,638m), revered simultaneously by Hinduism, Buddhism, Jainism, and Bon. Source of four major Asian rivers and one of Earth's most sacred mountains.",
                    photoFileName = "MtKailash"
                },
                new LocationEntry
                {
                    key = "DarkSkyPark",
                    nameCN = "暗夜公园", nameEN = "Dark Sky Park",
                    longitude = 116.8000, latitude = 40.4000, height = 1500,
                    heading = 0, pitch = -30, distance = 30000,
                    descriptionCN = "暗夜公园，远离城市光污染的星空观测圣地。在中国北京北部山区，可以欣赏到壮丽的银河和满天繁星。",
                    descriptionEN = "A Dark Sky Park — a sanctuary free from light pollution for stargazing. In the mountains north of Beijing, the Milky Way stretches across pristine night skies.",
                    photoFileName = "DarkSkyPark"
                }
            };
        }

        // -------------------------------------------------------------------
        // Lookup helpers
        // -------------------------------------------------------------------

        /// <summary>Find a location entry by key. Returns null if not found.</summary>
        public static LocationEntry GetByKey(string key)
        {
            return Entries.Find(e => e.key == key);
        }

        /// <summary>Search by query string (matches CN/EN name).</summary>
        public static List<LocationEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<LocationEntry>();
            string q = query.ToLowerInvariant();
            return Entries.FindAll(e =>
                e.key.ToLowerInvariant().Contains(q) ||
                e.nameCN.Contains(query) ||
                e.nameEN.ToLowerInvariant().Contains(q));
        }

        /// <summary>Get display name in the given language.</summary>
        public static string GetDisplayName(LocationEntry entry, bool useChinese)
        {
            return useChinese ? entry.nameCN : entry.nameEN;
        }

        /// <summary>Get description in the given language.</summary>
        public static string GetDescription(LocationEntry entry, bool useChinese)
        {
            return useChinese ? entry.descriptionCN : entry.descriptionEN;
        }
    }
}
