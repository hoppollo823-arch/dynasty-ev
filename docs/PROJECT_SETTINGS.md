# 🎮 DynastyVR — Unity 项目设置指南

> 东方皇朝 EV 元宇宙部 · 项目技术配置文档
> 更新日期：2026-03-18

---

## 1. Unity 版本与模块

| 项目 | 配置 |
|------|------|
| Unity 版本 | **2022.3 LTS** (最新补丁版本) |
| 渲染管线 | **HDRP 14.x** (High Definition Render Pipeline) |
| 输入系统 | **Unity Input System** (新系统, 非旧 Input Manager) |
| XR 插件 | OpenXR + XR Interaction Toolkit |
| 最低支持平台 | Windows 10 (64-bit) / Quest 3 (Android) |
| 推荐开发平台 | Windows 11 (RTX 3080+) / Ubuntu 22.04 |

### 必装模块
- Windows Build Support (IL2CPP)
- Android Build Support (IL2CPP) — Quest 端
- Documentation (离线文档)
- Language Pack — Chinese (Simplified)

---

## 2. HDRP 渲染管线配置

### 2.1 HDRP Asset 设置

创建 HDRP Volume Profile 路径：
```
Assets/Settings/HDRP/
├── DynastVR_HDRenderPipelineAsset.asset
├── DefaultVolumeProfile.asset
├── Quality/
│   ├── Ultra.asset
│   ├── High.asset
│   ├── Medium.asset
│   └── Low.asset
└── Compositor/
    └── CompositorSettings.asset
```

### 2.2 关键渲染参数

| 参数 | Ultra (PC VR) | High | Medium | Low (Quest Link) |
|------|---------------|------|--------|------------------|
| 最大分辨率 | 4K × 4K | 2K × 2K | 1080p | 1080p |
| 光线追踪 | 全光追 | 混合 | 仅 AO | 关闭 |
| 阴影分辨率 | 4096 | 2048 | 1024 | 512 |
| 阴影距离 | 500m | 300m | 150m | 100m |
| 云层质量 | 全体积 | 半体积 | 2D叠加 | 关闭 |
| LOD 层级 | 8 | 6 | 4 | 3 |
| 帧率目标 | 90fps | 90fps | 72fps | 72fps |

### 2.3 光照设置

```
光照配置:
├── 环境光: HDRI Sky (动态天空盒)
├── 方向光: 日光主光源 (Shadow = Contact Shadows + Cascade)
├── 反射探针: Box Projection + 混合距离 20m
├── 光探针: Adaptive Probe Volumes (APV)
├── 光追反射: Max 4 bounces, Roughness 0.0-1.0
└── 全局光照: Screen Space GI + Baked 混合
```

### 2.4 后处理效果

```
后处理体积 (Global Volume):
├── Bloom (阈值 0.9, 强度 0.3)
├── Tonemapping (ACES Filmic)
├── Color Grading (可动态切换日夜)
├── Vignette (关闭, VR 中会引起不适)
├── Motion Blur (关闭, VR 中会引起不适)
├── Depth of Field (仅过场动画启用)
├── Ambient Occlusion (GTAO, 半径 0.5m)
├── Screen Space Reflections (中等质量)
├── Subsurface Scattering (皮肤、植被)
└── Film Grain (关闭)
```

---

## 3. XR / VR 配置

### 3.1 XR 插件架构

```
XR Stack:
├── OpenXR Plugin
│   ├── Meta Quest Touch Controller Profile
│   ├── Hand Tracking Profile
│   └── Eye Tracking Profile
├── XR Interaction Toolkit (2.5+)
│   ├── XRRayInteractor (远程交互)
│   ├── XRDirectInteractor (直接交互)
│   ├── XRSimpleInteractable
│   └── XR Rig (玩家 rig)
├── XR Input (新 Input System)
│   ├── Action Map: VRControls
│   │   ├── PrimaryAction (Trigger)
│   │   ├── SecondaryAction (Grip)
│   │   ├── Thumbstick (Locomotion)
│   │   ├── PrimaryButton (A/X)
│   │   ├── SecondaryButton (B/Y)
│   │   └── Haptic (反馈)
│   └── Locomotion System
│       ├── Teleportation
│       ├── Continuous Move
│       └── Snap Turn
└── 多平台适配层
    ├── PC VR (SteamVR / Oculus Link)
    ├── Standalone (Quest 3)
    └── WebXR (远期)
```

### 3.2 VR 交互规则

**运动方式优先级：**
1. 飞行器/车辆内 → 座舱跟随，无独立 locomotion
2. 自由行走 → 连续移动 + 平滑转弯 (可选 snap turn)
3. 地球浏览 → 手势缩放/拖拽 (Google Earth 风格)

**交互原则：**
- 所有 UI 必须支持 World Space Canvas (VR 空间 UI)
- 菜单呼出：左手菜单按钮 / 手势 (手掌朝上)
- 抓取物体：Grip 键 + 物理碰撞
- 驾驶车辆：双手模拟方向盘 + 油门扳机

---

## 4. 目录结构规范

```
UnityProject/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/           # 核心系统 (Manager, Singleton, Events)
│   │   ├── GeoEngine/      # 地球引擎 (Cesium集成, 地形)
│   │   ├── RoadEngine/     # 路网引擎 (OSM, 车辆物理)
│   │   ├── CosmosEngine/   # 宇宙引擎 (天体, 黑洞)
│   │   ├── BuildSystem/    # 建造系统 (UGC, 地块)
│   │   ├── VR/             # VR交互脚本
│   │   ├── UI/             # UI控制器
│   │   └── [Feature]/      # 各功能模块独立文件夹
│   ├── Scenes/
│   │   ├── Main.unity              # 主场景
│   │   ├── Loading.unity           # 加载场景
│   │   ├── MainMenu.unity          # 主菜单
│   │   ├── Demo_Earth.unity        # 地球演示
│   │   ├── Demo_RoadTrip.unity     # 自驾演示
│   │   ├── Demo_Cosmos.unity       # 宇宙演示
│   │   └── Test_Room.unity         # VR测试房间
│   ├── Prefabs/
│   │   ├── Vehicles/       # 车辆预制体
│   │   ├── Aircraft/       # 飞行器预制体
│   │   ├── Environment/    # 环境预制体
│   │   └── Characters/     # 角色预制体
│   ├── Materials/
│   │   ├── Terrain/        # 地形材质
│   │   ├── Water/          # 水体材质
│   │   ├── Vehicles/       # 车辆材质
│   │   ├── Sky/            # 天空/大气材质
│   │   └── PostProcess/    # 后处理配置
│   ├── Textures/
│   ├── Models/
│   ├── Shaders/            # 自定义 Shader
│   ├── Audio/
│   ├── Resources/          # 运行时加载资源
│   ├── StreamingAssets/    # 流式加载 (Cesium tiles cache)
│   ├── Editor/             # 编辑器扩展
│   └── Plugins/            # 第三方插件
├── Packages/
│   └── manifest.json       # UPM 包依赖
├── ProjectSettings/        # Unity 项目设置
├── docs/                   # 项目文档 (repo root)
└── .gitignore
```

---

## 5. 核心依赖包 (Packages/manifest.json)

```json
{
  "dependencies": {
    "com.unity.render-pipelines.high-definition": "14.0.11",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.xr.openxr": "1.9.1",
    "com.unity.xr.interaction.toolkit": "2.5.2",
    "com.unity.xr.hands": "1.3.0",
    "com.unity.cesium": "1.7.0",
    "com.unity.probuilder": "5.2.2",
    "com.unity.textmeshpro": "3.0.6",
    "com.unity.addressables": "1.21.19",
    "com.unity.shadergraph": "14.0.11",
    "com.unity.visualeffectgraph": "14.0.11",
    "com.unity.ai.navigation": "1.1.5"
  }
}
```

---

## 6. 性能预算

### PC VR (推荐配置: RTX 3080 + i7-12700K)

| 指标 | 预算 |
|------|------|
| 帧率 | ≥90 fps (11.1ms/frame) |
| GPU 时间 | < 10ms |
| CPU 时间 | < 8ms |
| 内存 (GPU) | < 10 GB |
| 内存 (RAM) | < 16 GB |
| Draw Calls | < 3000 |
| Triangles/Frame | < 5M |

### Quest 3 (Stand-alone)

| 指标 | 预算 |
|------|------|
| 帧率 | ≥72 fps (13.9ms/frame) |
| GPU 时间 | < 12ms |
| CPU 时间 | < 10ms |
| 内存 (RAM) | < 6 GB |
| Draw Calls | < 500 |
| Triangles/Frame | < 750K |

---

## 7. 版本控制约定

- Unity 场景文件使用 YAML 序列化 (文本格式)
- 二进制大文件 (textures, models) → **Git LFS**
- LFS 追踪规则:
  ```
  *.png filter=lfs diff=lfs merge=lfs -text
  *.jpg filter=lfs diff=lfs merge=lfs -text
  *.tif filter=lfs diff=lfs merge=lfs -text
  *.fbx filter=lfs diff=lfs merge=lfs -text
  *.blend filter=lfs diff=lfs merge=lfs -text
  *.wav filter=lfs diff=lfs merge=lfs -text
  *.mp3 filter=lfs diff=lfs merge=lfs -text
  *.asset filter=lfs diff=lfs merge=lfs -text
  ```

---

## 8. 分支策略

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
