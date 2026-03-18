# 班公湖高海拔大气系统 — UE5 配置文档

> **项目：** DynastyVR — 班公湖 POC（UE5）  
> **作者：** EV-cosmos / 天体大气工程组  
> **海拔基准：** 4,242m（班公湖湖面）  
> **UE5 版本：** 5.4+

---

## 1. UE5 Sky Atmosphere 高海拔配置

### 1.1 物理背景

| 参数 | 海平面 | 班公湖（4,242m） | 比值 |
|------|--------|-------------------|------|
| 大气压 | 1013 hPa | ~600 hPa | 0.59 |
| 空气密度 | 1.225 kg/m³ | ~0.81 kg/m³ | **~60%** |
| 瑞利散射分子数 | 基准 | 减少 ~40% | 0.6 |
| 米氏散射（气溶胶） | 基准 | 显著降低 | ~0.3–0.4 |
| 臭氧柱量 | 基准 | 略高（高原效应） | ~1.05 |

### 1.2 视觉特征

| 特征 | 平原 | 班公湖（目标） |
|------|------|----------------|
| 天穹颜色 | 浅蓝 | **深蓝**（Rayleigh 散射减弱，蓝光穿透力增强） |
| 地平线附近 | 灰白 | **略白**（残余气溶胶散射） |
| 太阳圆盘 | 黄白 | **偏白偏蓝**（UV 强，散射少） |
| 阴影 | 柔和散射光 | **硬阴影**（大气散射填充光减少） |
| 能见度 | 10–20 km | **50 km+** |

### 1.3 UE5 Sky Atmosphere 组件设置

在 UE5 中，通过 `SkyAtmosphere` 组件控制天空大气效果。以下为高海拔参数建议。

---

## 2. UE5 Sky Atmosphere 具体参数

### 2.1 SkyAtmosphere 组件参数

```
SkyAtmosphere (Component)
├── Transform
│   └── Planet Center Position: 保持默认（由场景原点决定）
│
├── Mie Scattering Scale:        0.0025    （默认 0.006，降至 ~42%）
├── Mie Absorption Scale:        0.0012    （默认 0.003，降至 ~40%）
├── Mie Phase Function G:        0.80      （保持，前向散射特性不变）
│
├── Rayleigh Scattering Scale:   0.0020    （默认 0.0028，降至 ~71%）
├── Rayleigh Exponential Distribution: 8.0 （默认 8.0，保持）
│
├── Sky Luminance Factor:        (1.0, 1.0, 1.0)  默认
│
├── Ground Albedo:               (0.35, 0.30, 0.22)  （褐色地面）
│   │                                               └── RGB 近似褐土色
│
├── Multi Scattering:            1.0       （保持默认，高海拔仍需多次散射模拟）
│
├── Aerial Perspective:          启用
│   ├── Aerial Perspective Start Depth: 0.1 km
│   └── Aerial Perspective Distance:    100.0 km
│
└── Sky Atmosphere:              使用物理大气模型（Physically Based）
```

### 2.2 参数详解

#### Mie Scattering Scale（米氏散射强度）

| 项目 | 值 |
|------|----|
| UE5 默认 | 0.006 |
| **班公湖建议** | **0.0020 – 0.0030** |
| 物理依据 | 高空气溶胶浓度仅为海平面的 30–40% |
| 视觉效果 | 地平线雾霭减少，天穹更纯净 |

**推荐值：`0.0025`**（取中间值，可微调）

#### Rayleigh Scattering Scale（瑞利散射强度）

| 项目 | 值 |
|------|----|
| UE5 默认 | 0.0028 |
| **班公湖建议** | **0.0018 – 0.0022** |
| 物理依据 | 大气密度约为海平面 60%，分子散射相应减弱 |
| 视觉效果 | 天穹深蓝（非平原的浅蓝），高天更暗 |

**推荐值：`0.0020`**

#### Ground Albedo（地面反照率）

| 地面类型 | 典型 Albedo | 班公湖建议 |
|----------|-------------|-----------|
| 湖面 | 0.06 – 0.10 | `0.08` |
| 褐色裸土 | 0.25 – 0.35 | `0.30` |
| 草甸 | 0.20 – 0.30 | `0.25` |
| 积雪 | 0.70 – 0.90 | `0.80`（仅冬季/远山） |

**建议统一取褐色地面近似值：`RGB(0.35, 0.30, 0.22)`**

### 2.3 Directional Light 配合参数

```
Directional Light (太阳光)
├── Intensity:              10.0 lux     （默认 ~10，可升至 13–14 模拟高海拔增益）
├── Temperature:            5500 K       （偏白，高海拔 UV 强）
├── Cast Shadows:           True
│   └── Shadow Map Resolution: 2048
│   └── Cascade Shadow Distribution: 0.5
│   └── Shadow Bias:        适配地形
├── Volumetric Scattering:  启用
│   └── Scattering Intensity: 0.3       （低值，高空气溶胶少）
└── Atmosphere Sun Light Index: 0        （绑定 SkyAtmosphere）
```

#### 日照强度补偿

| 条件 | 强度倍率 | 说明 |
|------|---------|------|
| 平原基准 | 1.0× | Sea Level |
| **班公湖（晴天）** | **1.3 – 1.4×** | 大气衰减减少 30–40% |
| 班公湖（多云） | 0.4 – 0.7× | 云层遮蔽 |

> ⚠️ UE5 的 `SkyAtmosphere` 会自动根据太阳角度和大气参数计算光照衰减。  
> 如使用物理大气模型，`Directional Light` 的 `Intensity` 应保持默认值（~10 lux），  
> 由大气系统自动调节。手动提升强度仅在非物理大气管线中使用。

### 2.4 Moon Light 参数

```
Moon Light (Directional Light)
├── Intensity:              0.1 lux     （满月亮度）
├── Temperature:            4150 K       （偏暖白）
├── Cast Shadows:           True
│   └── Shadow Map Resolution: 1024
└── 月相由 Timeline 控制（0.0 – 1.0）
```

---

## 3. 云层系统

### 3.1 UE5 Volumetric Cloud 配置

使用 `VolumetricCloud` 组件实现体积云效果。

```
VolumetricCloud (Component)
├── Cloud Layer
│   ├── Layer Bottom Altitude:   6.0 km   （云底高度 6,000m）
│   ├── Layer Height Range:      4.0 km   （云层厚度 6,000–10,000m）
│   └── Tracing Start Max Distance: 80 km
│
├── Volumetric Material:         CloudMaterial（见下方）
│
├── Reflections:                 启用（湖面反射需要）
│
├── Shadow:                      启用
│   └── Shadow Scale:           0.5
│   └── Shadow Map Resolution:  512
│
└── Transmittance:               启用
    └── 基于云密度计算透光
```

### 3.2 班公湖典型云型

| 云型 | 国际分类 | 高度 | 出现频率 | UE5 实现方式 |
|------|---------|------|---------|-------------|
| **稀疏卷云** | Cirrus (Ci) | 8–10 km | **常见** | 纤维状纹理贴图 + 高空薄层 |
| **积云** | Cumulus (Cu) | 2–4 km | 偶有（中午） | 标准 Volumetric Cloud，中等密度 |
| **积雨云** | Cumulonimbus (Cb) | 2–15 km | 夏季偶现 | 远处大体积云柱 + 阴影 |

### 3.3 云量与覆盖率

```
云覆盖参数（Cloud Coverage）
├── 总覆盖率：        20% – 40%（少云/晴间多云）
├── 卷云覆盖率：      10% – 20%（高空薄云）
├── 积云覆盖率：      5% – 15%（中午地面加热后出现）
└── 积雨云覆盖率：    0% – 5%（仅夏季午后远处可见）
```

**UE5 中通过 `Cloud Material` 的密度函数和遮罩贴图控制覆盖率。**

### 3.4 云材质建议

```
CloudMaterial (Material)
├── 混合模式：        Translucent
├── 着色模型：        Default Lit
├── Opacity：         基于 Perlin Noise + Worley Noise
│   └── Density:     0.02 – 0.15（低密度，稀薄云层）
├── 底部暗度：        0.3（云底稍暗）
├── 散射颜色：        (1.0, 0.98, 0.95)（白色偏暖）
├── 边缘软化：        开启（卷云边缘消散效果）
└── 时间变化：        通过 Parameter Collection 驱动云层移动和变化
```

### 3.5 云层动态

| 参数 | 值 | 说明 |
|------|-----|------|
| 风速（高空） | 30–60 km/h | 卷云快速漂移 |
| 风向 | 西风带（W → E） | 班公湖位于西风带影响区 |
| 变化速度 | 慢（10–15 分钟一变） | 少云天气云型变化缓慢 |

---

## 4. 太阳光照系统

### 4.1 Directional Light 配置

```
DirectionalLight（主光源 - 太阳）
├── 组件设置
│   ├── Light Component
│   │   ├── Intensity:                 10.0 lux（物理大气下默认即可）
│   │   ├── Temperature:               5500 K
│   │   ├── Cast Shadows:              True
│   │   ├── Shadow Map Resolution:     2048
│   │   ├── Contact Shadows:           启用
│   │   └── Volumetric Scattering:     启用
│   │
│   └── Atmosphere / Fog Data
│       ├── Affects Atmosphere:        True
│       └── Affects Fog:               True
│
├── 阴影配置
│   ├── Dynamic Shadow Distance:       20000 m
│   ├── Num Dynamic Shadow Cascades:   4
│   ├── Cascade Distribution Exponent: 0.5
│   └── Shadow Bias:                   0.01 / 0.01
│
└── 光照质量
    ├── Ray Tracing Shadows:           可选（高配）
    ├── Soft Shadow Area Light:        启用
    └── Penumbra Width:                适中
```

### 4.2 高海拔日照特性

| 特性 | 平原 | 班公湖（4,242m） | UE5 实现 |
|------|------|------------------|---------|
| 日照强度 | 基准 | **+30–40%** | 物理大气自动处理 / 手动 ×1.3 |
| 色温 | 5000–5500K | **5500K（偏白偏蓝）** | Temperature = 5500 |
| 阴影 | 较柔和 | **硬阴影** | 减少 Scattering，增加 Direct |
| 散射光 | 较多 | **较少** | Mie/Rayleigh Scale 降低 |
| UV 强度 | 基准 | **显著增强** | 后处理 Bloom + 色调映射 |

### 4.3 黄金时刻（Golden Hour）

班公湖黄金时刻约在 **日落前 1 小时**（约 6:30 PM – 7:30 PM），太阳角度 5°–15°。

```
黄金时刻光照参数
├── Sun Elevation:         5° – 15°
├── Temperature:           3500 – 4500 K（金橙色）
├── Intensity:             3.0 – 6.0 lux（减弱）
├── 天穹颜色:
│   ├── 顶部：              深蓝 → 紫蓝
│   ├── 中部：              金橙色
│   └── 地平线：            金红色 → 橙色
├── 阴影：
│   ├── 长度：              极长（太阳低角度）
│   ├── 颜色：              偏蓝紫色（散射残余）
│   └── 软硬度：            中等（大气路径增加）
└── 湖面反射：
    └── 镜面高光：           金色长条反射（sun glitter）
```

---

## 5. 日出日落系统

### 5.1 Timeline 时间轴

使用 UE5 **Level Sequence** 或 **Blueprint Timeline** 驱动光照动态变化。

```
一日光照 Timeline（24h 循环）
│
├── 06:00  天色微亮     Sun Elevation: -6°    色温: 7000K    强度: 0.3
├── 06:30  ★ 日出       Sun Elevation: 0°     色温: 4500K    强度: 4.0
│         │              金色光芒 → 粉色天穹
├── 07:30  早晨         Sun Elevation: 10°    色温: 5200K    强度: 8.0
│         │              粉色 → 白色过渡
├── 08:30  上午         Sun Elevation: 20°    色温: 5500K    强度: 10.0
│
├── 12:00  正午         Sun Elevation: 60°+   色温: 5500K    强度: 12.0
│         │              最强日照，硬阴影，深蓝天穹
│
├── 15:00  下午         Sun Elevation: 40°    色温: 5500K    强度: 10.0
├── 17:00  傍晚         Sun Elevation: 20°    色温: 5200K    强度: 8.0
├── 18:00  日落前期     Sun Elevation: 10°    色温: 4500K    弧度: 6.0
│         │              开始金橙色光线
├── 19:00  ★ 黄金时刻   Sun Elevation: 5°     色温: 3800K    强度: 4.0
│         │              金橙色 → 橙色
├── 19:30  ★ 日落       Sun Elevation: 0°     色温: 3200K    强度: 2.0
│         │              白色 → 橙色 → 深红 → 紫
├── 20:00  暮光         Sun Elevation: -6°    色温: 2800K    强度: 0.5
│         │              深紫色天穹，可见星
├── 20:30  天黑         Sun Elevation: -12°   色温: N/A      强度: 0.0
│
└── 22:00 – 04:00       夜间模式
```

### 5.2 配色方案

#### 日出色彩序列

| 时间 | 天顶 | 中天 | 地平线 | 湖面反射 |
|------|------|------|--------|---------|
| 06:00 | 深蓝 | 蓝紫 | 橙红 | 暗蓝灰 |
| 06:15 | 深蓝 | 紫粉 | 金橙 | 暗橙 |
| **06:30** | 蓝紫 | **金色** | **金色** | **金色长波** |
| 06:45 | 蓝 | 粉色 | 浅金 | 暖橙 |
| 07:00 | 蓝 | 浅粉 | 白金 | 淡金 |
| 07:30 | 深蓝 | 白蓝 | 白色 | 蓝灰 |

#### 日落色彩序列

| 时间 | 天顶 | 中天 | 地平线 | 湖面反射 |
|------|------|------|--------|---------|
| 19:00 | 蓝 | 金橙 | 橙红 | 金色 |
| **19:30** | 蓝紫 | **橙** | **深红** | **红橙** |
| 19:45 | 紫蓝 | 红橙 | 深红紫 | 暗红 |
| 20:00 | 深紫 | 紫红 | 紫 | 暗紫 |
| 20:15 | 深蓝 | 暗紫 | 深蓝紫 | 黑蓝 |
| 20:30 | 黑蓝 | 深蓝 | 微光 | 黑 |

### 5.3 UE5 实现方式

```blueprint
// Blueprint: BP_SunController
//
// 输入: TimeOfDay (0.0 – 24.0)
//
// 流程:
// 1. 计算太阳方位角和仰角
// 2. 设置 DirectionalLight 的 Rotation
// 3. 从 Curve Table 读取对应色温
// 4. 从 Curve Table 读取对应强度
// 5. 更新 SkyAtmosphere 参数
// 6. 更新体积云光照
// 7. 更新湖面 Material 的反射参数
//
// 推荐: 使用 Sequencer + Curves，而非 Tick 更新
```

**关键蓝图节点：**

- `Set Actor Rotation` → 更新 Directional Light 角度
- `Get Float Value at Time` → 从 Float Curve 读取色温/强度
- `Set Temperature` / `Set Intensity` → 更新光源属性
- `Set Scalar Parameter Value` → 更新湖面材质参数

---

## 6. 星空系统

### 6.1 高海拔星空特征

| 特征 | 海平面 | 班公湖（4,242m） |
|------|--------|-------------------|
| 星等可见极限 | 4–5 等 | **6–7 等**（极佳） |
| 银河可见性 | 城市不可见，郊外模糊 | **肉眼清晰可见**，结构分明 |
| 大气扰动 | 明显（星星闪烁） | 较少（高处更稳定） |
| 光污染 | 视地点而定 | **极低**（偏远高原） |
| 星空颜色 | 偏暖白 | **冷白偏蓝** |

### 6.2 Star Map 配置（北半球夏季）

班公湖夏季（6–8 月）星空可参考以下主要星座和天体：

| 天体/星座 | 方位 | 高度（夏季夜晚） | 说明 |
|-----------|------|------------------|------|
| **银河** | 从天鹅座到射手座 | 天顶附近 | 夏季核心可见，暗云带清晰 |
| 北斗七星 | 北方 | 30–60° | 常年可见 |
| 织女星（天琴座） | 天顶附近 | 70–90° | 夏季大三角之一 |
| 天津四（天鹅座） | 天顶附近 | 60–80° | 银河所在 |
| 河鼓二（天鹰座） | 东偏南 | 50–70° | 夏季大三角之一 |
| 心宿二（天蝎座） | 南方 | 20–40° | 红色亮星 |
| 天蝎座整族 | 南方低空 | 15–35° | 夏季标志星座 |

### 6.3 UE5 Star 配置

```
UE5 Sky Atmosphere 夜间模式
│
├── SkyAtmosphere
│   ├── 夜间太阳位置：Sun Elevation < -10°
│   ├── 天空自动变暗
│   └── Star Light Intensity: 根据月相调节
│
├── USkyAtmosphere 组件
│   ├── Mie Scattering Scale: 保持日间设置
│   ├── Rayleigh Scattering Scale: 夜间视觉效果自动减弱
│   └── 启用 Night Mode（如使用 SkySphere 替代方案）
│
├── Star Map / Skybox
│   ├── 贴图分辨率：4096 × 2048（equirectangular）
│   ├── 伽马：线性
│   ├── 亮度：基于星等衰减
│   │   ├── 1等星: 1.0
│   │   ├── 3等星: 0.4
│   │   ├── 5等星: 0.1
│   │   └── 6.5等星: 0.03（高海拔可见）
│   └── 银河区域额外增强 ×1.5
│
├── 银河渲染
│   ├── 单独 Layer
│   ├── 半透明叠加
│   ├── 亮度：0.3 – 0.6（肉眼级可见度）
│   ├── 颜色：暖白 (0.95, 0.90, 0.85)
│   └── 暗云带：减法混合
│
└── 后处理
    ├── Bloom: 低（0.3）— 星星有轻微光晕
    ├── Auto Exposure:
    │   ├── Min Brightness: 0.01
    │   └── Max Brightness: 2.0
    ├── Tone Mapper:
    │   └── 支持 HDR 星空显示
    └── Vignette: 轻微（增强沉浸感）
```

### 6.4 月相影响

| 月相 | 月光强度 | 星空可见度 | 建议 |
|------|---------|-----------|------|
| 新月 | 0% | **最佳** | 银河清晰 |
| 上弦月 | 50% | 良好 | 亮星可见，银河减弱 |
| 满月 | 100% | 较差 | 仅亮星可见，无银河 |
| 下弦月 | 50% | 良好 | 与上弦类似 |

---

## 7. 雾 / 大气透视

### 7.1 高海拔能见度

班公湖区域由于海拔高、气溶胶少、湿度相对低，具有**极佳的能见度**：

| 条件 | 能见度 | UE5 雾距设置 |
|------|--------|-------------|
| 晴朗无霾 | **80–150 km** | Exponential Fog Density 极低 |
| 轻微薄雾 | 30–50 km | 标准雾距 50 km |
| 雨天 | 10–20 km | 增加雾密度 |

### 7.2 UE5 Exponential Height Fog 配置

```
ExponentialHeightFog
├── Fog Density:                0.002       （极低，模拟高海拔清洁空气）
├── Fog Height Falloff:         0.2         （缓慢衰减）
├── Fog Inscattering Color:     (0.75, 0.80, 0.90)  （淡蓝白色）
├── Fog Max Opacity:            0.3         （不会完全遮蔽远景）
├── Start Distance:             50000 m     （50 km 后开始有雾）
├── Fog Cutoff Distance:        200000 m    （200 km 截断）
│
├── Volumetric Fog:             启用（可选）
│   ├── Density:                0.001
│   ├── Albedo:                 (0.9, 0.92, 0.95)
│   └── Extinction Scale:       1.0
│
└── Directional Inscattering
    ├── Color:                  (1.0, 0.95, 0.90)   （日出日落暖色）
    └── Exponential Power:      2.0
```

### 7.3 大气透视（Atmospheric Perspective）

远山的大气透视效果（Aerial Perspective）：

```
Aerial Perspective（SkyAtmosphere 内置）
├── 启用: True
├── Start Distance: 10 km        （10 km 以上开始有明显透视）
├── Maximum Distance: 100 km     （100 km 处完全大气色）
├── Perspective Color:
│   ├── 近处（10 km）:  90% 原色 + 10% 天空色
│   ├── 中距（30 km）:  70% 原色 + 30% 天空色（淡蓝）
│   ├── 远距（60 km）:  40% 原色 + 60% 天空色（蓝灰）
│   └── 极远（100 km）: 10% 原色 + 90% 天空色（淡蓝灰）
└── 对比度衰减：随距离自然减弱
```

### 7.4 特殊天气效果

| 天气 | UE5 实现 | 参数调整 |
|------|---------|---------|
| **晴天** | 默认大气设置 | Fog Density = 0.002 |
| **薄雾晨雾** | 增加 Fog Density | 0.005 – 0.010，仅在日出前后 |
| **沙尘（罕见）** | 增加 Mie + Fog | Mie Scale = 0.008，Fog = 0.02，偏黄色 |
| **暴风雪** | 增加 Fog + 云 | Fog = 0.05，云覆盖率 90%+，降低光照强度 |

---

## 8. 材质与后处理配合

### 8.1 Post Process Volume（后处理）

```
PostProcessVolume
├── Exposure
│   ├── Min EV:                7.0        （高海拔曝光下限较高）
│   ├── Max EV:                14.0
│   └── Speed Up:              1.0
│
├── Color Grading
│   ├── White Temp:            5500 K     （与太阳色温匹配）
│   ├── Global Saturation:     1.1        （高海拔色彩更饱和）
│   ├── Global Contrast:       1.05       （略增对比度）
│   └── Shadows:               偏蓝       （模拟天光散射）
│
├── Bloom
│   ├── Intensity:             0.5        （高海拔紫外线强，Bloom 略增）
│   ├── Threshold:             1.0
│   └── Size Scale:            1.0
│
├── Auto Exposure
│   ├── Method:                Histogram
│   ├── Histogram Min:         0.8
│   └── Histogram Max:         25.0       （高海拔动态范围大）
│
└── Vignette
    └── Intensity:             0.2        （轻微暗角，增强沉浸感）
```

### 8.2 湖面水体材质

```
Water Material（Lake Surface）
├── 基础颜色:
│   ├── 晴天:     (0.15, 0.35, 0.55)    深蓝绿色
│   ├── 日落:     (0.45, 0.30, 0.20)    暖橙棕色
│   └── 夜间:     (0.05, 0.08, 0.12)    深蓝黑
├── Roughness:     0.02 – 0.10          （平滑水面）
├── Reflectivity:  0.85 – 0.95
├── 波浪:          小波纹（微风 3–5 m/s）
│   ├── Normal Map 动画速度: 0.3
│   └── 波浪高度: 0.1 – 0.3 m
└── 深度颜色:      深处更蓝，浅处更绿
```

---

## 9. 性能优化建议

| 优化项 | 方案 | 影响 |
|--------|------|------|
| Volumetric Cloud LOD | 远距离降低采样率 | 性能 ↑，远处质量微降 |
| Shadow Cascades | 3–4 级，近处精细远处粗糙 | 性能 ↑ |
| Star Map | 2K 贴图（非 4K） | 显存 ↓，星空质量可接受 |
| Exponential Fog | 禁用 Volumetric Fog（如不需要体积光） | 性能 ↑↑ |
| Ray Tracing | 可选，非必须 | 高配使用 |
| Auto Exposure | 使用固定 EV（已知光照条件） | 性能 ↑，可预测 |

---

## 10. 参数速查表

### 全天参数总览

| 参数 | 日间值 | 黄金时刻 | 夜间 |
|------|--------|---------|------|
| Sun Elevation | 10°–60° | 0°–10° | -18°–0° |
| Sun Temperature | 5500 K | 3200–4500 K | — |
| Sun Intensity | 10–12 lux | 3–6 lux | 0 lux |
| Mie Scale | 0.0025 | 0.0030 | 0.0020 |
| Rayleigh Scale | 0.0020 | 0.0022 | 0.0015 |
| Fog Density | 0.002 | 0.004 | 0.001 |
| Cloud Coverage | 20–40% | 15–30% | 20–30% |
| Exposure EV | 12–14 | 8–11 | 7–9 |
| Bloom Intensity | 0.3 | 0.8 | 0.1 |

---

## 附录

### A. 参考资源

- [UE5 Sky Atmosphere Documentation](https://docs.unrealengine.com/5.0/en-US/sky-atmosphere-component-in-unreal-engine/)
- [UE5 Volumetric Cloud Documentation](https://docs.unrealengine.com/5.0/en-US/volumetric-cloud-component-in-unreal-engine/)
- [班公湖地理数据 - Wikipedia](https://en.wikipedia.org/wiki/Pangong_Tso)
- CIE 标准大气模型（4,242m 参数）

### B. 版本历史

| 版本 | 日期 | 作者 | 说明 |
|------|------|------|------|
| 1.0 | 2026-03-18 | EV-cosmos | 初版，完成全部大气系统参数定义 |

---

> **文档状态：** ✅ 初版完成  
> **审核状态：** 待审核  
> **UE5 版本兼容：** 5.4+
