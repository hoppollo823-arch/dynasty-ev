// ============================================================================
// DynastyVR Earth — Location Search System
// 地名搜索 / 书签 / 快捷地点
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace DynastyVR.Earth
{
    /// <summary>
    /// Provides location search via Nominatim (OpenStreetMap geocoding),
    /// bookmark save/load, and quick-access buttons for predefined locations.
    /// </summary>
    public class LocationSearch : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------
        public static LocationSearch Instance { get; private set; }

        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("Search Settings")]
        [Tooltip("Nominatim API base URL (OpenStreetMap)")]
        public string nominatimUrl = "https://nominatim.openstreetmap.org/search";

        [Tooltip("User agent string for Nominatim (required by ToS)")]
        public string userAgent = "DynastyVR-Earth/1.0";

        [Tooltip("Results limit per search")]
        public int maxResults = 5;

        [Header("Bookmarks")]
        [Tooltip("Save file name (relative to persistentDataPath)")]
        public string bookmarksFileName = "dynastyvr_bookmarks.json";

        [Header("Quick Locations")]
        public List<QuickLocation> quickLocations = new List<QuickLocation>();

        // -------------------------------------------------------------------
        // Data
        // -------------------------------------------------------------------
        [Serializable]
        public class QuickLocation
        {
            public string displayName;
            public double longitude;
            public double latitude;
            public double height;
            public string icon = "📌";
        }

        [Serializable]
        public class Bookmark
        {
            public string name;
            public double longitude;
            public double latitude;
            public double height;
            public string dateAdded;
        }

        [Serializable]
        public class BookmarkList
        {
            public List<Bookmark> bookmarks = new List<Bookmark>();
        }

        [Serializable]
        public class NominatimResult
        {
            public string place_id;
            public string display_name;
            public double lon;
            public double lat;
        }

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private BookmarkList bookmarkData;
        private string bookmarksPath;
        private List<NominatimResult> searchResults = new List<NominatimResult>();
        private bool isSearching;

        // Events
        public event Action<List<NominatimResult>> OnSearchResults;
        public event Action<string> OnSearchError;
        public event Action<BookmarkList> OnBookmarksUpdated;

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

            bookmarksPath = Path.Combine(Application.persistentDataPath, bookmarksFileName);
            LoadBookmarks();

            // Populate default quick locations if empty
            if (quickLocations.Count == 0)
            {
                quickLocations.AddRange(new[]
                {
                    new QuickLocation { displayName = "上海 Shanghai", longitude = 121.4737, latitude = 31.2304, height = 100, icon = "🏙️" },
                    new QuickLocation { displayName = "纽约 New York", longitude = -74.0060, latitude = 40.7128, height = 100, icon = "🗽" },
                    new QuickLocation { displayName = "班公湖 Pangong Lake", longitude = 78.7375, latitude = 33.7167, height = 4250, icon = "🏔️" },
                    new QuickLocation { displayName = "冈仁波齐 Mt. Kailash", longitude = 81.3061, latitude = 31.1667, height = 6638, icon = "⛰️" },
                    new QuickLocation { displayName = "北京 Beijing", longitude = 116.4074, latitude = 39.9042, height = 100, icon = "🏯" },
                    new QuickLocation { displayName = "伦敦 London", longitude = -0.1276, latitude = 51.5074, height = 100, icon = "🇬🇧" },
                    new QuickLocation { displayName = "东京 Tokyo", longitude = 139.6917, latitude = 35.6895, height = 100, icon = "🗾" },
                    new QuickLocation { displayName = "巴黎 Paris", longitude = 2.3522, latitude = 48.8566, height = 100, icon = "🗼" },
                    new QuickLocation { displayName = "悉尼 Sydney", longitude = 151.2093, latitude = -33.8688, height = 100, icon = "🦘" },
                    new QuickLocation { displayName = "珠穆朗玛峰 Mt. Everest", longitude = 86.9250, latitude = 27.9881, height = 8848, icon = "🏔️" },
                    new QuickLocation { displayName = "撒哈拉沙漠 Sahara", longitude = 2.5, latitude = 23.4162, height = 500, icon = "🏜️" },
                    new QuickLocation { displayName = "自贡 Zigong", longitude = 104.7734, latitude = 29.3523, height = 300, icon = "🦕" },
                });
            }
        }

        // -------------------------------------------------------------------
        // Public API: Search
        // -------------------------------------------------------------------

        /// <summary>
        /// Search for a location by name using Nominatim geocoding.
        /// </summary>
        public void SearchLocation(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                OnSearchError?.Invoke("请输入搜索关键词");
                return;
            }

            if (isSearching)
            {
                Debug.Log("[LocationSearch] Search already in progress");
                return;
            }

            StartCoroutine(SearchCoroutine(query));
        }

        /// <summary>
        /// Get the latest search results (e.g., for UI display).
        /// </summary>
        public List<NominatimResult> GetSearchResults() => searchResults;

        // -------------------------------------------------------------------
        // Public API: Quick Locations
        // -------------------------------------------------------------------

        /// <summary>
        /// Fly to a quick location by index.
        /// </summary>
        public void FlyToQuickLocation(int index)
        {
            if (index < 0 || index >= quickLocations.Count) return;
            var loc = quickLocations[index];
            CesiumEarthManager.Instance?.FlyToCoordinates(loc.longitude, loc.latitude, loc.height);
        }

        /// <summary>
        /// Fly to a quick location by display name.
        /// </summary>
        public void FlyToQuickLocation(string name)
        {
            var loc = quickLocations.Find(l => l.displayName.Contains(name));
            if (loc != null)
            {
                CesiumEarthManager.Instance?.FlyToCoordinates(loc.longitude, loc.latitude, loc.height);
            }
        }

        // -------------------------------------------------------------------
        // Public API: Bookmarks
        // -------------------------------------------------------------------

        /// <summary>
        /// Add the current location as a bookmark.
        /// </summary>
        public void AddBookmark(string name, double longitude, double latitude, double height)
        {
            var bookmark = new Bookmark
            {
                name = name,
                longitude = longitude,
                latitude = latitude,
                height = height,
                dateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            bookmarkData.bookmarks.Add(bookmark);
            SaveBookmarks();
            OnBookmarksUpdated?.Invoke(bookmarkData);
        }

        /// <summary>
        /// Remove a bookmark by index.
        /// </summary>
        public void RemoveBookmark(int index)
        {
            if (index >= 0 && index < bookmarkData.bookmarks.Count)
            {
                bookmarkData.bookmarks.RemoveAt(index);
                SaveBookmarks();
                OnBookmarksUpdated?.Invoke(bookmarkData);
            }
        }

        /// <summary>
        /// Get all bookmarks.
        /// </summary>
        public List<Bookmark> GetBookmarks() => bookmarkData.bookmarks;

        /// <summary>
        /// Fly to a bookmark by index.
        /// </summary>
        public void FlyToBookmark(int index)
        {
            if (index < 0 || index >= bookmarkData.bookmarks.Count) return;
            var bm = bookmarkData.bookmarks[index];
            CesiumEarthManager.Instance?.FlyToCoordinates(bm.longitude, bm.latitude, bm.height);
        }

        // -------------------------------------------------------------------
        // Private: Nominatim Geocoding
        // -------------------------------------------------------------------
        private System.Collections.IEnumerator SearchCoroutine(string query)
        {
            isSearching = true;

            string url = $"{nominatimUrl}?q={UnityWebRequest.EscapeURL(query)}" +
                         $"&format=json&limit={maxResults}&accept-language=zh,en";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent", userAgent);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    // Simple JSON parsing (no external dependency)
                    searchResults = ParseNominatimResults(json);
                    OnSearchResults?.Invoke(searchResults);

                    if (searchResults.Count == 0)
                    {
                        OnSearchError?.Invoke($"未找到「{query}」的搜索结果");
                    }
                }
                else
                {
                    Debug.LogError($"[LocationSearch] Search failed: {request.error}");
                    OnSearchError?.Invoke($"搜索失败: {request.error}");
                }
            }

            isSearching = false;
        }

        /// <summary>
        /// Minimal JSON parser for Nominatim results (avoids Newtonsoft dependency).
        /// </summary>
        private List<NominatimResult> ParseNominatimResults(string json)
        {
            var results = new List<NominatimResult>();

            try
            {
                // Use Unity's JsonUtility with a wrapper, or manual parsing
                // For simplicity, use a regex-free approach with JsonUtility
                string wrappedJson = "{\"items\":" + json + "}";

                // We'll use a simple approach: parse manually
                // Nominatim returns a JSON array
                int idx = 0;
                while (true)
                {
                    int objStart = json.IndexOf('{', idx);
                    if (objStart < 0) break;

                    int objEnd = FindMatchingBrace(json, objStart);
                    if (objEnd < 0) break;

                    string objJson = json.Substring(objStart, objEnd - objStart + 1);

                    var result = new NominatimResult
                    {
                        place_id = ExtractStringValue(objJson, "place_id"),
                        display_name = ExtractStringValue(objJson, "display_name"),
                        lon = ExtractDoubleValue(objJson, "lon"),
                        lat = ExtractDoubleValue(objJson, "lat")
                    };

                    results.Add(result);
                    idx = objEnd + 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocationSearch] JSON parse error: {e.Message}");
            }

            return results;
        }

        private int FindMatchingBrace(string json, int start)
        {
            int depth = 0;
            bool inString = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                if (!inString)
                {
                    if (c == '{') depth++;
                    if (c == '}') { depth--; if (depth == 0) return i; }
                }
            }
            return -1;
        }

        private string ExtractStringValue(string obj, string key)
        {
            string searchKey = $"\"{key}\":\"";
            int start = obj.IndexOf(searchKey);
            if (start < 0) return "";
            start += searchKey.Length;
            int end = obj.IndexOf('"', start);
            if (end < 0) return "";
            return obj.Substring(start, end - start);
        }

        private double ExtractDoubleValue(string obj, string key)
        {
            string searchKey = $"\"{key}\":";
            int start = obj.IndexOf(searchKey);
            if (start < 0) return 0;
            start += searchKey.Length;
            int end = start;
            while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '.' || obj[end] == '-' || obj[end] == 'e' || obj[end] == 'E' || obj[end] == '+'))
                end++;
            string val = obj.Substring(start, end - start).Trim();
            double.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }

        // -------------------------------------------------------------------
        // Private: Bookmark Persistence
        // -------------------------------------------------------------------
        private void LoadBookmarks()
        {
            bookmarkData = new BookmarkList();

            if (File.Exists(bookmarksPath))
            {
                try
                {
                    string json = File.ReadAllText(bookmarksPath);
                    string wrappedJson = "{\"bookmarks\":" + json + "}";
                    bookmarkData = JsonUtility.FromJson<BookmarkList>(wrappedJson);
                    if (bookmarkData == null)
                        bookmarkData = new BookmarkList();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LocationSearch] Failed to load bookmarks: {e.Message}");
                    bookmarkData = new BookmarkList();
                }
            }
        }

        private void SaveBookmarks()
        {
            try
            {
                string json = JsonUtility.ToJson(bookmarkData, true);
                // Extract just the array
                int start = json.IndexOf("\"bookmarks\":") + 12;
                int end = json.LastIndexOf('}');
                string arrayJson = json.Substring(start, end - start).Trim();
                File.WriteAllText(bookmarksPath, arrayJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocationSearch] Failed to save bookmarks: {e.Message}");
            }
        }
    }
}
