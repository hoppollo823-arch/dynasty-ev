# 🎮 DynastyVR — Unreal Engine 5 项目设置指南

> 东方皇朝 EV 元宇宙部 · 项目技术配置文档
> 更新日期：2026-03-18
> **引擎迁移：Unity → Unreal Engine 5.4**

---

## 1. UE5 版本与模块

| 项目 | 配置 |
|------|------|
| 引擎版本 | **Unreal Engine 5.4** (最新补丁版本) |
| 项目模板 | **VR Template** (UE5 VR项目起点) |
| 渲染特性 | **Lumen GI + Nanite + World Partition** |
| 输入系统 | Enhanced Input System (UE5 原生) |
| XR 插件 | OpenXR + SteamVR / Meta Quest Link |
| 物理引擎 | **Chaos Physics** (UE5 默认) |
| 粒子系统 | **Niagara** |
| 最低支持平台 | Windows 10 (64-bit) / Quest 3 (Air Link) |
| 推荐开发平台 | Windows 11 (RTX 3080+) / Ubuntu 22.04 |

### 必装模块 (UE5 Feature Packs)
- Engine (核心引擎)
- Lumen (全局光照)
- Nanite (虚拟几何体)
- World Partition (大世界)
- PCG (程序化内容生成)
- EnhancedInput (输入系统)
- OpenXR (VR支持)
- Niagara (粒子特效)
- ChaosPhysics (物理引擎)
- CommonUI (通用UI框架)

---

## 2. Lumen 全局光照配置

### 2.1 Project Settings → Rendering

```
Engine → Rendering:
├── Global Illumination
│   ├── Dynamic Global Illumination Method: Lumen
│   ├── Reflection Method: Lumen
│   ├── Lumen Scene
│   │   ├── Mesh Distance Fields: ✅ Enabled
│   │   ├── Use Hardware Ray Tracing when available: ✅
│   │   ├── Lumen Ray Lighting Mode: Hit Lighting for Reflections
│   │   └── Final Gather Quality: 4 (High)
│   └── Lumen Global Illumination
│       ├── Lumen Scene Detail: 8.0
│       ├── Lumen Final Gather Quality: 4.0
│       ├── Lumen Surface Cache: ✅ Enabled
│       ├── Two Sided Geometry: ✅ Enabled
│       └── Fallback Relative Error: 0.2
│
├── Global Illumination Fallbacks
│   ├── Dynamic GI Method Fallback: Screen Space (For Low Quality)
│   └── Lumen Fallback: Screen Space
│
└── Ray Tracing (Hardware RT, optional)
    ├── Enable Ray Tracing: ✅ (RTX 2080+)
    ├── Ray Tracing Reflections: ✅
    ├── Ray Tracing Shadows: ✅
    └── Ray Tracing Ambient Occlusion: ✅
```

### 2.2 关键渲染参数

| 参数 | Epic (PC VR) | High | Medium | Low (Quest Link) |
|------|--------------|------|--------|------------------|
| 全局光照 | Lumen (HW RT) | Lumen (SW RT) | Lumen (Fallback) | Screen Space |
| 反射 | Lumen (Hit Lighting) | Lumen (Surface Cache) | Screen Space | Screen Space |
| 阴影 | Virtual Shadow Maps | VSM (Reduced) | Cascaded Shadow | Cascaded Shadow |
| 阴影距离 | 50000cm | 30000cm | 15000cm | 10000cm |
| 云层 | Volumetric Clouds | Volumetric (Low) | 2D Sky | Flat Sky |
| Nanite | ✅ 全启用 | ✅ 核心资产 | 部分 | ❌ 关闭 |
| TSR | ✅ 启用 | ✅ 启用 | ✅ 启用 | ❌ 关闭 |
| 帧率目标 | 90fps | 90fps | 72fps | 72fps |

### 2.3 虚拟阴影贴图 (VSM)

```
Engine → Rendering → Virtual Texture Shadows:
├── Enable Virtual Shadow Maps: ✅
├── Enable Per Object Casts Contact Shadows: ✅
├── Max Physical Pages (2^N): 20
├── Cache Update Rate: 2.0
└── Allow Hardware Ray Tracing When Available: ✅
```

### 2.4 Nanite 设置

```
Project Settings → Nanite:
├── Default Nanite Tessellation: Disabled (for now)
├── Allow Tessellation: (future use)
├── Max Tessellation Factor: 64
├── Material Compatibility:
│   ├── Support Unlit Materials: ✅
│   └── Support World Position Offset: ✅
└── Fallback Relative Error: 0.2
```

---

## 3. World Partition 配置

### 3.1 大世界设置

```
World Settings → World Partition:
├── Enable World Partition: ✅
├── Runtime Grid Settings:
│   ├── Grid (0, 0): Grid Size = 25600m, Loading Range = 12800m
│   ├── HLOD:
│   │   ├── Enable HLOD: ✅
│   │   ├── HLOD Volume: (auto-generated)
│   │   └── HLOD Level 1 Distance: 5000m
│   └── Streaming Source:
│       ├── Player: ✅ (跟随玩家位置流式加载)
│       └── Min/Max Draw Distance: 1000m / 50000m
├── Spatial Loading:
│   ├── Loading Range: 12800m
│   └── Cell Size: 256m
└── Data Layers (用于分区域控制):
    ├── Layer: "City_Shanghai" (LOD0 全精度)
    ├── Layer: "City_NewYork" (LOD0 全精度)
    ├── Layer: "City_Generic" (LOD1 中精度)
    └── Layer: "Nature_Rural" (LOD2 低精度)
```

### 3.2 HLOD (Hierarchical Level of Detail)

```
HLOD 配置:
├── Level 0: 原始模型 (0-2km)
├── Level 1: 简化模型 (2-5km) — 合并为 instanced meshes
├── Level 2: 代理模型 (5-15km) — billboard + 简单几何
└── Level 3: 极简 (15km+) — 仅地形 + 颜色块
```

---

## 4. VR 配置 (UE5 VR Template)

### 4.1 VR 模板设置

```
Project Settings → Maps & Modes:
├── Default GameMode: BP_MotionControllerGameMode
├── Default PawnClass: BP_MotionControllerPawn
├── Start in VR: ✅
└── Enable VR Preview: ✅
```

### 4.2 Enhanced Input 配置

```
Engine → Input → Enhanced Input:
├── Default Input Component Class: EnhancedInputComponent
├── Default Player Input Class: EnhancedPlayerInput
├── Input Mapping Contexts:
│   ├── IMC_VR_Default
│   │   ├── IA_Move → Thumbstick (L)
│   │   ├── IA_Turn → Thumbstick (R)
│   │   ├── IA_GripLeft → Grip (L)
│   │   ├── IA_GripRight → Grip (R)
│   │   ├── IA_TriggerL → Trigger (L)
│   │   ├── IA_TriggerR → Trigger (R)
│   │   ├── IA_Menu → Menu Button
│   │   ├── IA_Jump → (Reserved)
│   │   └── IA_Crouch → (Reserved)
│   └── IMC_VR_Vehicle
│       ├── IA_Steering → Grip (L+R rotation)
│       ├── IA_Throttle → Trigger (R)
│       ├── IA_Brake → Trigger (L)
│       └── IA_GearShift → Thumbstick (R)
└── Default Input Dead Zones:
    ├── Move: 0.25
    └── Turn: 0.25
```

### 4.3 VR 交互规则

**运动方式优先级：**
1. 飞行器/车辆内 → 座舱跟随，无独立 locomotion
2. 自由行走 → 连续移动 + 平滑转弯 (可选 snap turn)
3. 地球浏览 → 手势缩放/拖拽 (Google Earth 风格)

**交互原则：**
- 所有 UI 使用 World Space Widget (3D UI)
- 菜单呼出：左手菜单按钮 / 手势 (手掌朝上)
- 抓取物体：Grip 键 + Chaos 物理碰撞
- 驾驶车辆：双手模拟方向盘 + 油门扳机
- 触觉反馈：OpenXR Haptic Output

---

## 5. UE5 项目目录结构

```
UE5Project/
├── Source/                          # C++ 模块
│   ├── DynastyVR/                   # 主模块
│   │   ├── DynastyVR.Build.cs
│   │   ├── DynastyVRGameMode.cpp/.h
│   │   └── DynastyVRPlayerController.cpp/.h
│   ├── GeoEngine/                   # 地球引擎 (Cesium集成, 地形)
│   │   ├── GeoEngine.Build.cs
│   │   ├── CesiumTileManager.cpp/.h
│   │   ├── TerrainGenerator.cpp/.h
│   │   └── AtmosphereSystem.cpp/.h
│   ├── RoadEngine/                  # 路网引擎 (OSM, 车辆物理)
│   │   ├── RoadEngine.Build.cs
│   │   ├── RoadNetwork.cpp/.h
│   │   ├── VehiclePhysicsComponent.cpp/.h
│   │   └── RoutePlanner.cpp/.h
│   ├── CosmosEngine/                # 宇宙引擎 (天体, 黑洞)
│   │   ├── CosmosEngine.Build.cs
│   │   ├── CelestialBody.cpp/.h
│   │   ├── BlackHoleRenderer.cpp/.h
│   │   └── StarFieldSystem.cpp/.h
│   ├── BuildSystem/                 # 建造系统 (UGC, 地块)
│   │   ├── BuildSystem.Build.cs
│   │   ├── ConstructionComponent.cpp/.h
│   │   ├── LandPlotManager.cpp/.h
│   │   └── ModularBuildingActor.cpp/.h
│   └── Social/                      # 社交系统 (多人, Avatar)
│       ├── Social.Build.cs
│       ├── MultiplayerSession.cpp/.h
│       ├── VoiceChatComponent.cpp/.h
│       └── AvatarSystem.cpp/.h
│
├── Content/                         # 资产目录 (Git LFS)
│   ├── DynastyVR/
│   │   ├── Maps/
│   │   │   ├── MainMenu.umap
│   │   │   ├── EarthDemo.umap
│   │   │   ├── RoadTripDemo.umap
│   │   │   ├── CosmosDemo.umap
│   │   │   └── TestLevel.umap
│   │   ├── Blueprints/
│   │   │   ├── Vehicles/
│   │   │   ├── Aircraft/
│   │   │   ├── Characters/
│   │   │   ├── Environment/
│   │   │   └── UI/
│   │   ├── Materials/
│   │   │   ├── M_Terrain_Base
│   │   │   ├── M_Water_Ocean
│   │   │   ├── M_Vehicle_PBR
│   │   │   ├── M_Sky_Atmosphere
│   │   │   └── MI_*/ (材质实例)
│   │   ├── Textures/
│   │   │   ├── T_Terrain/
│   │   │   ├── T_Sky/
│   │   │   └── T_UI/
│   │   ├── Models/
│   │   │   ├── SM_Vehicles/
│   │   │   ├── SM_Aircraft/
│   │   │   └── SM_Environment/
│   │   ├── Audio/
│   │   │   ├── Engine/
│   │   │   ├── Ambient/
│   │   │   └── Music/
│   │   ├── Niagara/
│   │   │   ├── NS_Weather_Rain
│   │   │   ├── NS_Weather_Snow
│   │   │   ├── NS_BlackHole_Accretion
│   │   │   └── NS_Engine_Exhaust
│   │   └── DataAssets/
│   │       ├── DA_VehicleStats
│   │       ├── DA_LocationData
│   │       └── DA_WeatherPresets
│
├── Plugins/
│   ├── CesiumForUnreal/             # Cesium for Unreal 插件
│   │   ├── Source/
│   │   ├── Content/
│   │   └── CesiumForUnreal.uplugin
│   └── (其他第三方插件)
│
├── Config/                          # 配置文件
│   ├── DefaultEngine.ini
│   ├── DefaultGame.ini
│   ├── DefaultInput.ini
│   ├── DefaultScalability.ini
│   ├── DefaultVR.ini
│   └── Input/
│       └── DefaultInput.ini
│
├── Plugins/ (Cesium for Unreal)     # (已包含在上方 Plugins/)
│
├── DynastyVR.uproject               # 项目文件
├── .gitignore
└── docs/                            # 项目文档 (repo root)
```

---

## 6. Config 文件配置

### 6.1 DefaultEngine.ini (核心渲染)

```ini
[/Script/Engine.RendererSettings]
r.DynamicGlobalIlluminationMethod=1          ; Lumen
r.ReflectionMethod=1                          ; Lumen Reflections
r.Shadow.Virtual.Enable=1                     ; Virtual Shadow Maps
r.Nanite.AllowTessellation=0                  ; Tessellation off
r.Lumen.HardwareRayTracing.Enable=1           ; HW Ray Tracing
r.Lumen.FinalGather.Quality=4.0               ; High quality
r.Lumen.FinalGather.ScreenTraces=1
r.Lumen.TraceMeshSDFs=1
r.LumenScene.SurfaceCache.MeshCardsMinSize=10
r.RayTracing.GlobalIllumination=1             ; RT GI
r.Lumen.Reflections.Allow=1
r.Lumen.Reflections.MaxBounces=4
r.Lumen.DiffuseIndirect.Allow=1

[/Script/Engine.RendererSettings]
r.TemporalAA.Quality=1                        ; TSR High
r.TemporalAA.Upsampling=1                     ; TSR Upsampling
r.TemporalAA.Algorithm=1                      ; TCBAA (new)
r.TemporalAAFilterSize=0.5

[/Script/Engine.RendererSettings]
r.Streaming.PoolSize=4000                     ; Texture pool 4GB
r.ViewDistanceScale=1.0
r.ViewDistanceScale.Exponent=1.0

[/Script/Engine.VirtualTexturePoolSettings]
TexturesPoolSizeLimit=4000                    ; 4GB VT pool

[/Script/EngineSettings.GeneralProjectSettings]
ProjectName=DynastyVR
ProjectVersion=0.1.0
ProjectID={GENERATE_NEW_GUID}
```

### 6.2 DefaultGame.ini

```ini
[/Script/EngineSettings.GeneralProjectSettings]
ProjectName=DynastyVR
ProjectDescription=东方皇朝元宇宙 - 数字孪生宇宙
ProjectVersion=0.1.0
CompanyName=Dynasty Entertainment
ProjectDisplayedTitle=DynastyVR

[/Script/UnrealEd.ProjectPackagingSettings]
BuildConfiguration=PPBC_Development
StagingDirectory=(Path="")
FullRebuild=False
ForDistribution=False
UsePakFile=True
GenerateChunks=True
ChunkBasedInstall=True
CookEverythingInContentDirectory=False
```

### 6.3 DefaultInput.ini (Enhanced Input)

```ini
[/Script/Engine.InputSettings]
DefaultInputComponentClass=/Script/EnhancedInput.EnhancedInputComponent

[/Script/EnhancedInput.EnhancedInputUserSettings]
bEnableDefaultMappingContext=True
DefaultMappingContext=/Game/Input/IMC_VR_Default.IMC_VR_Default
```

### 6.4 DefaultVR.ini

```ini
[/Script/Engine.RendererSettings]
vr.InstancedStereoEnabled=1                    ; 基于实例的立体渲染
vr.HiddenAreaMask=1                           ; VR遮罩
vr.VROnlyMode=1                               ; 仅VR模式

[/Script/OculusHMD.OculusHMDSettings]
bInitHmdOnStartUp=1
bUseHealthAndSafetyWarning=True
PixelDensity=1.0
SuggestedPixelDensity=1.25
```

---

## 7. 性能预算

### PC VR (推荐配置: RTX 3080 + i7-12700K)

| 指标 | 预算 |
|------|------|
| 帧率 | ≥90 fps (11.1ms/frame) |
| GPU 时间 | < 10ms |
| CPU 时间 | < 8ms |
| 内存 (GPU VRAM) | < 10 GB |
| 内存 (RAM) | < 16 GB |
| Draw Calls | < 3000 |
| Triangles/Frame | < 10M (Nanite 可承受更多) |
| Texture Pool | 4GB |
| Nanite Mesh Cards | < 2000/帧 |

### Quest 3 (Air Link / Link)

| 指标 | 预算 |
|------|------|
| 帧率 | ≥72 fps (13.9ms/frame) |
| GPU 时间 | < 12ms |
| CPU 时间 | < 10ms |
| 内存 (RAM) | < 6 GB |
| Draw Calls | < 500 |
| Triangles/Frame | < 2M |
| Nanite | 关闭 (fallback to LOD) |

---

## 8. 版本控制约定

- UE5 资产文件 (.uasset, .umap) 使用 **Git LFS** 追踪
- C++ 源码直接 git tracking
- Config 文件直接 git tracking
- LFS 追踪规则:
  ```
  *.uasset filter=lfs diff=lfs merge=lfs -text
  *.umap filter=lfs diff=lfs merge=lfs -text
  *.png filter=lfs diff=lfs merge=lfs -text
  *.jpg filter=lfs diff=lfs merge=lfs -text
  *.fbx filter=lfs diff=lfs merge=lfs -text
  *.wav filter=lfs diff=lfs merge=lfs -text
  *.mp3 filter=lfs diff=lfs merge=lfs -text
  ```

---

## 9. 分支策略

```
main          ← 稳定版本, 可部署
├── develop   ← 日常开发整合
│   ├── feature/geo-engine
│   ├── feature/road-trip
│   ├── feature/cosmos
│   ├── feature/build-system
│   └── feature/vr-interaction
└── release/x.y ← 发布准备
```
