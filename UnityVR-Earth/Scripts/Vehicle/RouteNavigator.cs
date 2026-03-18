using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace DynastyVR.Vehicle
{
    /// <summary>
    /// 路线导航系统
    /// 读取JSON路线文件，自动计算行驶进度，途经点提醒
    /// </summary>
    public class RouteNavigator : MonoBehaviour
    {
        [Header("=== 路线配置 ===")]
        [Tooltip("路线JSON文件名（放在 Data/Route/ 下）")]
        public string routeFileName = "zigong_to_bangong.json";

        [Tooltip("是否在Start时自动加载路线")]
        public bool autoLoadOnStart = true;

        [Header("=== 途经点提醒 ===")]
        [Tooltip("途经点提醒距离 (m)")]
        public float waypointAlertDistance = 500f;

        [Tooltip("到达途经点判定距离 (m)")]
        public float waypointArriveDistance = 100f;

        [Tooltip("下一段路提示距离 (m)")]
        public float nextSegmentAlertDistance = 2000f;

        [Header("=== 事件 ===")]
        public UnityEvent<string> OnWaypointReached;     // 途经点名称
        public UnityEvent<string> OnNextSegmentAlert;     // 下一段路描述
        public UnityEvent OnRouteLoaded;
        public UnityEvent OnRouteCompleted;

        // 路线数据
        [Serializable]
        public class RoutePoint
        {
            public string name;
            public double lng;  // 经度
            public double lat;  // 纬度
            public float alt;   // 海拔 (m)
        }

        [Serializable]
        public class RouteData
        {
            public List<RoutePoint> points = new List<RoutePoint>();
        }

        // 运行时状态
        public bool IsRouteLoaded { get; private set; }
        public bool HasReachedEnd { get; private set; }
        public int CurrentWaypointIndex { get; private set; }
        public float TotalDistance { get; private set; }
        public float TraveledDistance { get; private set; }
        public float RouteProgress => TotalDistance > 0 ? TraveledDistance / TotalDistance : 0f;

        public List<RoutePoint> RoutePoints { get; private set; } = new List<RoutePoint>();

        // 当前途经点
        public RoutePoint CurrentWaypoint =>
            (CurrentWaypointIndex >= 0 && CurrentWaypointIndex < RoutePoints.Count)
            ? RoutePoints[CurrentWaypointIndex] : null;

        // 下一个途经点
        public RoutePoint NextWaypoint =>
            (CurrentWaypointIndex + 1 < RoutePoints.Count)
            ? RoutePoints[CurrentWaypointIndex + 1] : null;

        // 私有状态
        private float[] segmentDistances;  // 每段路程长度
        private float[] cumulativeDistances; // 累计路程长度
        private int lastReachedWaypoint = -1;
        private int lastAlertedSegment = -1;

        // 地球半径 (m)
        private const double EarthRadius = 6371000.0;

        private static RouteNavigator _instance;
        public static RouteNavigator Instance => _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            if (autoLoadOnStart)
            {
                LoadRoute(routeFileName);
            }
        }

        /// <summary>
        /// 加载路线JSON文件
        /// </summary>
        public void LoadRoute(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Data", "Route", fileName);

            // 备用路径：StreamingAssets
            if (!File.Exists(path))
            {
                path = Path.Combine(Application.streamingAssetsPath, "Data", "Route", fileName);
            }

            // 再备用：Resources
            if (!File.Exists(path))
            {
                LoadRouteFromResources(fileName);
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                ParseRouteJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RouteNavigator] 加载路线失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从Resources加载路线
        /// </summary>
        private void LoadRouteFromResources(string fileName)
        {
            string resourcePath = "Route/" + Path.GetFileNameWithoutExtension(fileName);
            TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

            if (jsonAsset != null)
            {
                ParseRouteJson(jsonAsset.text);
            }
            else
            {
                Debug.LogError($"[RouteNavigator] 找不到路线文件: {fileName}");
            }
        }

        /// <summary>
        /// 解析路线JSON
        /// </summary>
        private void ParseRouteJson(string json)
        {
            // 支持两种格式：直接数组 或 带points字段的对象
            try
            {
                // 尝试直接反序列化为数组
                RouteData data = JsonUtility.FromJson<RouteData>("{\"points\":" + json + "}");
                if (data.points.Count > 0)
                {
                    RoutePoints = data.points;
                }
            }
            catch
            {
                Debug.LogError("[RouteNavigator] JSON格式错误");
                return;
            }

            if (RoutePoints.Count < 2)
            {
                Debug.LogError("[RouteNavigator] 路线至少需要2个点");
                return;
            }

            CalculateDistances();
            CurrentWaypointIndex = 0;
            lastReachedWaypoint = -1;
            lastAlertedSegment = -1;
            HasReachedEnd = false;
            IsRouteLoaded = true;

            Debug.Log($"[RouteNavigator] 路线已加载: {RoutePoints.Count}个途经点, 总距离: {TotalDistance / 1000f:F1} km");
            OnRouteLoaded?.Invoke();

            // 提示出发
            if (RoutePoints.Count > 0)
            {
                Debug.Log($"[RouteNavigator] 🚩 起点: {RoutePoints[0].name}");
                OnNextSegmentAlert?.Invoke($"从 {RoutePoints[0].name} 出发，前往 {RoutePoints[1].name}");
            }
        }

        /// <summary>
        /// 计算各段距离和总距离
        /// </summary>
        private void CalculateDistances()
        {
            int count = RoutePoints.Count;
            segmentDistances = new float[count - 1];
            cumulativeDistances = new float[count];
            cumulativeDistances[0] = 0f;
            TotalDistance = 0f;

            for (int i = 0; i < count - 1; i++)
            {
                float dist = CalculateDistance(
                    RoutePoints[i].lat, RoutePoints[i].lng,
                    RoutePoints[i + 1].lat, RoutePoints[i + 1].lng
                );
                segmentDistances[i] = dist;
                TotalDistance += dist;
                cumulativeDistances[i + 1] = TotalDistance;
            }
        }

        /// <summary>
        /// 用Haversine公式计算两个经纬度点之间的距离
        /// </summary>
        public static float CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return (float)(EarthRadius * c);
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

        /// <summary>
        /// 更新行驶进度（由DrivingMode调用）
        /// </summary>
        public void UpdateProgress(Vector3 vehicleWorldPosition)
        {
            if (!IsRouteLoaded || HasReachedEnd) return;

            // 获取当前途经点和下一个途经点的世界坐标
            // 注意：在Unity+Cesium中需要将经纬度转换为世界坐标
            // 这里使用简化的计算：假设车辆大致沿着路线行驶

            // 检查是否接近当前目标途经点
            RoutePoint targetWP = CurrentWaypoint;
            if (targetWP == null) return;

            // 将途经点经纬度转换为Unity世界坐标（简化版，实际应使用Cesium的转换）
            Vector3 wpWorldPos = GeoToWorld(targetWP.lat, targetWP.lng, targetWP.alt);
            float distToTarget = Vector3.Distance(vehicleWorldPosition, wpWorldPos);

            // 检查是否到达途经点
            if (distToTarget < waypointArriveDistance)
            {
                ReachWaypoint();
            }
            else
            {
                // 检查是否接近途经点（提醒）
                if (distToTarget < waypointAlertDistance && CurrentWaypointIndex != lastReachedWaypoint)
                {
                    AlertWaypointApproaching(targetWP, distToTarget);
                }

                // 检查是否需要提示下一段路
                if (NextWaypoint != null && CurrentWaypointIndex != lastAlertedSegment)
                {
                    Vector3 nextWpWorldPos = GeoToWorld(NextWaypoint.lat, NextWaypoint.lng, NextWaypoint.alt);
                    float distToNext = Vector3.Distance(vehicleWorldPosition, nextWpWorldPos);
                    if (distToNext < nextSegmentAlertDistance)
                    {
                        lastAlertedSegment = CurrentWaypointIndex;
                        string alert = $"即将进入下一段: → {NextWaypoint.name}";
                        Debug.Log($"[RouteNavigator] 📢 {alert}");
                        OnNextSegmentAlert?.Invoke(alert);
                    }
                }
            }

            // 计算已行驶距离（简化：基于当前途经点的累计距离 + 当前到目标点的距离）
            float traveledInSegment = TotalDistance - cumulativeDistances[CurrentWaypointIndex];
            if (CurrentWaypointIndex > 0)
            {
                traveledInSegment = cumulativeDistances[CurrentWaypointIndex];
            }
            // 加上当前段的进度
            if (CurrentWaypointIndex < segmentDistances.Length && segmentDistances[CurrentWaypointIndex] > 0)
            {
                float segProgress = 1f - Mathf.Clamp01(distToTarget / segmentDistances[CurrentWaypointIndex]);
                TraveledDistance = cumulativeDistances[CurrentWaypointIndex] + segmentDistances[CurrentWaypointIndex] * segProgress;
            }
        }

        /// <summary>
        /// 到达途经点
        /// </summary>
        private void ReachWaypoint()
        {
            RoutePoint wp = CurrentWaypoint;
            lastReachedWaypoint = CurrentWaypointIndex;

            Debug.Log($"[RouteNavigator] ✅ 到达途经点: {wp.name} ({wp.lat:F2}, {wp.lng:F2}, 海拔{wp.alt:F0}m)");
            OnWaypointReached?.Invoke(wp.name);

            // 移动到下一个途经点
            CurrentWaypointIndex++;

            if (CurrentWaypointIndex >= RoutePoints.Count - 1)
            {
                // 到达终点
                HasReachedEnd = true;
                CurrentWaypointIndex = RoutePoints.Count - 1;
                TraveledDistance = TotalDistance;

                Debug.Log($"[RouteNavigator] 🏁 已到达终点: {wp.name}！");
                OnRouteCompleted?.Invoke();
            }
            else if (CurrentWaypointIndex < RoutePoints.Count)
            {
                RoutePoint next = RoutePoints[CurrentWaypointIndex];
                Debug.Log($"[RouteNavigator] 📍 下一站: {next.name} ({segmentDistances[CurrentWaypointIndex] / 1000f:F1} km)");
                OnNextSegmentAlert?.Invoke($"下一站: {next.name}");
            }
        }

        /// <summary>
        /// 提醒接近途经点
        /// </summary>
        private void AlertWaypointApproaching(RoutePoint wp, float distance)
        {
            string unit = distance > 1000f ? "km" : "m";
            float dist = distance > 1000f ? distance / 1000f : distance;

            Debug.Log($"[RouteNavigator] ⚠️ 接近途经点: {wp.name} ({dist:F1} {unit})");
            OnWaypointReached?.Invoke($"接近: {wp.name}");
        }

        /// <summary>
        /// 经纬度+海拔 → Unity世界坐标（简化转换）
        /// 实际项目中应使用 CesiumGeoreference 的转换方法
        /// </summary>
        public static Vector3 GeoToWorld(double lat, double lon, float alt)
        {
            // 简化：以经纬度映射到平面坐标
            // 实际Unity+Cesium项目应用 Cesium坐标系
            float x = (float)(lon * 111320f * Math.Cos(DegreesToRadians(lat)));
            float z = (float)(lat * 110540f);
            float y = alt;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// 经纬度 → 方位角（用于指南针）
        /// </summary>
        public static float GetBearing(double lat1, double lon1, double lat2, double lon2)
        {
            double dLon = DegreesToRadians(lon2 - lon1);
            double lat1R = DegreesToRadians(lat1);
            double lat2R = DegreesToRadians(lat2);

            double y = Math.Sin(dLon) * Math.Cos(lat2R);
            double x = Math.Cos(lat1R) * Math.Sin(lat2R) -
                       Math.Sin(lat1R) * Math.Cos(lat2R) * Math.Cos(dLon);

            double bearing = RadiansToDegrees(Math.Atan2(y, x));
            return (float)((bearing + 360) % 360);
        }

        /// <summary>
        /// 获取当前朝向下一个途经点的方位角
        /// </summary>
        public float GetDirectionToNextWaypoint()
        {
            if (!IsRouteLoaded || CurrentWaypoint == null || NextWaypoint == null)
                return 0f;

            return GetBearing(
                CurrentWaypoint.lat, CurrentWaypoint.lng,
                NextWaypoint.lat, NextWaypoint.lng
            );
        }

        /// <summary>
        /// 获取距离下一个途经点的距离
        /// </summary>
        public float GetDistanceToNextWaypoint()
        {
            if (!IsRouteLoaded || CurrentWaypoint == null || NextWaypoint == null)
                return 0f;

            return CalculateDistance(
                CurrentWaypoint.lat, CurrentWaypoint.lng,
                NextWaypoint.lat, NextWaypoint.lng
            );
        }

        /// <summary>
        /// 获取当前途经点名称
        /// </summary>
        public string GetCurrentWaypointName()
        {
            return CurrentWaypoint?.name ?? "未命名";
        }

        /// <summary>
        /// 获取下一个途经点名称
        /// </summary>
        public string GetNextWaypointName()
        {
            return NextWaypoint?.name ?? "终点";
        }

        /// <summary>
        /// 获取路线进度文本
        /// </summary>
        public string GetProgressText()
        {
            return $"{TraveledDistance / 1000f:F1} / {TotalDistance / 1000f:F1} km ({RouteProgress * 100f:F0}%)";
        }

        /// <summary>
        /// 获取途经点列表文本
        /// </summary>
        public string GetWaypointListText()
        {
            string text = $"路线共 {RoutePoints.Count} 个途经点:\n";
            for (int i = 0; i < RoutePoints.Count; i++)
            {
                string marker = i < CurrentWaypointIndex ? "✅" :
                                i == CurrentWaypointIndex ? "📍" : "⬜";
                text += $"  {marker} {RoutePoints[i].name}\n";
            }
            return text;
        }
    }
}
