# 🔬 DynastyVR — 技术验证方案 (Proof of Concept)

> 东方皇朝 EV 元宇宙部 · Phase 0 技术验证
> 编写者：EV-president
> 日期：2026-03-18

---

## 📌 概述

本 PoC 验证 DynastyVR 三大核心技术栈的可行性与集成效果：

1. **Cesium for Unity** — 全球三维地球渲染
2. **HDRP 渲染管线** — AAA级画面品质
3. **VR 交互框架** — 沉浸式操作体验

**目标：** 在 2 周内搭建一个可运行的 VR Demo，实现"从太空俯瞰地球 → 缩放到上海外滩 → VR 漫步"的核心体验。

---

## 🌍 Part 1: Cesium for Unity 集成

### 1.1 安装步骤

#### Step 1: 安装 Cesium for Unity 插件

```bash
# 通过 Unity Package Manager (UPM) 安装
# 方法 A: OpenUPM (推荐)

# 在 Unity 中: Window → Package Manager → + → Add package by name
# 输入: com.cesium.unity

# 方法 B: 直接编辑 Packages/manifest.json
# 添加:
# "com.cesium.unity": "1.7.0"
```

#### Step 2: 配置 Cesium Ion Token

1. 前往 https://cesium.com/ion/tokens 创建账号
2. 生成新的 Access Token
3. 在 Unity 中: Cesium → Cesium3DTileset → Ion Token
4. 配置资产 ID：
   - Cesium World Terrain: `1`
   - Cesium World Terrain + Bing Imagery: `1`
   - Google Photorealistic 3D Tiles: （需 Google Maps Platform key）

#### Step 3: 创建 Cesium 地球

```
场景层级结构:
Hierarchy:
├── CesiumGeoreference          (原点: 上海外滩 31.2304°N, 121.4737°E)
│   ├── Cesium3DTileset         (地形 + 影像)
│   │   └── Source: Cesium Ion Assets
│   ├── CesiumSunLight          (HDRP方向光绑定)
│   └── CesiumBackgroundColor    (HDRP天空盒绑定)
├── XRRig                       (VR 相机)
└── [其他 GameObjects]
```

### 1.2 关键配置参数

```csharp
// CesiumGeoreference.cs 配置
[Header("Origin Location")]
public double latitude = 31.2304;    // 上海外滩纬度
public double longitude = 121.4737;  // 上海外滩经度  
public double height = 0.0;          // 海平面

// Cesium3DTileset.cs 配置
[Header("Tile Loading")]
public float maximumScreenSpaceError = 16.0f;  // LOD精度 (越小越精细)
public int maximumCacheSize = 1000;            // 缓存大小(MB)
public bool enableWaterMask = true;            // 水面遮罩
public bool enableLod = true;                  // LOD 流式加载

// Cesium 对 HDRP 的适配
[Header("HDRP Integration")]
public bool castShadows = true;
public int shadowCastingType = 1;              // TwoSided
```

### 1.3 性能优化策略

| 策略 | 实现方式 |
|------|----------|
| 动态 LOD | Screen Space Error 16→32 (远处降低精度) |
| 瓦片缓存 | 2GB 磁盘缓存 + 1GB 内存缓存 |
| 视锥剔除 | Cesium 自带 frustum culling |
| 加载优先级 | 视口中心 > 视口边缘 > 屏幕外 |
| 预加载 | 地标区域预缓存 (上海、纽约等) |

### 1.4 验证指标

- [ ] 从太空缩放到地面 < 5秒 (无明显卡顿)
- [ ] 上海外滩区域加载帧率 ≥ 60fps
- [ ] 地形精度误差 < 30m (Copernicus DEM)
- [ ] 卫星影像无明显拼接缝隙
- [ ] HDRP 材质与 Cesium 地形正确混合

---

## 🎨 Part 2: HDRP 渲染管线配置

### 2.1 初始化 HDRP

#### Step 1: 创建 HDRP Asset

```
Assets/Settings/HDRP/
├── DynastyVR_HDRenderPipelineAsset.asset  ← 主管线 Asset
└── Quality/
    ├── VR_High.asset      ← PC VR (RTX 3080+)
    ├── VR_Medium.asset    ← PC VR (GTX 1660+)
    └── Quest_Link.asset   ← Quest Link 模式
```

#### Step 2: 配置 HDRP Global Settings

```
Project Settings → HDRP:
├── Default Volume Profile → 新建 DynastyVR_VolumeProfile
├── Lighting:
│   ├── Reflection Probe Mode: Automated
│   ├── Ambient Mode: Dynamic (HDRI Sky)
│   └── Probe Volume: Enabled (APV)
├── Rendering:
│   ├── Decal Layers: Enabled
│   ├── Depth Buffer: R32 (用于大气散射)
│   └── Color Buffer Format: R16G16B16A16
└── XR:
    ├── Single Pass Instanced: ✅ (VR必备)
    └── Mirror XR View: ✅
```

### 2.2 大气与天空配置

```csharp
// HDRP Volume Profile: 大气效果
// 路径: Assets/Settings/VolumeProfiles/Atmosphere.asset

VolumeProfile:
├── Volumetric Fog
│   ├── Enable: ✅
│   ├── Fog Height: 20km
│   ├── Fog Density: 0.005
│   ├── Color Mode: Ground Color (根据海拔渐变)
│   └── Scattering: Rayleigh (蓝色天空) + Mie (太阳光晕)
│
├── HDRI Sky (或 Procedural Sky)
│   ├── Enable: ✅
│   ├── HDRI Cubemap: 自定义太空→地面渐变 cubemap
│   ├── Exposure: 13.0 (室外)
│   ├── Rotation: 由 Sun Light 控制
│   └── Intensity Multiplier: 1.0
│
├── Physically Based Sky
│   ├── Enable: ✅ (备选方案, 物理准确)
│   ├── Planet Radius: 6371000m (地球半径)
│   ├── Atmosphere Height: 100000m
│   ├── Air Density: 1.0
│   ├── Aerosol Density: 0.3
│   └── Color Saturation: 1.0
│
├── Cloud Layer (VFX Graph 或 HDRP Clouds)
│   ├── Enable: ✅
│   ├── Cloud Type: Volumetric
│   ├── Altitude: 2000-10000m
│   ├── Coverage: 动态 (可接入天气API)
│   └── Wind: 模拟真实风向
│
└── Custom Fog (地表雾效)
    ├── Enable: ✅
    ├── Height: 0-200m
    ├── Density: 0.01
    └── Color: 灰白色 (城市霾)
```

### 2.3 城市渲染质量层级

| 效果 | 上海外滩 (精品) | 普通城市 | 郊外/自然 |
|------|-----------------|----------|-----------|
| 反射探针 | 全覆盖 5m间距 | 20m间距 | 100m间距 |
| 屏幕空间反射 | 高质量 | 中等 | 关闭 |
| 接触阴影 | ✅ | ✅ | ❌ |
| 体积雾 | ✅ | 仅地表 | ❌ |
| 镜面反射平面 | ✅ (路面水洼) | ❌ | ❌ |
| 植被密度 | 低 | 中 | 高 |
| 建筑 LOD | LOD0-3 | LOD0-2 | LOD0-1 |

### 2.4 一日周期光照

```csharp
// 动态时间系统 (C# 脚本框架)
public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    public float timeOfDay = 12.0f;        // 0-24
    public float timeSpeed = 1.0f;         // 1x = 真实时间
    public float latitude = 31.23f;        // 随 Cesium 位置更新
    public float longitude = 121.47f;

    [Header("References")]
    public Light sunLight;
    public Volume postProcessVolume;

    void Update()
    {
        timeOfDay += Time.deltaTime * timeSpeed / 3600f;
        if (timeOfDay > 24f) timeOfDay -= 24f;

        // 计算太阳方位角和高度角
        float sunAltitude = CalculateSunAltitude(timeOfDay, latitude, longitude);
        float sunAzimuth = CalculateSunAzimuth(timeOfDay, latitude, longitude);

        // 更新方向光
        sunLight.transform.rotation = Quaternion.Euler(sunAltitude, sunAzimuth, 0f);

        // 更新后处理 (黄金时刻、蓝调时刻自动触发)
        UpdatePostProcessForTime(sunAltitude);
    }
}
```

### 2.5 验证指标

- [ ] RTX 3080 下 4K VR 帧率 ≥ 90fps (上海外滩场景)
- [ ] 大气散射从太空到地面无缝过渡
- [ ] 日夜交替平滑无跳帧
- [ ] 反射材质 (水面、玻璃幕墙) 物理正确
- [ ] HDRP 单 Pass Instanced VR 渲染正常

---

## 🥽 Part 3: VR 交互框架

### 3.1 XR 环境搭建

#### Step 1: 安装依赖

```json
// Packages/manifest.json 中添加:
{
  "com.unity.xr.openxr": "1.9.1",
  "com.unity.xr.interaction.toolkit": "2.5.2",
  "com.unity.xr.hands": "1.3.0",
  "com.unity.inputsystem": "1.7.0"
}
```

#### Step 2: XR Rig 配置

```
Hierarchy:
├── XR Rig (偏移容器, 位置 (0,0,0))
│   ├── Camera Offset (Y=0.0 对应坐姿, Y=1.6 对应站姿)
│   │   ├── Main Camera (TrackedPoseDriver)
│   │   │   └── Post Process Volume (仅本地后处理)
│   │   ├── Left Controller (XR Controller)
│   │   │   ├── Left Controller Model
│   │   │   ├── XR Ray Interactor (激光)
│   │   │   ├── XR Direct Interactor (抓取)
│   │   │   └── Hand Model (备选手部追踪)
│   │   └── Right Controller (同上)
│   ├── Locomotion System
│   │   ├── Teleportation Provider
│   │   ├── Snap Turn Provider (90°)
│   │   └── Continuous Move Provider (可选)
│   └── ActionBasedControllerManager (状态机)
```

#### Step 3: Input Actions 配置

创建 Input Action Asset:
```
Assets/Input/VRControls.inputactions

Action Map: "XR Default"
├── Move (Value, Vector2) → Thumbstick
├── Turn (Value, Vector2) → Thumbstick (X轴)
├── Grip (Value, Float) → Grip Button
├── Trigger (Value, Float) → Trigger Button
├── PrimaryButton (Button) → A/X
├── SecondaryButton (Button) → B/Y
├── Menu (Button) → Menu Button
├── Haptics (Value, Vector2) → Output
└── Pointer (Value, Pose) → Pose from controller
```

### 3.2 交互系统设计

#### 车辆/飞行器交互

```csharp
// 驾驶舱交互 (核心脚本框架)
public class VehicleCockpitInteraction : MonoBehaviour
{
    [Header("Input Bindings")]
    public XRGrabInteractable steeringWheel;
    public XRGrabInteractable throttle;      // 扳机油门
    public XRGrabInteractable brake;         // 扳机刹车
    public XRGrabInteractable gearShift;     // 档位

    [Header("Haptic Feedback")]
    public float engineVibrationIntensity = 0.1f;
    public float collisionVibrationIntensity = 0.8f;

    void OnSteeringWheelRotated(float angle)
    {
        // 限制方向盘旋转范围: -540° ~ +540°
        vehiclePhysics.SetSteerAngle(angle);
        // 发送触觉反馈
        SendHapticPulse(engineVibrationIntensity);
    }
}
```

#### 地球浏览交互 (Google Earth 风格)

```csharp
// 地球手势交互
public class EarthGestureInteraction : MonoBehaviour
{
    public CesiumGeoreference georeference;

    [Header("Gesture Settings")]
    public float zoomSpeed = 2.0f;
    public float rotateSpeed = 0.5f;
    public float minAltitude = 10.0f;      // 最低高度 (10m)
    public float maxAltitude = 20000000.0f; // 最高高度 (20000km)

    // 双手缩放
    public void OnPinchZoom(float delta)
    {
        float newAltitude = Mathf.Clamp(
            currentAltitude * (1f - delta * zoomSpeed),
            minAltitude,
            maxAltitude
        );
        SetCameraAltitude(newAltitude);
    }

    // 单手旋转
    public void OnDragRotate(Vector2 delta)
    {
        RotateGlobe(delta.x * rotateSpeed, delta.y * rotateSpeed);
    }

    // 双击地标 → 快速跳转
    public void OnDoubleTapLandmark(Vector3 worldPosition)
    {
        FlyToLocation(worldPosition, transitionDuration: 2.0f);
    }
}
```

#### 导航 UI (World Space)

```csharp
// VR 菜单系统
public class VRMenuSystem : MonoBehaviour
{
    // 菜单层级:
    // Layer 1: 全局菜单 (手掌朝上呼出)
    //   ├── 🌍 地球浏览
    //   ├── 🚗 自驾旅行
    //   ├── ✈️ 飞行模式
    //   ├── 🕳️ 宇宙探索
    //   ├── 🏠 建造模式
    //   └── ⚙️ 设置
    //
    // Layer 2: 功能子菜单
    //   地球浏览 → [搜索地点] [收藏] [路线规划]
    //   自驾旅行 → [选择路线] [选择车辆] [天气]
    //
    // 悬浮UI → World Space Canvas (距离 1-3m, 跟随手部)
    // 交互 → Ray Interactor 点击 / Direct Interactor 抓取
}
```

### 3.3 VR 舒适度配置

| 项目 | 设置 | 原因 |
|------|------|------|
| 运动模糊 | ❌ 关闭 | VR 晕动症 |
| 镜头畸变 | ❌ 关闭 | VR 晕动症 |
| 暗角 | ❌ 关闭 | 视野受限 |
| 地面雾 | ✅ 保持 | 有助稳定感 |
| 平滑移动 | ✅ 启用 | 减少卡顿感 |
| Snap Turn | ✅ 可选 | 90°/45°/连续 |
| 色调映射 | ACES Filmic | 自然色彩 |

### 3.4 验证指标

- [ ] Quest 3 通过 Link 模式运行 90fps
- [ ] 手部追踪 → 地球缩放操作流畅
- [ ] 控制器输入延迟 < 20ms
- [ ] 驾驶舱交互 (方向盘+油门) 可用
- [ ] World Space UI 不遮挡主视野
- [ ] 30分钟 VR 测试无明显不适

---

## 📅 PoC 实施计划 (2 周)

### Week 1: 基础搭建

| 天 | 任务 | 负责人 |
|----|------|--------|
| D1 | Unity 项目初始化 + HDRP + XR 插件安装 | EV-president |
| D2 | Cesium for Unity 安装 + 地球场景配置 | EV-president |
| D3 | 基础 VR Rig 搭建 + Input Actions 配置 | EV-interaction-designer |
| D4 | 大气效果 + 日夜循环基础 | EV-scene-designer |
| D5 | 集成测试: VR中看到地球 | 全员 |

### Week 2: 交互与优化

| 天 | 任务 | 负责人 |
|----|------|--------|
| D6 | 手势缩放地球交互 | EV-interaction-designer |
| D7 | 驾驶舱基础交互 (选择飞行器) | EV-interaction-designer |
| D8 | 上海外滩场景精修 | EV-scene-designer |
| D9 | 性能优化 + QA | EV-performance-engineer |
| D10 | PoC 演示录制 + 文档整理 | EV-president |

### 成功标准

> **PoC 通过条件：**
> 1. ✅ VR 设备中可流畅查看地球 (太空→地面无缝缩放)
> 2. ✅ 上海外滩区域达到 AAA 级画面质量
> 3. ✅ 手势/控制器交互响应延迟 < 20ms
> 4. ✅ 帧率在目标设备上达标 (PC ≥90fps, Quest ≥72fps)
> 5. ✅ 30分钟连续体验无明显晕动症报告

---

## 📦 技术栈总结

| 组件 | 技术选型 | 版本 |
|------|----------|------|
| 引擎 | Unity | 2022.3 LTS |
| 渲染管线 | HDRP | 14.x |
| 地球引擎 | Cesium for Unity | 1.7 |
| 3D 建筑 | Google Photorealistic Tiles | 最新 |
| 地形 | Copernicus DEM | 30m |
| 卫星影像 | Bing Maps / Sentinel-2 | 最新 |
| VR 框架 | XR Interaction Toolkit | 2.5 |
| 输入系统 | Unity Input System | 1.7 |
| 路网数据 | OpenStreetMap | 最新 |
| 星表 | Hipparcos / Tycho-2 | 完整星表 |
| 黑洞数据 | EHT 公开影像 | 2019+ |

---

*此 PoC 文档将作为 Phase 1 开发的技术基准。验证通过后进入正式开发阶段。*
