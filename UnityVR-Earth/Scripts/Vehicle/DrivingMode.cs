using UnityEngine;
using UnityEngine.Events;

namespace DynastyVR.Vehicle
{
    /// <summary>
    /// DynastyVR 自驾模式控制器
    /// 切换飞行/自驾模式，控制车辆沿路线自动行驶
    /// 操作：W/S 加速减速，A/D 转向
    /// </summary>
    public class DrivingMode : MonoBehaviour
    {
        [Header("=== 模式切换 ===")]
        [Tooltip("初始是否处于自驾模式")]
        public bool startInDrivingMode = true;

        [Tooltip("切换模式按键")]
        public KeyCode toggleModeKey = KeyCode.Tab;

        public enum VehicleMode { Flying, Driving }
        public VehicleMode currentMode = VehicleMode.Flying;

        [Header("=== 速度控制 ===")]
        [Tooltip("当前车速 (km/h)")]
        public float currentSpeed = 0f;

        public float maxSpeed = 200f;
        public float minSpeed = 0f;
        [Tooltip("加速速率 (km/h/s)")]
        public float acceleration = 40f;
        [Tooltip("刹车速率 (km/h/s)")]
        public float brakingForce = 80f;
        [Tooltip("自然减速 (km/h/s)")]
        public float frictionDeceleration = 10f;

        [Header("=== 转向控制 ===")]
        [Tooltip("转向灵敏度 (度/秒)")]
        public float steerSensitivity = 90f;
        [Tooltip("当前转向角 (度)")]
        public float currentSteerAngle = 0f;
        [Tooltip("最大转向角")]
        public float maxSteerAngle = 35f;
        [Tooltip("转向回正速率")]
        public float steerReturnSpeed = 120f;

        [Header("=== 自动驾驶 ===")]
        [Tooltip("自动驾驶目标速度 (km/h)")]
        public float autoPilotSpeed = 80f;
        [Tooltip("是否启用自动驾驶")]
        public bool enableAutoPilot = true;

        [Header("=== 引用 ===")]
        [Tooltip("路线导航器（可选）")]
        public RouteNavigator navigator;

        [Tooltip("车辆模型Transform（用于旋转）")]
        public Transform vehicleModel;

        // 事件
        [Header("=== 事件 ===")]
        public UnityEvent OnModeSwitchToDriving;
        public UnityEvent OnModeSwitchToFlying;
        public UnityEvent OnVehicleStart;
        public UnityEvent OnVehicleStop;

        // 私有状态
        private bool isAccelerating = false;
        private bool isBraking = false;
        private float steerInput = 0f;
        private Vector3 lastPosition;
        private float movingDirection = 1f; // 1=前进, -1=后退

        // 单例访问（供HUD等使用）
        private static DrivingMode _instance;
        public static DrivingMode Instance => _instance;

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            lastPosition = transform.position;

            if (startInDrivingMode)
            {
                SwitchToDrivingMode();
            }
            else
            {
                SwitchToFlyingMode();
            }
        }

        private void Update()
        {
            // 模式切换
            if (Input.GetKeyDown(toggleModeKey))
            {
                ToggleMode();
            }

            if (currentMode == VehicleMode.Driving)
            {
                HandleInput();
                UpdateSpeed(Time.deltaTime);
                UpdateRotation(Time.deltaTime);
                MoveVehicle(Time.deltaTime);
            }
        }

        /// <summary>
        /// 切换飞行/自驾模式
        /// </summary>
        public void ToggleMode()
        {
            if (currentMode == VehicleMode.Flying)
                SwitchToDrivingMode();
            else
                SwitchToFlyingMode();
        }

        /// <summary>
        /// 切换到自驾模式
        /// </summary>
        public void SwitchToDrivingMode()
        {
            currentMode = VehicleMode.Driving;
            currentSpeed = 0f;
            currentSteerAngle = 0f;

            // 如果有导航器，从导航器获取初始位置
            if (navigator != null && navigator.IsRouteLoaded)
            {
                // 将车辆放置到路线起点
                var startWP = navigator.CurrentWaypoint;
                if (startWP != null)
                {
                    SetVehiclePosition(startWP.Value);
                }
            }

            OnModeSwitchToDriving?.Invoke();
            Debug.Log("[DynastyVR Driving] 已切换到自驾模式 🚗");
        }

        /// <summary>
        /// 切换到飞行模式
        /// </summary>
        public void SwitchToFlyingMode()
        {
            currentMode = VehicleMode.Flying;
            currentSpeed = 0f;
            currentSteerAngle = 0f;
            isAccelerating = false;
            isBraking = false;

            OnModeSwitchToFlying?.Invoke();
            Debug.Log("[DynastyVR Driving] 已切换到飞行模式 ✈️");
        }

        /// <summary>
        /// 处理用户输入
        /// </summary>
        private void HandleInput()
        {
            // W/S 加速减速
            isAccelerating = Input.GetKey(KeyCode.W) || Input.GetAxis("Vertical") > 0.1f;
            isBraking = Input.GetKey(KeyCode.S) || Input.GetAxis("Vertical") < -0.1f;

            // A/D 转向
            steerInput = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetAxis("Horizontal") < -0.1f)
                steerInput = -1f;
            else if (Input.GetKey(KeyCode.D) || Input.GetAxis("Horizontal") > 0.1f)
                steerInput = 1f;
        }

        /// <summary>
        /// 更新速度
        /// </summary>
        private void UpdateSpeed(float deltaTime)
        {
            if (enableAutoPilot && !isAccelerating && !isBraking)
            {
                // 自动驾驶：保持目标速度
                currentSpeed = Mathf.MoveTowards(currentSpeed, autoPilotSpeed, acceleration * deltaTime);
            }
            else if (isAccelerating)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * deltaTime);
                if (currentSpeed > 0) movingDirection = 1f;
            }
            else if (isBraking)
            {
                if (currentSpeed > 1f)
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakingForce * deltaTime);
                }
                else
                {
                    // 低速时可以倒车
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -maxSpeed * 0.3f, acceleration * deltaTime);
                    movingDirection = -1f;
                }
            }
            else
            {
                // 自然减速
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, frictionDeceleration * deltaTime);
            }

            currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed * 0.3f, maxSpeed);
        }

        /// <summary>
        /// 更新旋转（转向）
        /// </summary>
        private void UpdateRotation(float deltaTime)
        {
            if (Mathf.Abs(steerInput) > 0.01f)
            {
                // 转向角与速度相关（高速时灵敏度降低）
                float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / 60f);
                float targetAngle = steerInput * maxSteerAngle * speedFactor;
                currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetAngle, steerSensitivity * deltaTime);
            }
            else
            {
                // 自动回正
                currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, 0f, steerReturnSpeed * deltaTime);
            }

            // 应用旋转到车辆模型
            if (vehicleModel != null)
            {
                vehicleModel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
            }

            // 如果有导航器，也影响行驶方向
            if (navigator != null && navigator.IsRouteLoaded && Mathf.Abs(currentSteerAngle) > 0.1f)
            {
                // 转向会稍微偏离路线中心线（视觉效果）
            }
        }

        /// <summary>
        /// 移动车辆
        /// </summary>
        private void MoveVehicle(float deltaTime)
        {
            // 计算移动距离
            float speedMps = currentSpeed / 3.6f; // km/h → m/s
            float distance = speedMps * deltaTime * movingDirection;

            // 沿前方移动
            Vector3 moveDir = transform.forward;
            transform.position += moveDir * distance;

            // 更新导航器进度
            if (navigator != null && navigator.IsRouteLoaded)
            {
                navigator.UpdateProgress(transform.position);

                // 如果到达终点
                if (navigator.HasReachedEnd)
                {
                    currentSpeed = 0f;
                    OnVehicleStop?.Invoke();
                    Debug.Log("[DynastyVR Driving] 已到达目的地！🎉");
                }
            }

            lastPosition = transform.position;
        }

        /// <summary>
        /// 设置车辆位置（来自导航器）
        /// </summary>
        public void SetVehiclePosition(Vector3 worldPos)
        {
            transform.position = worldPos;
            lastPosition = worldPos;
        }

        /// <summary>
        /// 设置车辆朝向朝向下一个途经点
        /// </summary>
        public void SetVehicleRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
            if (vehicleModel != null)
                vehicleModel.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 获取当前速度文本（供HUD使用）
        /// </summary>
        public string GetSpeedText()
        {
            return $"{Mathf.Abs(currentSpeed):F0} km/h";
        }

        /// <summary>
        /// 获取当前模式文本
        /// </summary>
        public string GetModeText()
        {
            return currentMode == VehicleMode.Driving ? "🚗 自驾" : "✈️ 飞行";
        }

        private void OnGUI()
        {
            // 简易调试信息（正式版由VehicleHUD处理）
            if (currentMode == VehicleMode.Driving)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                style.normal.textColor = Color.white;

                GUI.Label(new Rect(10, 10, 300, 30), $"模式: {GetModeText()}", style);
                GUI.Label(new Rect(10, 35, 300, 30), $"速度: {GetSpeedText()}", style);
                GUI.Label(new Rect(10, 60, 300, 30), $"转向: {currentSteerAngle:F1}°", style);
                GUI.Label(new Rect(10, 85, 400, 30), $"[Tab]切换模式  [W/S]油门刹车  [A/D]转向", style);
            }
        }
    }
}
