using UnityEngine;

namespace DynastyVR.Vehicle
{
    /// <summary>
    /// DynastyVR 车载HUD
    /// 显示速度、位置、海拔、距离、指南针等信息
    /// </summary>
    public class VehicleHUD : MonoBehaviour
    {
        [Header("=== HUD 设置 ===")]
        [Tooltip("是否显示HUD")]
        public bool showHUD = true;

        [Tooltip("HUD缩放倍率")]
        public float hudScale = 1f;

        [Header("=== 颜色方案 ===")]
        public Color primaryColor = new Color(0.2f, 0.8f, 1f, 0.9f);      // 科技蓝
        public Color accentColor = new Color(0f, 1f, 0.6f, 0.9f);           // 青绿
        public Color warningColor = new Color(1f, 0.8f, 0f, 0.9f);          // 黄色警告
        public Color dangerColor = new Color(1f, 0.3f, 0.3f, 0.9f);         // 红色危险
        public Color bgColor = new Color(0f, 0f, 0f, 0.5f);                 // 半透明黑背景

        [Header("=== 引用 ===")]
        public DrivingMode drivingMode;
        public RouteNavigator navigator;

        // 样式
        private GUIStyle speedStyle;
        private GUIStyle infoStyle;
        private GUIStyle titleStyle;
        private GUIStyle waypointStyle;
        private bool stylesInitialized = false;

        // 指南针
        private float cameraHeading = 0f; // 摄像头朝向 (0-360)

        private void Start()
        {
            if (drivingMode == null)
                drivingMode = DrivingMode.Instance;
            if (navigator == null)
                navigator = RouteNavigator.Instance;
        }

        private void Update()
        {
            if (!showHUD) return;
            if (drivingMode == null || drivingMode.currentMode != DrivingMode.VehicleMode.Driving) return;

            // 更新摄像机朝向（用于指南针）
            if (Camera.main != null)
            {
                cameraHeading = Camera.main.transform.eulerAngles.y;
            }
        }

        private void OnGUI()
        {
            if (!showHUD || drivingMode == null) return;
            if (drivingMode.currentMode != DrivingMode.VehicleMode.Driving) return;

            InitStyles();

            float scale = hudScale;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float baseSize = 20f * scale;
            float largeSize = 36f * scale;
            float hugeSize = 60f * scale;

            // === 左上角：速度和基本信息 ===
            DrawSpeedPanel(scale, baseSize, largeSize, hugeSize);

            // === 左下角：路线信息 ===
            if (navigator != null && navigator.IsRouteLoaded)
            {
                DrawRoutePanel(scale, baseSize);
            }

            // === 右上角：指南针 ===
            DrawCompass(scale, baseSize);

            // === 右下角：途经点信息 ===
            if (navigator != null && navigator.IsRouteLoaded)
            {
                DrawWaypointPanel(scale, baseSize);
            }

            // === 底部中央：途经点提醒横幅 ===
            DrawAlertBanner(scale, largeSize);
        }

        /// <summary>
        /// 速度面板（左上）
        /// </summary>
        private void DrawSpeedPanel(float scale, float baseSize, float largeSize, float hugeSize)
        {
            float x = 20f * scale;
            float y = 20f * scale;
            float panelW = 280f * scale;
            float panelH = 160f * scale;

            // 背景
            GUI.Box(new Rect(x, y, panelW, panelH), "", CreateBoxStyle());

            // 速度值
            float speed = drivingMode.currentSpeed;
            Color speedColor = Mathf.Abs(speed) > 150f ? dangerColor :
                               Mathf.Abs(speed) > 100f ? warningColor : accentColor;

            speedStyle.normal.textColor = speedColor;
            speedStyle.fontSize = (int)(hugeSize);
            speedStyle.fontStyle = FontStyle.Bold;
            speedStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x + 20f * scale, y + 15f * scale, panelW - 40f * scale, hugeSize),
                      $"{Mathf.Abs(speed):F0}", speedStyle);

            // 单位
            infoStyle.normal.textColor = primaryColor;
            infoStyle.fontSize = (int)(baseSize);
            infoStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + panelW - 120f * scale, y + 45f * scale, 100f * scale, baseSize),
                      "km/h", infoStyle);

            // 模式
            infoStyle.alignment = TextAnchor.MiddleLeft;
            infoStyle.normal.textColor = primaryColor;
            GUI.Label(new Rect(x + 20f * scale, y + panelH - baseSize - 15f * scale, panelW, baseSize),
                      drivingMode.GetModeText(), infoStyle);

            // 转向指示
            float steerAngle = drivingMode.currentSteerAngle;
            string steerDir = steerAngle > 2f ? "右转 →" :
                              steerAngle < -2f ? "← 左转" : "— 居中";
            infoStyle.alignment = TextAnchor.MiddleRight;
            infoStyle.normal.textColor = Mathf.Abs(steerAngle) > 10f ? warningColor : primaryColor;
            GUI.Label(new Rect(x + panelW - 140f * scale, y + panelH - baseSize - 15f * scale, 120f * scale, baseSize),
                      steerDir, infoStyle);
        }

        /// <summary>
        /// 路线信息面板（左下）
        /// </summary>
        private void DrawRoutePanel(float scale, float baseSize)
        {
            float x = 20f * scale;
            float y = Screen.height - 180f * scale;
            float panelW = 320f * scale;
            float panelH = 160f * scale;

            GUI.Box(new Rect(x, y, panelW, panelH), "", CreateBoxStyle());

            float lineY = y + 10f * scale;
            float lineH = baseSize + 4f;

            // 当前位置（经纬度）
            if (navigator.CurrentWaypoint != null)
            {
                var wp = navigator.CurrentWaypoint;
                infoStyle.normal.textColor = accentColor;
                infoStyle.fontSize = (int)(baseSize * 0.85f);
                infoStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                          $"📍 {wp.name}", infoStyle);
                lineY += lineH;

                infoStyle.normal.textColor = primaryColor;
                GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                          $"   经度: {wp.lng:F2}°  纬度: {wp.lat:F2}°", infoStyle);
                lineY += lineH;

                // 海拔
                string altStr = wp.alt > 4000f ? "⚠️ 高原" :
                                wp.alt > 3000f ? "🏔️ 高海拔" : "";
                infoStyle.normal.textColor = wp.alt > 4000f ? warningColor : primaryColor;
                GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                          $"   海拔: {wp.alt:F0} m  {altStr}", infoStyle);
                lineY += lineH;
            }

            // 行驶距离
            infoStyle.normal.textColor = accentColor;
            infoStyle.fontSize = (int)(baseSize * 0.9f);
            GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                      $"🛣️ {navigator.GetProgressText()}", infoStyle);
            lineY += lineH;

            // 进度条
            float barX = x + 10f * scale;
            float barW = panelW - 20f * scale;
            float barH = 8f * scale;
            float barY = lineY + 4f * scale;
            GUI.Box(new Rect(barX, barY, barW, barH), "", CreateBarBackgroundStyle());
            if (navigator.RouteProgress > 0.01f)
            {
                GUI.Box(new Rect(barX, barY, barW * navigator.RouteProgress, barH), "", CreateBarFillStyle());
            }
        }

        /// <summary>
        /// 指南针（右上）
        /// </summary>
        private void DrawCompass(float scale, float baseSize)
        {
            float cx = Screen.width - 80f * scale;
            float cy = 80f * scale;
            float radius = 55f * scale;

            // 背景圆
            GUIStyle circleStyle = new GUIStyle();
            circleStyle.normal.background = MakeCircleTexture(128, bgColor);
            GUI.DrawTexture(new Rect(cx - radius, cy - radius, radius * 2, radius * 2), circleStyle.normal.background);

            // 方向标记
            string[] dirs = { "N", "E", "S", "W" };
            float[] angles = { 0, 90, 180, 270 };
            Color[] dirColors = { dangerColor, primaryColor, primaryColor, primaryColor };

            for (int i = 0; i < 4; i++)
            {
                float angle = (angles[i] - cameraHeading) * Mathf.Deg2Rad;
                float tx = cx + Mathf.Sin(angle) * (radius - 20f * scale);
                float ty = cy - Mathf.Cos(angle) * (radius - 20f * scale);

                infoStyle.normal.textColor = dirColors[i];
                infoStyle.fontSize = (int)(baseSize * 0.8f);
                infoStyle.fontStyle = FontStyle.Bold;
                infoStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(tx - 15f * scale, ty - baseSize * 0.5f, 30f * scale, baseSize), dirs[i], infoStyle);
            }

            // 指针（三角形指向北方）
            float northAngle = -cameraHeading * Mathf.Deg2Rad;
            // 这里用文字"N▲"代替复杂三角形绘制
            infoStyle.normal.textColor = dangerColor;
            infoStyle.fontSize = (int)(baseSize * 1.5f);
            infoStyle.fontStyle = FontStyle.Bold;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(cx - 20f * scale, cy - baseSize * 0.3f, 40f * scale, baseSize * 1.2f), "▲", infoStyle);

            // 当前朝向度数
            infoStyle.normal.textColor = accentColor;
            infoStyle.fontSize = (int)(baseSize * 0.7f);
            GUI.Label(new Rect(cx - 25f * scale, cy + radius * 0.3f, 50f * scale, baseSize),
                      $"{cameraHeading:F0}°", infoStyle);
        }

        /// <summary>
        /// 途经点信息面板（右下）
        /// </summary>
        private void DrawWaypointPanel(float scale, float baseSize)
        {
            float x = Screen.width - 320f * scale;
            float y = Screen.height - 130f * scale;
            float panelW = 300f * scale;
            float panelH = 110f * scale;

            GUI.Box(new Rect(x, y, panelW, panelH), "", CreateBoxStyle());

            float lineY = y + 8f * scale;
            float lineH = baseSize + 4f;

            // 下一站
            infoStyle.normal.textColor = accentColor;
            infoStyle.fontSize = (int)(baseSize);
            infoStyle.fontStyle = FontStyle.Bold;
            infoStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                      $"🎯 下一站: {navigator.GetNextWaypointName()}", infoStyle);
            lineY += lineH;

            // 距离下一站
            float distToNext = navigator.GetDistanceToNextWaypoint();
            string distStr = distToNext > 1000f ? $"{distToNext / 1000f:F1} km" : $"{distToNext:F0} m";
            infoStyle.normal.textColor = primaryColor;
            infoStyle.fontSize = (int)(baseSize * 0.85f);
            infoStyle.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                      $"   距离: {distStr}", infoStyle);
            lineY += lineH;

            // 途经点进度
            GUI.Label(new Rect(x + 10f * scale, lineY, panelW - 20f * scale, lineH),
                      $"   途经点: {navigator.CurrentWaypointIndex + 1}/{navigator.RoutePoints.Count}", infoStyle);
        }

        /// <summary>
        /// 途经点提醒横幅（底部中央）
        /// </summary>
        private float bannerTimer = 0f;
        private string bannerText = "";
        private Color bannerColor = accentColor;

        public void ShowBanner(string text, Color color)
        {
            bannerText = text;
            bannerColor = color;
            bannerTimer = 3f; // 显示3秒
        }

        private void DrawAlertBanner(float scale, float largeSize)
        {
            if (bannerTimer <= 0f) return;

            bannerTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(bannerTimer);
            Color c = bannerColor;
            c.a = alpha;

            GUIStyle bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(largeSize),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bannerStyle.normal.textColor = c;

            // 半透明背景条
            GUIStyle bannerBg = new GUIStyle(GUI.skin.box);
            bannerBg.normal.background = MakePixelTexture(new Color(0, 0, 0, alpha * 0.6f));

            float bannerW = Screen.width * 0.8f;
            float bannerH = largeSize * 1.8f;
            float bx = (Screen.width - bannerW) / 2f;
            float by = Screen.height * 0.7f;

            GUI.Box(new Rect(bx, by, bannerW, bannerH), "", bannerBg);
            GUI.Label(new Rect(bx, by, bannerW, bannerH), bannerText, bannerStyle);
        }

        // === 样式工具 ===

        private void InitStyles()
        {
            if (stylesInitialized) return;

            speedStyle = new GUIStyle(GUI.skin.label);
            infoStyle = new GUIStyle(GUI.skin.label);
            titleStyle = new GUIStyle(GUI.skin.label);
            waypointStyle = new GUIStyle(GUI.skin.label);

            stylesInitialized = true;
        }

        private GUIStyle CreateBoxStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakePixelTexture(bgColor);
            style.padding = new RectOffset(8, 8, 8, 8);
            return style;
        }

        private GUIStyle CreateBarBackgroundStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakePixelTexture(new Color(0.2f, 0.2f, 0.3f, 0.8f));
            return style;
        }

        private GUIStyle CreateBarFillStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakePixelTexture(accentColor);
            return style;
        }

        private Texture2D MakePixelTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCircleTexture(int size, Color color)
        {
            Texture2D tex = new Texture2D(size, size);
            float center = size / 2f;
            float radiusSq = center * center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 公共方法：显示途经点到达通知
        /// </summary>
        public void NotifyWaypointReached(string waypointName)
        {
            ShowBanner($"✅ 到达: {waypointName}", accentColor);
        }

        /// <summary>
        /// 公共方法：显示下一段路提示
        /// </summary>
        public void NotifyNextSegment(string segmentInfo)
        {
            ShowBanner($"📍 {segmentInfo}", primaryColor);
        }

        /// <summary>
        /// 公共方法：显示警告
        /// </summary>
        public void ShowWarning(string warning)
        {
            ShowBanner($"⚠️ {warning}", warningColor);
        }
    }
}
