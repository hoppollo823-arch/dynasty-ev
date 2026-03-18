// ============================================================================
// DynastyVR Earth — Route Display System
// 路线叠加 / 站点标记 / 自贡→班公湖
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynastyVR.Earth.Route
{
    /// <summary>
    /// Draws 3D routes on the Cesium globe using LineRenderer or GL lines.
    /// Loads route data from predefined routes or external JSON.
    /// </summary>
    public class RouteDisplay : MonoBehaviour
    {
        // -------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------
        public static RouteDisplay Instance { get; private set; }

        // -------------------------------------------------------------------
        // Data Types
        // -------------------------------------------------------------------
        [Serializable]
        public class RoutePoint
        {
            public double longitude;
            public double latitude;
            public double altitude; // meters above sea level
            public string name;
            public string description;
            public bool isWaypoint; // true = major stop, false = intermediate
        }

        [Serializable]
        public class Route
        {
            public string routeName;
            public string routeDescription;
            public Color color = Color.red;
            public float lineWidth = 5f;
            public List<RoutePoint> points = new List<RoutePoint>();
        }

        // -------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------
        [Header("Line Rendering")]
        [Tooltip("Shader used for route lines")]
        public Shader lineShader;

        [Tooltip("Default route line width (screen pixels)")]
        public float defaultLineWidth = 5f;

        [Tooltip("Altitude offset above terrain (meters)")]
        public float altitudeOffset = 50f;

        [Header("Marker Settings")]
        [Tooltip("Marker prefab (if null, uses built-in sphere)")]
        public GameObject markerPrefab;

        [Tooltip("Waypoint marker scale")]
        public float waypointScale = 500f;

        [Tooltip("Intermediate point marker scale")]
        public float intermediateScale = 200f;

        [Header("Predefined Routes")]
        public List<Route> predefinedRoutes = new List<Route>();

        // -------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------
        private List<GameObject> activeRouteObjects = new List<GameObject>();
        private Route activeRoute;
        private Material lineMaterial;

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

            // Initialize predefined routes if empty
            if (predefinedRoutes.Count == 0)
            {
                InitializePredefinedRoutes();
            }

            CreateLineMaterial();
        }

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Display a route by index.
        /// </summary>
        public void ShowRoute(int routeIndex)
        {
            if (routeIndex < 0 || routeIndex >= predefinedRoutes.Count) return;
            ShowRoute(predefinedRoutes[routeIndex]);
        }

        /// <summary>
        /// Display a route.
        /// </summary>
        public void ShowRoute(Route route)
        {
            ClearActiveRoutes();
            activeRoute = route;

            // Create route line
            CreateRouteLine(route);

            // Create waypoint markers
            foreach (var point in route.points)
            {
                if (point.isWaypoint)
                {
                    CreateWaypointMarker(point, route.color);
                }
            }

            Debug.Log($"[RouteDisplay] Showing route: {route.routeName} ({route.points.Count} points)");
        }

        /// <summary>
        /// Display a route from a list of coordinates.
        /// </summary>
        public void ShowCustomRoute(string name, List<RoutePoint> points, Color color)
        {
            var route = new Route
            {
                routeName = name,
                points = points,
                color = color,
                lineWidth = defaultLineWidth
            };
            ShowRoute(route);
        }

        /// <summary>
        /// Clear all displayed routes.
        /// </summary>
        public void ClearRoutes()
        {
            ClearActiveRoutes();
            activeRoute = null;
        }

        /// <summary>
        /// Get the currently active route.
        /// </summary>
        public Route GetActiveRoute() => activeRoute;

        /// <summary>
        /// Get predefined route count.
        /// </summary>
        public int GetRouteCount() => predefinedRoutes.Count;

        /// <summary>
        /// Get predefined route names.
        /// </summary>
        public List<string> GetRouteNames()
        {
            var names = new List<string>();
            foreach (var route in predefinedRoutes)
                names.Add(route.routeName);
            return names;
        }

        // -------------------------------------------------------------------
        // Private: Route Rendering
        // -------------------------------------------------------------------
        private void CreateRouteLine(Route route)
        {
            // We use a child GameObject with a LineRenderer
            GameObject lineObj = new GameObject($"Route_{route.routeName}");
            lineObj.transform.SetParent(transform);

            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = lineMaterial;
            lineRenderer.startColor = route.color;
            lineRenderer.endColor = route.color;
            lineRenderer.startWidth = route.lineWidth * 0.001f;
            lineRenderer.endWidth = route.lineWidth * 0.001f;
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCornerVertices = 4;

            // Generate intermediate points between waypoints for smooth curves
            List<Vector3> worldPositions = new List<Vector3>();
            for (int i = 0; i < route.points.Count; i++)
            {
                var p = route.points[i];
                Vector3 worldPos = LatLonToUnityPosition(p.longitude, p.latitude, p.altitude + altitudeOffset);
                worldPositions.Add(worldPos);

                // Add intermediate points for better line rendering
                if (i < route.points.Count - 1)
                {
                    var next = route.points[i + 1];
                    int steps = 10; // intermediate segments
                    for (int s = 1; s < steps; s++)
                    {
                        double t = (double)s / steps;
                        double midLon = p.longitude + (next.longitude - p.longitude) * t;
                        double midLat = p.latitude + (next.latitude - p.latitude) * t;
                        double midAlt = p.altitude + (next.altitude - p.altitude) * t;
                        Vector3 midPos = LatLonToUnityPosition(midLon, midLat, midAlt + altitudeOffset);
                        worldPositions.Add(midPos);
                    }
                }
            }

            lineRenderer.positionCount = worldPositions.Count;
            lineRenderer.SetPositions(worldPositions.ToArray());

            activeRouteObjects.Add(lineObj);
        }

        private void CreateWaypointMarker(RoutePoint point, Color color)
        {
            GameObject marker;

            if (markerPrefab != null)
            {
                marker = Instantiate(markerPrefab);
            }
            else
            {
                // Create a simple sphere marker
                marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"Marker_{point.name}";

                // Remove collider for cleanliness
                var col = marker.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Add a text label
                CreateTextLabel(marker, point.name);
            }

            marker.transform.SetParent(transform);
            marker.name = $"Marker_{point.name}";

            // Position on globe
            Vector3 worldPos = LatLonToUnityPosition(
                point.longitude, point.latitude, point.altitude + altitudeOffset);
            marker.transform.position = worldPos;

            // Scale
            float scale = point.isWaypoint ? waypointScale : intermediateScale;
            marker.transform.localScale = Vector3.one * scale;

            // Color
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = color
                };
                renderer.material.SetColor("_EmissionColor", color * 2f);
                renderer.material.EnableKeyword("_EMISSION");
            }

            // Make marker always face camera (billboard)
            var billboard = marker.AddComponent<BillboardFollower>();

            activeRouteObjects.Add(marker);
        }

        private void CreateTextLabel(GameObject parent, string text)
        {
            // Create a simple canvas for the label
            GameObject canvasObj = new GameObject("Label");
            canvasObj.transform.SetParent(parent.transform);
            canvasObj.transform.localPosition = Vector3.up * 1.5f;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.transform.localScale = Vector3.one * 0.01f;

            // Text component
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform);

            var textComp = textObj.AddComponent<UnityEngine.UI.Text>();
            textComp.text = text;
            textComp.fontSize = 36;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Outline for readability
            var outline = textObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, 2);

            // RectTransform
            var rt = textObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 60);
            rt.anchoredPosition = Vector2.zero;
        }

        // -------------------------------------------------------------------
        // Coordinate Conversion
        // -------------------------------------------------------------------

        /// <summary>
        /// Convert geographic coordinates to Unity world position.
        /// Uses CesiumGeoreference as the origin reference.
        /// </summary>
        public static Vector3 LatLonToUnityPosition(double longitude, double latitude, double altitude)
        {
            // Convert to ECEF (Earth-Centered, Earth-Fixed)
            // WGS84 ellipsoid constants
            const double a = 6378137.0;           // semi-major axis
            const double f = 1.0 / 298.257223563;  // flattening
            const double e2 = 2 * f - f * f;       // eccentricity squared

            double latRad = latitude * Math.PI / 180.0;
            double lonRad = longitude * Math.PI / 180.0;

            double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));

            double x = (N + altitude) * Math.Cos(latRad) * Math.Cos(lonRad);
            double y = (N + altitude) * Math.Cos(latRad) * Math.Sin(lonRad);
            double z = (N * (1 - e2) + altitude) * Math.Sin(latRad);

            // Convert to Unity coordinate system (Y-up)
            // ECEF: X = east, Y = north (kind of), Z = up
            // Unity: X = east, Y = up, Z = north
            // Apply georeference offset
            var georef = CesiumEarthManager.Instance?.georeference;
            if (georef != null)
            {
                // Cesium uses ENU at the georeference origin
                double originLon = georef.longitude;
                double originLat = georef.latitude;
                double originAlt = georef.height;

                double originLatRad = originLat * Math.PI / 180.0;
                double originLonRad = originLon * Math.PI / 180.0;

                double N0 = a / Math.Sqrt(1 - e2 * Math.Sin(originLatRad) * Math.Sin(originLatRad));
                double originX = (N0 + originAlt) * Math.Cos(originLatRad) * Math.Cos(originLonRad);
                double originY = (N0 + originAlt) * Math.Cos(originLatRad) * Math.Sin(originLonRad);
                double originZ = (N0 * (1 - e2) + originAlt) * Math.Sin(originLatRad);

                // Rotation matrix ENU -> Unity
                double sinLat = Math.Sin(originLatRad);
                double cosLat = Math.Cos(originLatRad);
                double sinLon = Math.Sin(originLonRad);
                double cosLon = Math.Cos(originLonRad);

                // ENU components
                double dx = x - originX;
                double dy = y - originY;
                double dz = z - originZ;

                // ENU coordinates
                double east = -sinLon * dx + cosLon * dy;
                double north = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz;
                double up = cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;

                // Unity: X = East, Y = Up, Z = North
                return new Vector3((float)east, (float)up, (float)north);
            }

            // Fallback: direct conversion (less accurate away from origin)
            return new Vector3((float)(x * 0.001), (float)(z * 0.001), (float)(y * 0.001));
        }

        // -------------------------------------------------------------------
        // Predefined Routes
        // -------------------------------------------------------------------
        private void InitializePredefinedRoutes()
        {
            // Route 1: 自贡 → 班公湖 (Zigong to Pangong Lake)
            var zigongToPangong = new Route
            {
                routeName = "自贡 → 班公湖 (Zigong → Pangong Lake)",
                routeDescription = "穿越川藏线，从自贡恐龙之乡到班公湖高原明珠",
                color = Color.red,
                lineWidth = 6f,
                points = new List<RoutePoint>
                {
                    new RoutePoint { longitude = 104.7734, latitude = 29.3523, altitude = 300,
                                     name = "自贡 Zigong", description = "恐龙之乡", isWaypoint = true },
                    new RoutePoint { longitude = 104.0665, latitude = 30.5728, altitude = 500,
                                     name = "成都 Chengdu", description = "天府之国", isWaypoint = true },
                    new RoutePoint { longitude = 102.2678, latitude = 30.0370, altitude = 600,
                                     name = "雅安 Ya'an", description = "雨城", isWaypoint = false },
                    new RoutePoint { longitude = 100.0456, latitude = 29.6570, altitude = 2500,
                                     name = "康定 Kangding", description = "情歌城", isWaypoint = true },
                    new RoutePoint { longitude = 99.3010, latitude = 29.9985, altitude = 3900,
                                     name = "理塘 Litang", description = "世界高城", isWaypoint = true },
                    new RoutePoint { longitude = 98.5820, latitude = 29.6570, altitude = 3600,
                                     name = "昌都 Qamdo", description = "藏东明珠", isWaypoint = false },
                    new RoutePoint { longitude = 97.1714, latitude = 29.6520, altitude = 3800,
                                     name = "八宿 Basu", description = "然乌湖畔", isWaypoint = false },
                    new RoutePoint { longitude = 95.7710, latitude = 29.8600, altitude = 3600,
                                     name = "波密 Bomi", description = "冰川之乡", isWaypoint = false },
                    new RoutePoint { longitude = 94.3625, latitude = 29.6540, altitude = 2900,
                                     name = "林芝 Nyingchi", description = "西藏江南", isWaypoint = true },
                    new RoutePoint { longitude = 93.7800, latitude = 29.3200, altitude = 3500,
                                     name = "米林 Mainling", description = "雅鲁藏布江", isWaypoint = false },
                    new RoutePoint { longitude = 91.1580, latitude = 29.6500, altitude = 3650,
                                     name = "拉萨 Lhasa", description = "圣城", isWaypoint = true },
                    new RoutePoint { longitude = 89.2600, latitude = 29.6500, altitude = 4200,
                                     name = "日喀则 Shigatse", description = "后藏中心", isWaypoint = true },
                    new RoutePoint { longitude = 86.9250, latitude = 29.0000, altitude = 5200,
                                     name = "仲巴 Zhongba", description = "高原荒漠", isWaypoint = false },
                    new RoutePoint { longitude = 83.5000, latitude = 31.5000, altitude = 4500,
                                     name = "噶尔 Ali (Shiquanhe)", description = "阿里地区", isWaypoint = true },
                    new RoutePoint { longitude = 79.8000, latitude = 32.8000, altitude = 4250,
                                     name = "日土 Rutog", description = "班公湖东岸", isWaypoint = false },
                    new RoutePoint { longitude = 78.7375, latitude = 33.7167, altitude = 4250,
                                     name = "班公湖 Pangong Lake", description = "高原明珠，中印边境湖", isWaypoint = true },
                }
            };

            // Route 2: 上海 → 班公湖 (Shanghai to Pangong Lake)
            var shanghaiToPangong = new Route
            {
                routeName = "上海 → 班公湖 (Shanghai → Pangong Lake)",
                routeDescription = "从东海之滨到西部边陲",
                color = Color.cyan,
                lineWidth = 5f,
                points = new List<RoutePoint>
                {
                    new RoutePoint { longitude = 121.4737, latitude = 31.2304, altitude = 10,
                                     name = "上海 Shanghai", isWaypoint = true },
                    new RoutePoint { longitude = 118.7969, latitude = 32.0603, altitude = 20,
                                     name = "南京 Nanjing", isWaypoint = true },
                    new RoutePoint { longitude = 112.9388, latitude = 28.2282, altitude = 50,
                                     name = "长沙 Changsha", isWaypoint = true },
                    new RoutePoint { longitude = 106.5516, latitude = 29.5630, altitude = 250,
                                     name = "重庆 Chongqing", isWaypoint = true },
                    new RoutePoint { longitude = 103.8577, latitude = 30.0561, altitude = 500,
                                     name = "成都 Chengdu", isWaypoint = true },
                    new RoutePoint { longitude = 100.0456, latitude = 29.6570, altitude = 2500,
                                     name = "康定 Kangding", isWaypoint = false },
                    new RoutePoint { longitude = 91.1580, latitude = 29.6500, altitude = 3650,
                                     name = "拉萨 Lhasa", isWaypoint = true },
                    new RoutePoint { longitude = 81.3061, latitude = 31.1667, altitude = 5000,
                                     name = "冈仁波齐 Mt. Kailash", isWaypoint = true },
                    new RoutePoint { longitude = 78.7375, latitude = 33.7167, altitude = 4250,
                                     name = "班公湖 Pangong Lake", isWaypoint = true },
                }
            };

            // Route 3: Belt & Road mini-segment
            var beltRoad = new Route
            {
                routeName = "丝路古道 Ancient Silk Road",
                routeDescription = "长安 → 西域 (部分)",
                color = Color.yellow,
                lineWidth = 4f,
                points = new List<RoutePoint>
                {
                    new RoutePoint { longitude = 108.9426, latitude = 34.3416, altitude = 400,
                                     name = "西安 Xi'an", isWaypoint = true },
                    new RoutePoint { longitude = 100.4498, latitude = 36.0672, altitude = 2200,
                                     name = "兰州 Lanzhou", isWaypoint = true },
                    new RoutePoint { longitude = 94.9084, latitude = 40.1421, altitude = 1139,
                                     name = "敦煌 Dunhuang", isWaypoint = true },
                    new RoutePoint { longitude = 89.1895, latitude = 42.9513, altitude = 735,
                                     name = "吐鲁番 Turpan", isWaypoint = true },
                    new RoutePoint { longitude = 75.8564, latitude = 39.4547, altitude = 1289,
                                     name = "喀什 Kashgar", isWaypoint = true },
                }
            };

            predefinedRoutes.Add(zigongToPangong);
            predefinedRoutes.Add(shanghaiToPangong);
            predefinedRoutes.Add(beltRoad);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private void CreateLineMaterial()
        {
            // Use built-in Unlit/Color as fallback, or custom shader
            Shader shader = lineShader;
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null)
            {
                lineMaterial = new Material(shader);
                lineMaterial.color = Color.red;
            }
            else
            {
                // Ultimate fallback
                lineMaterial = new Material(Shader.Find("Sprites/Default"));
                lineMaterial.color = Color.red;
            }
        }

        private void ClearActiveRoutes()
        {
            foreach (var obj in activeRouteObjects)
            {
                if (obj != null) Destroy(obj);
            }
            activeRouteObjects.Clear();
        }
    }

    // =========================================================================
    // Billboard Follower — makes markers face the camera
    // =========================================================================
    public class BillboardFollower : MonoBehaviour
    {
        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
        }

        private void LateUpdate()
        {
            if (mainCam != null)
            {
                transform.LookAt(mainCam.transform);
                transform.Rotate(0, 180, 0); // Flip so text reads correctly
            }
        }
    }
}
