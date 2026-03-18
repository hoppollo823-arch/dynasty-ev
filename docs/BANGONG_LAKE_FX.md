# 班公湖特效系统 — Bangong Lake FX System

> **项目：** DynastyVR — 班公湖 POC（UE5）  
> **作者：** EV-effect（东方皇朝元宇宙部 · 特效工程师）  
> **日期：** 2026-03-18  
> **状态：** 初版 V1.0

---

## 1. Niagara 粒子系统

### 1.1 海鸥群（Seagull Flock）

| 参数 | 值 |
|---|---|
| 粒子数量 | 20–30 只（Niagara Sim Target: GPU） |
| 网格体 | `SM_Seagull_LOD0`（骨骼网格体，12–18 骨骼），`SM_Seagull_LOD1`（简化为 Billboard） |
| 飞行行为 | Boids 算法驱动（详见 §2） |
| 动画 | Skeletal Mesh 动画蓝图，含 Idle/Flap/Dive 三态混合 |
| 翅膀频率 | Idle: 1.2Hz, Dive: 3.5Hz, Flap: 2.0Hz |
| 生命周期 | 循环（Emitter Loop），无死亡 |
| 音频 | Niagara Audio Emitter `SFX_SeagullCall`，随机触发间隔 4–12s |

**Niagara System 路径：**  
`/Content/FX/Seagull/NSeagull_Flock.uasset`

**Emitter 结构：**

```
NSeagull_Flock
├── Emitter: SeagullSpawn (GPU Sim)
│   ├── Spawn Burst (一次性 25 particles)
│   ├── Particle Update: BoidsForce (自定义模块)
│   ├── Particle Update: Attractor (湖岸路径点)
│   ├── Mesh Renderer: Skeletal Mesh
│   └── Audio Renderer: SeagullCall
└── Emitter: SeagullTrail (CPU Sim, 仅近景)
    ├── Spawn per particle
    └── Ribbon Renderer (空气扰动尾迹)
```

### 1.2 水面飞溅（Water Splash）

| 参数 | 值 |
|---|---|
| 触发条件 | 车辆 Actor 碰到浅水区 Volume |
| 粒子数量 | 每次 15–40 个 spray 粒子 + 8–12 个 foam 粒子 |
| 粒子尺寸 | Spray: 3–12cm 随机, Foam: 1–4cm |
| 速度 | Spray: 2–6 m/s（向上 + 径向）, Foam: 0.1–0.5 m/s |
| 颜色 | Spray: 白色半透明, Foam: 浅白（略带湖水蓝） |
| 淡出 | Scale Alpha: LifeCurve（0→1→0），寿命 0.8–2.0s |
| 物理 | 受重力影响，重力缩放 1.0 |

**Niagara System 路径：**  
`/Content/FX/Water/NSeagull_Splash.uasset`

**实现要点：**

- 蓝图 `BP_Vehicle` 通过 `OnComponentHit` 事件触发 Niagara Component Spawn
- 碰撞点作为 Niagara User Parameter `User.SpawnLocation` 传入
- 浅水区用 `BoxCollision` Tag `"ShallowWater"` 标记
- Splash 方向根据车辆速度向量计算：`Tangent = Normalize(Velocity); Normal = (0,0,1); Radial = Cross(Tangent, Normal)`

### 1.3 经幡飘动（Prayer Flag Animation）

| 参数 | 值 |
|---|---|
| 类型 | 顶点动画材质（非粒子） |
| 材质 | `M_PrayerFlag_Animated` |
| 动画方式 | 顶点位移着色器（World Position Offset） |
| 波形 | 复合正弦波：低频大振幅 + 高频小振幅 |
| 振幅 | 主波: 8–15cm, 次波: 2–4cm |
| 频率 | 主波: 0.5–1.2Hz, 次波: 2.0–4.0Hz |
| 衰减 | 从旗杆端（固定边）到旗尾递增 |
| 风向影响 | Material Parameter Collection `MPC_Wind` 驱动 |

**材质节点关键结构：**

```
World Position Offset:
├── DistanceToPole (UV.x as pole reference)
│   ├── Attenuation = saturate(DistanceToPole)
│   ├── PrimaryWave = sin(Time * Freq.x + DistanceToPole * Phase.x) * Amp.x * Attenuation
│   ├── SecondaryWave = sin(Time * Freq.y + DistanceToPole * Phase.y) * Amp.y * Attenuation
│   └── WindOffset = WindDirection * (PrimaryWave + SecondaryWave) * WindStrength
└── Output: WorldOffset + WindOffset * WPOStrength
```

**MPC_Wind 参数：**

| Parameter | 类型 | 默认值 |
|---|---|---|
| `WindDirection` | Vector | (-0.7, 0.7, 0.0) — 西北风 |
| `WindStrength` | Scalar | 1.0 (0–3 range) |
| `WindTurbulence` | Scalar | 0.4 |

### 1.4 风沙/浮尘（High-Altitude Dust）

| 参数 | 值 |
|---|---|
| 粒子数量 | 200–500 个（GPU Sim） |
| 粒子尺寸 | 0.5–3.0cm（Screen Size: 0.002–0.008） |
| 透明度 | 0.05–0.20（极淡，仅氛围感） |
| 运动 | 随风飘散 + 轻微布朗运动 |
| 颜色 | 暖灰/沙色 `(0.82, 0.78, 0.70)` |
| 寿命 | 8–20s |
| 生成区域 | 地面以上 0.5–3m，半径 50m 圆柱体 |
| 密度曲线 | 随风速线性缩放 |

**Niagara System 路径：**  
`/Content/FX/Atmosphere/NAtmos_Dust.uasset`

### 1.5 水面涟漪（Water Ripples）

| 参数 | 值 |
|---|---|
| 触发方式 | 射线检测（雨滴）/ 投掷事件（石子） |
| 纹理 | `T_Ripple_Normal`（径向波纹法线贴图） |
| 波纹半径 | 展开速度: 1.5–3.0 m/s |
| 波纹强度 | 初始 0.8，按指数衰减，持续 3–5s |
| 波纹数量 | 雨天: 20–40 同时活跃; 单次石子: 1 个 |
| 材质混合 | 权重写入 Render Target → 混合到水面材质 Height/NMA |
| 层级 | 持久化 Actor `BP_WaterRippleManager` 管理，使用 Render Target 2D |

**实现架构：**

```
BP_WaterRippleManager
├── RenderTarget2D (R16F, 1024×1024, 覆盖湖面)
├── RipplePool: 固定 32 个 ripple slot（循环使用）
├── Draw Each Ripple:
│   ├── RT_WriteRipple (Material Function: 径向波纹写入)
│   └── Output → Additive blend to main RT
└── Water Material Samples RT → Displacement + Normal
```

---

## 2. 海鸥行为设计（Seagull Behavior System）

### 2.1 Boids 算法实现

在 Niagara 自定义模块 `BoidsForce`（C++ / Custom HLSL）中实现：

| 规则 | 参数 | 默认值 |
|---|---|---|
| **分离 Separation** | 避让半径 | 3.0m |
| | 最大排斥力 | 2.5 m/s² |
| **对齐 Alignment** | 感知半径 | 10.0m |
| | 对齐权重 | 0.8 |
| **聚合 Cohesion** | 感知半径 | 15.0m |
| | 聚合权重 | 0.6 |
| **边界 Bound** | 湖面活动范围 | 半径 200m 圆柱 |
| | 边界力 | 线性递增，距边界 <50m 开始 |
| **高度约束** | 最低高度 | 水面 + 3m |
| | 最高高度 | 水面 + 40m |
| **随机扰动** | 噪声频率 | 0.3 Hz |
| | 噪声强度 | 0.5 m/s² |

**飞行路径（Patrol Path）：**

定义 6–8 个 Path Point 沿湖岸分布，海鸥群围绕这些点做循环巡逻：

```
PatrolPoints:
  P1: North Shore   (X: 100, Y: -150, Z: +8)
  P2: NE Point       (X: 180, Y: -50,  Z: +10)
  P3: East Shore     (X: 200, Y: 100,  Z: +6)
  P4: SE Bay         (X: 120, Y: 200,  Z: +9)
  P5: South Shore    (X: -50, Y: 180,  Z: +7)
  P6: NW Cliffs      (X: -150, Y: -100, Z: +15)
```

- Attractor Force 将最近的 N 只海鸥引向当前目标点
- 到达阈值：距离 < 20m → 切换到下一个 Path Point
- 偶尔（10% 概率）触发 **俯冲行为**：短暂下降至水面 +1m，然后拉起

### 2.2 Niagara Audio Emitter

| 参数 | 值 |
|---|---|
| 音频资产 | `SFX_Seagull_Call_01` ~ `SFX_Seagull_Call_04`（4 个变体） |
| 触发模式 | Random Range Float: 4–12s |
| 音量 | 距离衰减：0m: 0.8, 50m: 0.4, 150m: 0.1, 200m+: 0 |
| 多普勒 | 关闭（视觉距离足够小） |
| 同时发声限制 | Max Concurrent: 3 |

### 2.3 LOD 策略

| LOD 级别 | 距离阈值 | 表现 |
|---|---|---|
| LOD0 | < 80m | 完整骨骼网格体 + 翅膀动画 + 音频 |
| LOD1 | 80–200m | 简化骨骼（6 骨骼）+ Billboard 叠加 |
| LOD2 | 200–400m | 纯 Billboard（十字交叉面片）+ 静态 |
| LOD3 | > 400m | 2D Sprite（三角形），淡出至消失 |

---

## 3. 风效果（Wind System）

### 3.1 风参数

| 参数 | 值 |
|---|---|
| 主导风向 | 西北风（从 NW 吹向 SE） |
| 风向向量 | `(-0.707, 0.707, 0.0)` 归一化 |
| 风速范围 | 15–30 km/h（4.2–8.3 m/s） |
| 基准风速 | 22 km/h（6.1 m/s） |
| 阵风 | 正弦叠加：振幅 ±8 km/h，周期 15–30s |
| 风向偏移 | ±15° 摆动 |

### 3.2 风对各系统的影响

| 系统 | 影响方式 |
|---|---|
| **水面波纹** | Gerstner 波方向 `WindDirection`，波高随风速缩放 |
| **经幡** | `MPC_Wind.WindDirection` 直接驱动 WPO 偏移方向 |
| **风沙/浮尘** | 粒子 velocity 添加 `WindDirection × WindSpeed` |
| **海鸥群** | Boids 增加 Wind Offset Force，大风时降低飞行高度 |
| **旗帜布料** | Chaos Cloth Wind Direction & Strength 同步 |

### 3.3 风向可视化

- 地面浮尘粒子（§1.4）密度与风速成正比
- 当风速 > 25 km/h 时，地面增加 **水平沙尘带** 粒子（半透明条带状，贴地面 0–1.5m）
- 远处可见的 **风吹雪/沙尘线**（仅 LOD0–LOD1 时渲染）

### 3.4 全局风管理器

蓝图 `BP_WindManager`（单例）：
- 管理 `MPC_Wind` 全局参数
- 通过 Timeline 驱动风速/风向变化（可绑定天气系统）
- 暴露 `GetWindDirection()` / `GetWindStrength()` 给其他系统

---

## 4. 动物效果（Wildlife FX）

### 4.1 藏野驴群（Tibetan Wild Ass）

| 参数 | 值 |
|---|---|
| 数量 | 3–5 只 |
| 网格体 | `SM_WildAss_LOD0`（低面数骨骼网格体，~800 tris） |
| 移动速度 | 1.5–3.0 km/h（慢步/漫步） |
| 移动模式 | 沿预定义路径缓慢行走，偶尔停下 |
| 活动区域 | 远处湖岸草地，距玩家 > 200m |
| 触发 LOD | LOD0: < 300m, LOD1: 300–600m, >600m: 不渲染 |
| 行为 | 循环：Walk 20s → Idle 10s → Walk 20s |
| 阴影 | 仅 LOD0 投射阴影（远处简化） |

**实现方式：** `BP_WildAssHerd` Actor，使用 `Spline` 路径驱动移动，每只驴偏移路径 2–5m。

### 4.2 土拨鼠（Marmot）

| 参数 | 值 |
|---|---|
| 数量 | 0–2 只（概率生成） |
| 网格体 | `SM_Marmot_LOD0`（~300 tris） |
| 位置 | 湖岸岩石区域，距玩家 > 50m |
| 行为 | 静止观望 → 钻入洞穴（简化为 Scale→0） |
| 触发 | 玩家进入 50m 范围后触发钻洞 |
| 音频 | 可选：短促叫声 `SFX_Marmot` |

**LOD：** 简化为静态网格体，无骨骼动画。远处仅可见小凸起。

---

## 5. 天气效果（Weather System）

### 5.1 天气模式概览

| 天气 | 出现概率 | 持续时间 | 亮度系数 |
|---|---|---|---|
| ☀️ 晴天（主模式） | 60% | 3–5 min | 1.0 |
| ⛅ 多云 | 25% | 2–4 min | 0.6–0.8 |
| 🌧️ 雨天（偶尔） | 10% | 1–3 min | 0.4–0.6 |
| 🌪️ 风暴（罕见） | 5% | 1–2 min | 0.2–0.4 |

天气切换通过 `BP_WeatherManager` 管理，使用 Lerped Transition（15–30s 过渡）。

### 5.2 各天气模式特效

#### ☀️ 晴天 (Clear Sky)

- 天空：`BP_SkyAtmosphere` 默认配置
- 太阳光：强度 75,000 lux（高海拔强烈）
- 阴影：锐利，级联阴影 3 级
- 水面：强反射（Screen Space + Planar Reflection）
- 全局：高对比度，色彩鲜明

#### ⛅ 多云 (Cloudy)

- 天空：Cloud Layer 增加，覆盖率 50–70%
- **云影效果：** 使用大型半透明 Plane，贴 cloud shadow 贴图，随风缓慢移动（速度 5–10 km/h）
- 云影贴图：`T_CloudShadow`（2048×2048 平铺噪声）
- 太阳光强度：降至 30,000 lux
- 水面反射减弱

#### 🌧️ 雨天 (Rain)

- **湖面涟漪：** 触发 `NWater_RippleRain` 粒子系统，密集涟漪
- 水面颜色：RGB 偏移向 `(0.3, 0.35, 0.4)`（变暗变灰）
- 天空：灰暗，覆盖 85–95%
- 雨粒子：`NRain_Heavy`（CPU Sim，500–1000 粒子）
- 地面：启用 Wetness 材质参数（`MPC_Weather.RainWetness = 0.8`）
- 后处理：Bloom 降低，对比度降低

#### 🌪️ 风暴 (Dust Storm)

- **沙尘粒子：** `NAtmos_DustStorm` — 密度 10x，覆盖整个视野
- 能见度：降至 50–100m（指数雾密度 ↑）
- 天空颜色：偏黄/棕 `(0.75, 0.65, 0.45)`
- 风速：提升至 40–60 km/h
- 后处理：
  - Lens Dust（镜头灰尘贴图）
  - 饱和度降低 `Desaturation = 0.3`
  - 暗角加重
- 海鸥：隐藏（躲避风暴行为）

### 5.3 天气系统架构

```
BP_WeatherManager
├── CurrentState: Enum (Clear/Cloudy/Rain/Storm)
├── TransitionProgress: 0→1 over 15-30s
├── Controls:
│   ├── BP_StormController (风速/风向)
│   ├── BP_CloudController (云层/云影)
│   ├── BP_RainController (雨粒子/涟漪)
│   └── BP_PostProcessController (后处理参数)
└── Sync:
    ├── MPC_Weather (Material Parameter Collection)
    └── BP_PostProcessVolume (通过 BP 绑定)
```

---

## 6. 后处理效果（Post Processing）

### 6.1 效果列表

| 效果 | 说明 | 值/范围 |
|---|---|---|
| **Bloom** | 高海拔阳光反射强烈 | Intensity: 0.8–1.5 |
| **Lens Flare** | 太阳直射时适度眩光 | Intensity: 0.6–1.0 |
| **色彩分级** | 高海拔紫外线 → 高饱和度 | Saturation: (1.15, 1.15, 1.15, 1.0) |
| **暗角 (Vignette)** | 轻微边缘压暗 | Intensity: 0.15–0.25 |
| **对比度** | 高海拔清晰空气 | Contrast: 1.05–1.10 |

### 6.2 高海拔特殊处理

- **紫外线偏移：** 暗部轻微偏蓝 `Shadows: (0.98, 0.98, 1.02)`
- **空气散射：** 远处物体轻微蓝色散射（指数雾颜色偏蓝 `(0.6, 0.7, 0.9)`）
- **太阳光晕：** 太阳周边轻微 Halo（Scale 1.2, Threshold 0.95）

---

## 7. UE5 Post Process Volume 配置参数

### 7.1 全局 Post Process Volume 设置

**Actor：** `BP_PostProcessVolume`（绑定到 `BP_PostProcessController`）

```
BP_PostProcessVolume
├── Settings
│   ├── Blend Radius: 0 (全场景覆盖)
│   ├── Blend Priority: 1
│   └── Infinite Extent (Unbound): ✓
```

### 7.2 曝光设置（Exposure）

| 参数 | 晴天 | 多云 | 雨天 | 风暴 |
|---|---|---|---|---|
| Metering Mode | Auto Exposure (Histogram) | Auto Exposure | Auto Exposure | Manual |
| Min Brightness | -2.0 | -1.5 | -1.0 | -0.5 |
| Max Brightness | 8.0 | 6.0 | 4.0 | 2.0 |
| Speed Up | 3.0 | 2.5 | 2.0 | 1.0 |
| Speed Down | 1.0 | 1.0 | 0.8 | 0.5 |
| Exposure Compensation | 0.3 | 0.0 | -0.3 | -0.5 |
| Low Percent | 80.0 | 80.0 | 80.0 | 80.0 |
| High Percent | 98.0 | 98.0 | 98.0 | 98.0 |

### 7.3 色彩映射（Color Grading）

#### ACES 色彩映射配置

```
Color Grading:
├── Global:
│   ├── Saturation:  (1.15, 1.15, 1.15, 1.0)    ← 高海拔高饱和
│   ├── Contrast:    (1.08, 1.08, 1.08, 1.0)    ← 清晰空气
│   ├── Gain:        (1.02, 1.01, 0.98, 1.0)    ← 轻微暖调
│   ├── Offset:      (0.0,  0.0,  0.02, 0.0)    ← 暗部偏蓝
│   └── Gamma:       (1.0,  1.0,  1.0,  1.0)
│
├── Shadows:
│   ├── Saturation:  (1.05, 1.05, 1.10, 1.0)
│   ├── Contrast:    (1.10, 1.10, 1.10, 1.0)
│   └── Color:       (0.98, 0.98, 1.02, 1.0)    ← 暗部蓝偏
│
├── Midtones:
│   ├── Saturation:  (1.15, 1.15, 1.15, 1.0)
│   └── Contrast:    (1.05, 1.05, 1.05, 1.0)
│
├── Highlights:
│   ├── Saturation:  (1.10, 1.08, 1.05, 1.0)
│   └── Gain:        (1.05, 1.03, 1.00, 1.0)    ← 高光暖调
│
└── Film:
    ├── Toe:         0.55
    ├── Shoulder:    0.75
    ├── Slope:       1.0
    └── Film Slope Override: 1.0

Tone Curve Settings (ACES):
├── ACES Blue Fix:    Enabled
├── ACES Min White:   0.0
├── ACES Max White:   12.0
└── ACES Brightness:  1.0
```

### 7.4 Bloom 配置

| 参数 | 晴天 | 多云 | 雨天 |
|---|---|---|---|
| Method | Standard | Standard | Convolution |
| Intensity | 1.2 | 0.8 | 0.4 |
| Threshold | 0.95 | 0.90 | 0.80 |
| Size Scale | 1.0 | 1.0 | 1.0 |
| Size X1 (Convolution) | 0.3 | 0.3 | 0.2 |
| Size X2 | 0.8 | 0.8 | 0.5 |
| Size X3 | 2.0 | 2.0 | 1.5 |
| Size X4 | 4.0 | 4.0 | 3.0 |
| Size X5 | 8.0 | 8.0 | 6.0 |
| Size X6 | 16.0 | 16.0 | 12.0 |

### 7.5 Lens Flare 配置

```
Lens Flares:
├── Intensity:        0.8 (晴天) / 0.5 (多云) / 0.2 (雨天)
├── Tint:             (1.0, 0.95, 0.9, 1.0) — 暖色调
├── Bokeh Size:       8.0
├── Threshold:        0.98
└── Customized Material: M_LensFlare_Custom (可选，太阳形状光晕)
```

### 7.6 暗角 (Vignette) 配置

| 参数 | 值 |
|---|---|
| Intensity | 0.20 (默认) / 0.35 (风暴) |
| Size | 0.8 |
| Color | Black (0, 0, 0, 1) |

### 7.7 其他 Post Process 设置

| 参数 | 值 |
|---|---|
| **Ambient Occlusion** | Intensity: 0.5, Radius: 200, Falloff: 1.0 |
| **Screen Space Reflections** | Quality: 100%, Max Roughness: 0.4 |
| **Motion Blur** | Amount: 0.5, Max: 2.0, Per Object: 3.0 |
| **Anti-Aliasing** | Method: TAA (Temporal), Quality: Normal |
| **Depth of Field** | 晴天关闭，可选电影模式开启 |
| **Chromatic Aberration** | Intensity: 0.05（极轻，仅边缘） |
| **Film Grain** | Intensity: 0.02（极轻，增加胶片质感） |

### 7.8 天气切换 Post Process 变化（Lerp 参数）

`BP_PostProcessController` 使用 `Timeline` 在天气切换时平滑过渡：

```
Transition: Clear → Cloudy (15s)
├── Exposure.Compensation:    0.3 → 0.0      (Lerp)
├── Bloom.Intensity:          1.2 → 0.8      (Lerp)
├── Bloom.Threshold:          0.95 → 0.90    (Lerp)
├── Color.Saturation:         1.15 → 1.05    (Lerp)
├── LensFlare.Intensity:      0.8 → 0.5      (Lerp)
├── Fog.Density:              0.001 → 0.005  (Lerp)
└── Vignette.Intensity:       0.20 → 0.25    (Lerp)
```

```
Transition: Cloudy → Rain (20s)
├── Exposure.Compensation:    0.0 → -0.3     (Lerp)
├── Bloom.Intensity:          0.8 → 0.4      (Lerp)
├── Color.Saturation:         1.05 → 0.95    (Lerp)
├── Color.Contrast:           1.05 → 0.95    (Lerp)
├── Fog.Density:              0.005 → 0.015  (Lerp)
├── Fog.Color:                (0.7,0.75,0.9) → (0.5,0.55,0.65)
└── DirectionalLight.Intensity: 30000 → 15000 (Lerp)
```

```
Transition: Rain → Storm (15s)
├── Exposure.Compensation:    -0.3 → -0.5    (Lerp)
├── Fog.Density:              0.015 → 0.05   (Lerp)
├── Fog.Color:                → (0.65, 0.55, 0.45)  (沙尘色)
├── Vignette.Intensity:       0.25 → 0.40    (Lerp)
├── Desaturation:             0.0 → 0.3      (Lerp)
└── Skylight.Intensity:       → 0.3          (Lerp)
```

---

## 8. 文件结构

```
Content/FX/
├── Seagull/
│   ├── NSeagull_Flock.uasset          # 海鸥群体 Niagara System
│   ├── SM_Seagull_LOD0.fbx            # 海鸥骨骼网格体 LOD0
│   ├── SM_Seagull_LOD1.fbx            # 海鸥骨骼网格体 LOD1
│   ├── ABP_Seagull.uasset             # 海鸥动画蓝图
│   └── SFX_Seagull_Call_01~04.wav     # 海鸥叫声 × 4
├── Water/
│   ├── NSeagull_Splash.uasset         # 水花 Niagara System
│   ├── NWater_RippleRain.uasset       # 雨天涟漪 Niagara System
│   ├── T_Ripple_Normal.uasset         # 涟漪法线贴图
│   └── MF_RippleWriter.uasset         # Ripple 写入 Material Function
├── Atmosphere/
│   ├── NAtmos_Dust.uasset             # 浮尘 Niagara System
│   ├── NAtmos_DustStorm.uasset        # 风暴风沙 Niagara System
│   └── T_Dust_Sprite.uasset           # 灰尘 Sprite 纹理
├── Rain/
│   └── NRain_Heavy.uasset             # 雨滴 Niagara System
└── Weather/
    └── NWeather_CloudShadow.uasset    # 云影平面材质
```

```
Blueprints/
├── FX/
│   ├── BP_PostProcessVolume.uasset    # 后处理 Volume
│   ├── BP_PostProcessController.uasset# 天气-后处理联动
│   ├── BP_WeatherManager.uasset       # 天气管理器
│   ├── BP_WindManager.uasset          # 风力管理器
│   ├── BP_WaterRippleManager.uasset   # 涟漪管理器
│   ├── BP_WildAssHerd.uasset          # 藏野驴群
│   └── BP_Marmot.uasset               # 土拨鼠
└── Vehicles/
    └── BP_Vehicle.uasset              # 车辆（含水面碰撞检测）
```

```
Materials/
├── M_PrayerFlag_Animated.uasset      # 经幡顶点动画材质
├── M_Water_Surface.uasset            # 水面材质（采样 Ripple RT）
├── M_LensFlare_Custom.uasset         # 自定义光晕材质
└── MPC_Wind.uasset                   # Wind Material Parameter Collection
```

---

## 9. 性能预算参考

| 系统 | 目标帧率 | GPU 时间预算 | 备注 |
|---|---|---|---|
| 海鸥群 | 60 FPS | ≤ 1.0ms | GPU Sim，远处 LOD 降级 |
| 水花 | 60 FPS | ≤ 0.3ms | 事件触发，非持续 |
| 经幡 | 60 FPS | ≤ 0.5ms | 材质 WPO，< 20 面旗帜 |
| 风沙/浮尘 | 60 FPS | ≤ 0.8ms | GPU Sim，按风速调密度 |
| 涟漪 | 60 FPS | ≤ 0.5ms | RT 更新，固定 32 slot |
| 藏野驴 | 60 FPS | ≤ 0.3ms | 仅远处 LOD |
| 天气系统 | 60 FPS | ≤ 0.2ms | 逻辑开销 |
| 后处理 | 60 FPS | ≤ 2.0ms | Bloom + Color Grading |
| **总计** | **60 FPS** | **≤ 5.6ms** | — |

---

## 10. TODO & 后续

- [ ] 海鸥 Boids 模块 C++ 实现并 Profile
- [ ] 涟漪 RT 方案测试（确认 GPU 开销）
- [ ] 经幡顶点动画在大量旗帜时的性能测试
- [ ] 天气切换的美术调参（需要美术协助）
- [ ] 藏野驴动画资源采购/制作
- [ ] 土拨鼠简化方案确认
- [ ] 音效资源收集（海鸥、风声、雨声）
- [ ] VR 双眼渲染性能验证

---

*EV-effect | 东方皇朝元宇宙部 | DynastyVR POC v1.0*
