# 🔬 DynastyVR — 技术验证方案 (Proof of Concept)

> 东方皇朝 EV 元宇宙部 · Phase 0 技术验证
> 编写者：EV-president
> 日期：2026-03-18
> **引擎迁移：Unity → Unreal Engine 5.4**

---

## 📌 概述

本 PoC 验证 DynastyVR 三大核心技术栈的可行性与集成效果：

1. **Cesium for Unreal** — 全球三维地球渲染
2. **Lumen + Nanite 渲染系统** — AAA级画面品质
3. **UE5 VR Template** — 沉浸式操作体验

**目标：** 在 2 周内搭建一个可运行的 VR Demo，实现"从太空俯瞰地球 → 缩放到上海外滩 → VR 漫步"的核心体验。

---

## 🌍 Part 1: Cesium for Unreal 集成

### 1.1 安装步骤

#### Step 1: 安装 Cesium for Unreal 插件

```bash
# 方法 A: Unreal Marketplace (推荐)
# 1. 打开 UE5 Editor
# 2. Window → Plugins → Search "Cesium"
# 3. 安装 Cesium for Unreal 官方插件
# 4. 重启编辑器

# 方法 B: 源码编译 (可选)
# 1. git clone https://github.com/CesiumGS/cesium-unreal.git
# 2. 将 CesiumForUnreal 目录放入 Plugins/
# 3. Generate project files & Build
```

#### Step 2: 配置 Cesium Ion Token

1. 前往 https://cesium.com/ion/tokens 创建账号
2. 生成新的 Access Token
3. 在 UE5 中: Cesium → Cesium Ion Token 设置
4. 配置资产 ID：
   - Cesium World Terrain: `1`
   - Cesium World Terrain + Bing Imagery: `1`
   - Google Photorealistic 3D Tiles: （需 Google Maps Platform key）

#### Step 3: 创建 Cesium 地球关卡

```
关卡层级结构 (Outliner):
├── CesiumGeoreference          (原点: 上海外滩 31.2304°N, 121.4737°E)
│   ├── Cesium3DTileset         (地形 + 影像)
│   │   └── Source: Cesium Ion Assets
│   ├── DirectionalLight        (太阳光, 绑定 Cesium)
│   └── SkyAtmosphere           (物理大气)
├── BP_VRPawn (VR玩家)
│   ├── Camera (XROrigin)
│   └── MotionController (左/右手)
└── [其他 Actors]
```

### 1.2 关键配置参数

```
# CesiumGeoreference 配置 (Details Panel)
Origin Latitude:    31.2304      (上海外滩纬度)
Origin Longitude:   121.4737     (上海外滩经度)
Origin Height:      0.0          (海平面)

# Cesium3DTileset 配置
Maximum Screen Space Error: 16.0   (LOD精度, 越小越精细)
Maximum Cache Size:        1000    (缓存大小MB)
Enable Water Mask:         true
Enable Lod:                true

# Cesium 对 UE5 Lumen 的适配
Cast Shadows:              true
Shadow Casting Type:       TwoSided
Nanite Override:           Enabled (for photorealistic tiles)
```

### 1.3 性能优化策略

| 策略 | 实现方式 |
|------|----------|
| 动态 LOD | Screen Space Error 16→32 (远处降低精度) |
| 瓦片缓存 | 2GB 磁盘缓存 + 1GB 内存缓存 |
| 视锥剔除 | Cesium 自带 frustum culling + UE5 Built-in |
| World Partition | 自动网格化分区流式加载 |
| 加载优先级 | 视口中心 > 视口边缘 > 屏幕外 |
| Nanite | Google Photorealistic Tiles 启用 Nanite |
| 预加载 | 地标区域预缓存 (上海、纽约等) |

### 1.4 验证指标

- [ ] 从太空缩放到地面 < 5秒 (无明显卡顿)
- [ ] 上海外滩区域加载帧率 ≥ 60fps
- [ ] 地形精度误差 < 30m (Copernicus DEM)
- [ ] 卫星影像无明显拼接缝隙
- [ ] Lumen 全局光照与 Cesium 地形正确混合
- [ ] Nanite 虚拟几何体与 Cesium 3D Tiles 兼容

---

## 🎨 Part 2: Lumen + Nanite 渲染系统配置

### 2.1 初始化渲染设置

#### Step 1: 创建项目 (VR Template)

```
1. Unreal Engine 5.4 Launcher → New Project
2. 选择 "VR Template"
3. 项目名称: DynastyVR
4. Blueprint/C++: C++ (主模块) + Blueprint (快速迭代)
5. Target: Desktop
6. Ray Tracing: ✅ (可后续启用)
7. 创建项目
```

#### Step 2: 启用核心渲染特性

```
Edit → Project Settings → Engine → Rendering:
├── Dynamic Global Illumination: Lumen
├── Reflection Method: Lumen
├── Shadow Method: Virtual Shadow Maps
├── Hardware Ray Tracing: ✅ (RTX 2080+)
├── Nanite: ✅ (default on in UE5)
├── Temporal Super Resolution: ✅ (TSR)
├── Automatic LOD: Generated (Static Mesh LOD)
└── Generate Mesh Distance Fields: ✅ (required for Lumen)
```

### 2.2 大气与天空配置

```
# 关卡中的大气 Actor 配置:

1. Sky Atmosphere (物理大气散射)
   └── Use Physical Light Units: ✅
   └── Planet Top (view from space): ✅

2. Directional Light (太阳)
   └── Intensity: 10 lux (physically based)
   └── Atmosphere Sun Light Index: 0
   └── Cast Deep Shadow: ✅

3. Sky Light (环境光)
   └── Source Type: Captured Scene
   └── Real Time Capture: ✅
   └── Intensity Scale: 1.0

4. Exponential Height Fog (地表雾)
   └── Fog Density: 0.02
   └── Fog Height Falloff: 0.2
   └── Volumetric Fog: ✅
   └── Scattering Distribution: 0.7

5. Volumetric Cloud (体积云)
   └── Cloud Material: M_VolumeCloud
   └── Altitude: 2000-15000m
   └── Layer Bottom: 2000m
   └── Layer Top: 15000m
   └── Wind: 可接入天气API动态控制
   └── Tracing Max Opaque Samples: 64
```

### 2.3 城市渲染质量层级

| 效果 | 上海外滩 (精品) | 普通城市 | 郊外/自然 |
|------|-----------------|----------|-----------|
| 反射 | Lumen Hit Lighting | Lumen Surface Cache | Screen Space |
| 阴影 | VSM 全精度 | VSM 中等 | VSM 低精度 |
| 接触阴影 | ✅ | ✅ | ❌ |
| 体积雾 | ✅ | 仅地表 | ❌ |
| 镜面反射平面 | ✅ (路面水洼) | ❌ | ❌ |
| 植被 (PCG) | 低密度精细 | 中密度 | 高密度 |
| Nanite | ✅ 建筑全开 | ✅ | ✅ 地形 |
| LOD HLOD | Level 0 only | Level 0-1 | Level 0-2 |

### 2.4 一日周期光照

```
蓝图系统: BP_DayNightCycle

# 逻辑框架:
Event Tick:
├── CalculateSunPosition(timeOfDay, latitude, longitude)
│   ├── Sun Altitude → DirectionalLight rotation X
│   └── Sun Azimuth → DirectionalLight rotation Y
├── Update SkyLight intensity (time-based curve)
├── Update Volumetric Cloud color (time-based)
├── Trigger Lumen rebuild for lighting changes
└── Post Process Volume blend:
    ├── Golden Hour (sun 5-15°): warm color grade
    ├── Blue Hour (sun 0-6°): blue color grade
    └── Night (sun <0°): bloom + city lights
```

### 2.5 验证指标

- [ ] RTX 3080 下 VR 帧率 ≥ 90fps (上海外滩场景)
- [ ] Lumen 全局光照从太空到地面无缝过渡
- [ ] Nanite 几何体无性能问题
- [ ] 日夜交替平滑，Lumen 实时更新
- [ ] 反射材质 (水面、玻璃幕墙) 物理正确
- [ ] VSM 阴影在远距离保持精度

---

## 🥽 Part 3: UE5 VR Template 配置

### 3.1 VR 环境搭建

#### Step 1: 使用 VR Template 创建项目

```
UE5 Launcher → New Project → Games → VR Template
├── Project Name: DynastyVR
├── Blueprint or C++: C++
├── Target: Desktop
└── Ray Tracing: Optional (can enable later)
```

#### Step 2: VR Pawn 配置

```
Hierarchy (Outliner):
├── VRPawn (BP_MotionControllerPawn)
│   ├── XROrigin (Scene Component)
│   │   ├── Camera (XRCameraComponent)
│   │   │   └── PostProcessVolume (本地后处理)
│   │   ├── LeftController (Motion Controller)
│   │   │   ├── LeftHandMesh
│   │   │   ├── GrabSphere (Sphere Trace)
│   │   │   └── LaserPointer (Line Trace)
│   │   └── RightController (Motion Controller)
│   │       ├── RightHandMesh
│   │       ├── GrabSphere
│   │       └── LaserPointer
│   └── VRLocomotion
│       ├── TeleportComponent
│       ├── SmoothMoveComponent
│       └── SnapTurnComponent
└── (继承自 VR Template 默认配置)
```

#### Step 3: Enhanced Input 配置

创建 Input Mapping Context:
```
Content/Input/IMC_VR_Default

Action Mappings:
├── IA_Move → L Thumbstick (Vector2D)
├── IA_Turn → R Thumbstick (Vector2D, X axis)
├── IA_GripLeft → L Grip Button (Digital)
├── IA_GripRight → R Grip Button (Digital)
├── IA_TriggerL → L Trigger (Float)
├── IA_TriggerR → R Trigger (Float)
├── IA_Menu → L Menu Button (Digital)
├── IA_Select → R A/X Button (Digital)
└── IA_Haptic → Output (Haptic Feedback)

# 车辆模式:
Action Map: "VR_Vehicle"
├── IA_Steering → Grip L+R (rotation-based)
├── IA_Throttle → R Trigger (Float)
├── IA_Brake → L Trigger (Float)
└── IA_Horn → R A Button
```

### 3.2 交互系统设计

#### 车辆/飞行器交互

```
蓝图: BP_VehicleInteraction

# 核心组件:
├── GrabPoint_SteeringWheel (GrabComponent)
│   └── Constraint: Rotation (limited -540° to +540°)
├── GrabPoint_Throttle (GrabComponent)
│   └── Axis: Forward/Backward
├── GrabPoint_Brake (GrabComponent)
│   └── Axis: Forward/Backward (reversed)
└── Haptic Feedback:
    ├── Engine Vibration: 0.1 intensity
    ├── Collision: 0.8 intensity
    └── Terrain Roughness: dynamic intensity
```

#### 地球浏览交互 (Google Earth 风格)

```
蓝图: BP_EarthInteraction

# 交互逻辑:
├── Pinch Zoom (双控制器距离 → 相机高度)
│   ├── Min Altitude: 10m
│   └── Max Altitude: 20,000km
├── Drag Rotate (单控制器移动 → 地球旋转)
├── Double Tap Landmark → FlyToLocation (Lerp 2s)
└── World Scale (手臂展开 → 世界缩放)
```

#### 导航 UI (3D Widget)

```
蓝图: BP_VRMenuWidget

# 菜单层级:
Layer 1: 全局菜单 (手掌朝上呼出)
├── 🌍 地球浏览
├── 🚗 自驾旅行
├── ✈️ 飞行模式
├── 🕳️ 宇宙探索
├── 🏠 建造模式
└── ⚙️ 设置

# Widget 距离: 1-3m (跟随手部)
# 交互: Laser Pointer 点击 / Grab 抓取
```

### 3.3 VR 舒适度配置

| 项目 | 设置 | 原因 |
|------|------|------|
| 运动模糊 | ❌ 关闭 | VR 晕动症 |
| 暗角 | ❌ 关闭 | 视野受限 |
| 地面雾 | ✅ 保持 | 有助稳定感 |
| 平滑移动 | ✅ 启用 | 减少卡顿感 |
| Snap Turn | ✅ 可选 | 45°/90°/连续 |
| 色调映射 | ACES Filmic | 自然色彩 |
| TSR | ✅ 启用 | 保持帧率稳定 |
| Instanced Stereo | ✅ 启用 | VR渲染优化 |

### 3.4 验证指标

- [ ] Quest 3 通过 Air Link/Link 运行 90fps
- [ ] 手部追踪 → 地球缩放操作流畅
- [ ] 控制器输入延迟 < 20ms
- [ ] 驾驶舱交互 (方向盘+油门) 可用
- [ ] 3D Widget UI 不遮挡主视野
- [ ] 30分钟 VR 测试无明显不适
- [ ] Lumen GI 在 VR 双目渲染正常

---

## 📅 PoC 实施计划 (2 周)

### Week 1: 基础搭建

| 天 | 任务 | 负责人 |
|----|------|--------|
| D1 | UE5 项目初始化 (VR Template) + 渲染设置 | EV-president |
| D2 | Cesium for Unreal 安装 + 地球关卡配置 | EV-president |
| D3 | 基础 VR Pawn 搭建 + Enhanced Input 配置 | EV-interaction-designer |
| D4 | Lumen 大气效果 + 日夜循环 + Volumetric Clouds | EV-scene-designer |
| D5 | 集成测试: VR中看到地球 (Lumen + Nanite) | 全员 |

### Week 2: 交互与优化

| 天 | 任务 | 负责人 |
|----|------|--------|
| D6 | 手势缩放地球交互 (双控制器) | EV-interaction-designer |
| D7 | 驾驶舱基础交互 (选择飞行器, Chaos物理) | EV-interaction-designer |
| D8 | 上海外滩场景精修 (Nanite建筑 + VSM阴影) | EV-scene-designer |
| D9 | 性能优化 (TSR/DLSS) + QA | EV-performance-engineer |
| D10 | PoC 演示录制 + 文档整理 | EV-president |

### 成功标准

> **PoC 通过条件：**
> 1. ✅ VR 设备中可流畅查看地球 (太空→地面无缝缩放)
> 2. ✅ 上海外滩区域达到 AAA 级画面质量 (Lumen + Nanite)
> 3. ✅ 手势/控制器交互响应延迟 < 20ms
> 4. ✅ 帧率在目标设备上达标 (PC ≥90fps, Quest ≥72fps)
> 5. ✅ 30分钟连续体验无明显晕动症报告
> 6. ✅ Lumen 全局光照在 VR 双目渲染中表现正确

---

## 📦 技术栈总结

| 组件 | 技术选型 | 版本 |
|------|----------|------|
| 引擎 | **Unreal Engine** | **5.4** |
| 渲染管线 | **Lumen GI + Nanite** | UE5 built-in |
| 全局光照 | **Lumen** | 动态GI，实时计算 |
| 虚拟几何体 | **Nanite** | 无需LOD |
| 大世界加载 | **World Partition** | 自动网格化 |
| 程序化生成 | **PCG** | 植被/建筑自动生成 |
| 物理引擎 | **Chaos Physics** | 车辆/破坏 |
| 粒子系统 | **Niagara** | 星云/天气/特效 |
| 阴影 | **Virtual Shadow Maps** | 像素级精确 |
| 超分辨率 | **TSR / DLSS / XeSS** | 多方案可选 |
| 地球引擎 | **Cesium for Unreal** | 最新版 |
| 3D 建筑 | **Google Photorealistic Tiles** | 最新 |
| 地形 | **Copernicus DEM** | 30m |
| 卫星影像 | **Bing Maps / Sentinel-2** | 最新 |
| VR 框架 | **UE5 VR Template + OpenXR** | 内置 |
| 输入系统 | **Enhanced Input** | UE5 原生 |
| 路网数据 | **OpenStreetMap** | 最新 |
| 星表 | **Hipparcos / Tycho-2** | 完整星表 |
| 黑洞数据 | **EHT 公开影像** | 2019+ |

---

*此 PoC 文档将作为 Phase 1 开发的技术基准。验证通过后进入正式开发阶段。*
