# 班公湖水体渲染 Shader — DynastyVR POC

> **项目**：DynastyVR — 班公湖 POC（UE5）
> **作者**：EV-shader（东方皇朝元宇宙部）
> **版本**：v1.0 | 2026-03-18
> **状态**：POC 核心着色器规格

---

## 目录

1. [班公湖渐变水色 Shader](#1-班公湖渐变水色-shader)
2. [清澈湖水材质](#2-清澈湖水材质)
3. [水面波纹（Gerstner Wave）](#3-水面波纹gerstner-wave)
4. [雪山倒影](#4-雪山倒影)
5. [高海拔大气散射集成](#5-高海拔大气散射集成)
6. [日落/日出变体](#6-日落日出变体)
7. [UE5 Material 节点图完整伪代码](#7-ue5-material-节点图完整伪代码)
8. [性能优化建议](#8-性能优化建议)

---

## 1. 班公湖渐变水色 Shader

### 1.1 核心原理

班公湖（Pangong Tso）东西长约 160km，是一个独特的**盐度梯度湖**：
- **东段**（印度控制区）：淡水注入，盐度低 → 折射率接近纯水（~1.33）→ 水色浅蓝通透
- **西段**（中国控制区）：内流无出口，盐度高 → 折射率升高（~1.36）→ 水色深蓝浓郁
- **中段**：盐度渐变过渡带 → 碧蓝渐变

**渲染原理**：通过 World Position X 轴坐标映射盐度梯度，驱动折射率变化和水色混合。

### 1.2 输入参数

| 参数 | 类型 | 来源 | 说明 |
|------|------|------|------|
| `WorldPosition.X` | float | Vertex/Fragment WorldPos | 决定东西位置，范围约 [−80km, 0km] |
| `WaterDepth` | float | SceneDepth − PixelDepth | 湖水深度（0–30m） |
| `LightDirection` | float3 | Scene DirectionalLight | 主光源方向 |
| `Time` | float | Material Time | 驱动波纹动画 |

### 1.3 颜色映射函数

```
// === 盐度梯度映射 ===
// 将世界坐标 X 映射到 [0, 1] 渐变值
float SalinityGradient = Remap(
    WorldPosition.X,
    FromMin = -80000.0,   // 西端（cm 单位）
    FromMax = 0.0,        // 东端
    ToMin = 0.0,
    ToMax = 1.0
);

// 平滑过渡（避免硬边）
float SmoothGrad = SmoothStep(SalinityGradient, Edge0 = 0.3, Edge1 = 0.7);

// === 三段颜色定义 ===
float3 ShallowBlue = float3(0.4, 0.7, 0.95);    // 东段：浅蓝（淡水区）
float3 Turquoise   = float3(0.2, 0.55, 0.85);   // 中段：碧蓝（过渡带）
float3 DeepBlue    = float3(0.1, 0.35, 0.65);    // 西段：深蓝（咸水区）

// === 双 Lerp 实现三段渐变 ===
float3 WaterColor;
if (SmoothGrad > 0.5) {
    // 东段→中段
    float t = (SmoothGrad - 0.5) * 2.0;
    WaterColor = Lerp(Turquoise, ShallowBlue, t);
} else {
    // 中段→西段
    float t = SmoothGrad * 2.0;
    WaterColor = Lerp(DeepBlue, Turquoise, t);
}

// === 深度对颜色的影响 ===
// 浅水偏绿，深水偏蓝
float DepthInfluence = Saturate(WaterDepth / 10.0);
float3 DepthTint = Lerp(
    float3(0.3, 0.6, 0.4),   // 浅水：偏绿
    float3(0.0, 0.0, 0.2),   // 深水：纯蓝偏移
    DepthInfluence
);
WaterColor = WaterColor + DepthTint * 0.15;
```

### 1.4 UE5 Material Graph 节点设计

```
┌─────────────────────────────────────────────────────────────────┐
│                  班公湖渐变水色 Material Graph                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [WorldPosition]──→ [ComponentMask(R)] ──→ [Divide(/10000)]    │
│         │                                      │                │
│         │                               [Clamp(-8, 0)]         │
│         │                                      │                │
│         │                               [Remap]                │
│         │                          In: [-8, 0] → [0, 1]        │
│         │                                      │                │
│         │                               [SmoothStep]            │
│         │                          Edge0=0.3  Edge1=0.7        │
│         │                                      │                │
│         │                              ┌───────┴───────┐        │
│         │                              │  Gradient     │        │
│         │                              │  (0..1)       │        │
│         │                              └───────┬───────┘        │
│         │                                      │                │
│  [Constant3: DeepBlue]    [Constant3: Turquoise]  [Constant3:  │
│   (0.1, 0.35, 0.65)       (0.2, 0.55, 0.85)      ShallowBlue] │
│         │                    │                   (0.4, 0.7, 0.95)│
│         │                    │                        │          │
│         └────────┐    ┌─────┘                         │          │
│                  ▼    ▼                               │          │
│              [Lerp(A,B)]  ← Gradient > 0.5?          │          │
│                  │         Select node                │          │
│                  │                                    │          │
│              [Lerp(A,B)]  ← Gradient < 0.5?          │          │
│                  │                                    │          │
│                  ▼                                    │          │
│            [WaterBaseColor] ◄─────────────────────────┘          │
│                  │                                               │
│                  ▼                                               │
│            [BaseColor Output]                                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**节点连接明细：**

| 序号 | 节点类型 | 输入 | 输出 | 说明 |
|------|----------|------|------|------|
| 1 | `WorldPosition` | — | XYZ | 获取像素世界坐标 |
| 2 | `ComponentMask (R)` | WorldPos.X | float | 仅取 X 分量 |
| 3 | `Divide` | X / 10000.0 | float | 转换单位（cm→十km） |
| 4 | `Clamp (-8, 0)` | Divided value | float | 限制范围 |
| 5 | `Remap (Lerp)` | Clamp → [0,1] | float | 线性映射到 0..1 |
| 6 | `SmoothStep (0.3, 0.7)` | Remapped | float | 平滑过渡 |
| 7 | `Constant3` × 3 | — | float3 | 三段预设颜色 |
| 8 | `If / Select` | Gradient > 0.5 | — | 选择 Lerp 路径 |
| 9 | `Lerp` × 2 | 相邻两色 | float3 | 三段混合输出 |

---

## 2. 清澈湖水材质

### 2.1 Fresnel 反射（IOR 1.33）

水的菲涅尔效应：**掠射角反射强，垂直视角透射强**。

```
// Schlick 近似
float CosTheta = dot(ViewDir, WorldNormal);
float3 FresnelBase = float3(0.025, 0.025, 0.03);  // 水的 F0

float3 Fresnel = FresnelBase +
    (1.0 - FresnelBase) * Pow(1.0 - CosTheta, 5.0);

// UE5: Fresnel 节点（内置）
// 或自定义：
//   ViewDir → Dot(Normal) → OneMinus → Power(5) → Multiply + Add
```

**UE5 Material Graph：**
```
[CameraVector] → [Dot(Normal)] → [OneMinus] → [Power(5)]
                                                         │
[Constant: 0.025] ─────────────────────────────────►[Add]│
                                                         │
                                                    [Multiply]
                                                         │
    [Lerp: Background vs Reflection Color] ◄────────────┘
         │
    [Specular Output]
```

### 2.2 水面透明度随视角变化

```
// 视角越倾斜，水面越不透明（反射增强，透射减弱）
float ViewAngle = dot(ViewDir, float3(0, 0, 1));  // 假设水面近水平
float Opacity = Lerp(0.3, 0.95, 1.0 - Abs(ViewAngle));

// UE5: 使用 Fresnel 控制 Opacity
FresnelR → Power(2) → Saturate → Opacity Output
```

### 2.3 水下可见度（深度衰减）

```
// 深水区看不清湖底
float DepthFade = Exp(-WaterDepth * AbsorptionCoefficient);

// AbsorptionCoefficient ≈ 0.08 for clear alpine lake
float UnderwaterVis = Exp(-WaterDepth * 0.08);

// UE5 节点：
[SceneDepth] - [PixelDepth] → [Divide(/100)] → [Exp] → [DepthFade]
                                                              │
[SceneTexture: BaseColor] → [Multiply] ◄─────────────────────┘
                               │
                          [Emissive / Opacity]
```

### 2.4 焦散效果（Caustics）

阳光穿过起伏水面，在水底/水体中形成动态光纹。

```
// 3D 焦散纹理投影（推荐使用 Tiling Caustics 法线贴图）
float2 CausticsUV1 = WorldPos.XY * 0.5 + Time * float2(0.1, 0.05);
float2 CausticsUV2 = WorldPos.XY * 0.3 - Time * float2(0.08, 0.03);

float Caustics1 = TextureSample(CausticsNormalMap, CausticsUV1).r;
float Caustics2 = TextureSample(CausticsNormalMap, CausticsUV2).r;

// 两层叠加产生干涉纹样
float CausticsPattern = (Caustics1 + Caustics2) * 0.5;
CausticsPattern = Pow(CausticsPattern, 3.0) * 8.0;  // 锐化光纹

// 深度衰减：焦散只在浅水可见
float CausticsStrength = Exp(-WaterDepth * 0.15) * 0.4;

// 最终焦散
float3 CausticsColor = CausticsPattern * CausticsStrength * SunLightColor;
```

**UE5 Material Graph：**
```
[TextureSample: CausticsMap] × 2 (不同 UV/速度)
        │
    [Add] → [Power(3)] → [Multiply × 8]
        │
    [Exp(-Depth×0.15)] → [Multiply × 0.4]
        │
    [Multiply: Caustics × SunColor]
        │
    [Add to Emissive / BaseColor]
```

---

## 3. 水面波纹（Gerstner Wave）

### 3.1 Gerstner Wave 数学模型

Gerstner 波同时产生水平和垂直位移，比正弦波更真实。

```
// 单个 Gerstner 波
// 输入：位置 P, 时间 t, 波参数 (Q, A, ω, φ, D)
//
// Q = 运动锐度 (0~1, 控制波峰尖锐度)
// A = 振幅
// ω = 角频率 = 2π / 波长
// φ = 相位速度 × 时间
// D = 传播方向 (归一化)

float3 GerstnerWave(float3 P, float t, float Q, float A, float omega,
                     float phaseSpeed, float2 D)
{
    float theta = dot(D, P.xz) * omega + t * phaseSpeed;
    float cosT = Cos(theta);
    float sinT = Sin(theta);

    float3 displacement;
    displacement.x = Q * A * D.x * cosT;
    displacement.z = Q * A * D.y * cosT;
    displacement.y = A * sinT;

    return displacement;
}
```

### 3.2 三层波叠加

| 层级 | 类型 | 振幅 (A) | 波长 | 方向 | 速度 | Q |
|------|------|----------|------|------|------|---|
| 大波 | 风浪 | 0.3m | 8–12m | NW (−0.7, 0.7) | 2.5 m/s | 0.5 |
| 中波 | 涌浪 | 0.1m | 3–5m | W (−1, 0) | 1.8 m/s | 0.3 |
| 小波 | 微风涟漪 | 0.02m | 0.5–1m | 任意多方向 | 3.0 m/s | 0.8 |

```
// === 三层 Gerstner 波叠加 ===
float3 WaveDisplacement = float3(0, 0, 0);
float3 WaveNormal = float3(0, 0, 1);  // 累积法线

// --- 大波：风浪，西北方向 ---
{
    float2 D = normalize(float2(-0.7, 0.7));    // NW
    float A = 0.3;
    float wavelength = 10.0;
    float omega = 2.0 * PI / wavelength;
    float phaseSpeed = Sqrt(9.81 / omega) * 1.2;  // 深水波色散
    float Q = 0.5;

    WaveDisplacement += GerstnerWave(WorldPos, Time, Q, A, omega,
                                     phaseSpeed, D);
}

// --- 中波：涌浪，西方 ---
{
    float2 D = normalize(float2(-1.0, 0.0));    // W
    float A = 0.1;
    float wavelength = 4.0;
    float omega = 2.0 * PI / wavelength;
    float phaseSpeed = Sqrt(9.81 / omega);
    float Q = 0.3;

    WaveDisplacement += GerstnerWave(WorldPos, Time, Q, A, omega,
                                     phaseSpeed, D);
}

// --- 小波：微风涟漪（2–3 个子方向） ---
for (int i = 0; i < 3; i++) {
    float angle = Time * 0.1 + i * 2.094;  // 120° 间隔旋转
    float2 D = normalize(float2(Cos(angle), Sin(angle)));
    float A = 0.02;
    float wavelength = 0.7;
    float omega = 2.0 * PI / wavelength;
    float phaseSpeed = Sqrt(9.81 / omega) * 0.8;
    float Q = 0.8;

    WaveDisplacement += GerstnerWave(WorldPos, Time, Q, A, omega,
                                     phaseSpeed, D);
}
```

### 3.3 法线计算

```
// 通过偏导数计算精确法线
// 对 Gerstner 波求导：
//
// ∂P/∂x = (1 - Σ Q·A·D.x²·ω·sinθ, Σ Q·A·D.x·D.y·ω·cosθ, Σ Q·A·D.x·cosθ)
// ∂P/∂z = (Σ Q·A·D.x·D.y·ω·cosθ, 1 - Σ Q·A·D.y²·ω·sinθ, Σ Q·A·D.y·cosθ)
//
// Normal = normalize(cross(∂P/∂x, ∂P/∂z))

float3 Tangent = float3(1.0, 0.0, 0.0);
float3 Binormal = float3(0.0, 0.0, 1.0);

// 对每个波累加导数贡献
for (each wave) {
    float theta = dot(D, P.xz) * omega + t * phaseSpeed;
    float sinT = Sin(theta);
    float cosT = Cos(theta);

    Tangent.x   -= Q * A * D.x * D.x * omega * sinT;
    Tangent.z   -= Q * A * D.x * D.y * omega * sinT;
    Tangent.y   += Q * A * D.x * cosT;

    Binormal.x  -= Q * A * D.x * D.y * omega * sinT;
    Binormal.z  -= Q * A * D.y * D.y * omega * sinT;
    Binormal.y   += Q * A * D.y * cosT;
}

float3 WaveNormal = normalize(cross(Tangent, Binormal));
```

### 3.4 法线贴图混合（增强细节）

```
// 叠加两张平铺法线贴图，不同速度和缩放
float2 NormalUV1 = WorldPos.XY * 0.05 + Time * float2(0.02, 0.01);
float2 NormalUV2 = WorldPos.XY * 0.08 - Time * float2(0.015, 0.025);

float3 NormalMap1 = UnpackNormal(TextureSample(NormalTex, NormalUV1));
float3 NormalMap2 = UnpackNormal(TextureSample(NormalTex, NormalUV2));

// 混合比例
float3 DetailNormal = BlendAngleCorrectedNormals(NormalMap1, NormalMap2, 0.5);

// 与 Gerstner 计算法线混合
float3 FinalNormal = BlendAngleCorrectedNormals(WaveNormal, DetailNormal, 0.3);
```

### 3.5 视距衰减（LOD）

```
// 近处使用完整 Gerstner + 法线贴图，远处简化
float DistanceToCamera = length(WorldPos - CameraPos);

// 过渡区间
float LODBlend = Saturate(
    (DistanceToCamera - 500.0) / 1500.0   // 500m 开始，2000m 完成
);

// LOD 0 (< 500m): 完整 3 层 Gerstner + 双法线贴图
// LOD 1 (500-2000m): 2 层 Gerstner + 单法线贴图
// LOD 2 (> 2000m): 1 层正弦波 + 无细节法线

float3 WaveResult = Lerp(FullWaveResult, SimplifiedWaveResult, LODBlend);
float3 NormalResult = Lerp(FullNormal, SimplifiedNormal, LODBlend);

// UE5: 使用 Distance Based Blend 节点
// [DistanceToCamera] → [SmoothStep(500, 2000)] → [Lerp 节点的 Alpha]
```

**UE5 Material Graph（波纹总览）：**
```
[WorldPosition] → [GerstnerWave × 3 layers] → [Add] → [WaveDisplacement]
      │                                                    │
      │                                            [Add to WorldPosition Offset]
      │
      ├→ [Tangent/Binormal derivation] → [Cross] → [WaveNormal]
      │                                                    │
      ├→ [NormalMap × 2] → [BlendNormals] ─────────────►[BlendNormals]
      │                                                    │
      │                                              [Normal Output]
      │
[DistanceToCamera] → [SmoothStep] → [LOD Select]
                                                    │
                                          [Lerp: Full/Simplified]
                                                    │
                                          [Final Normal + Offset]
```

---

## 4. 雪山倒影

### 4.1 混合反射策略

班公湖周围海拔 4000m+ 的雪山群是视觉核心，需要高质量倒影。

```
// === 混合方案 ===
// 近处（< 200m）：Planar Reflection（高质量）
// 中距（200-800m）：SSR + Planar 混合
// 远处（> 800m）：仅 SSR + Cubemap 兜底

float DistToCam = length(WorldPos - CameraPos);

// Planar 反射强度（近处为主）
float PlanarStrength = 1.0 - Saturate((DistToCam - 100.0) / 700.0);
PlanarStrength = Pow(PlanarStrength, 2.0);  // 平方衰减

// SSR 强度
float SSRStrength = 1.0 - Saturate((DistToCam - 500.0) / 3000.0);

// Cubemap 兜底
float CubemapStrength = 1.0 - PlanarStrength - SSRStrength;
CubemapStrength = Max(CubemapStrength, 0.0);
```

### 4.2 倒影颜色校正

```
// 倒影中的雪山受大气散射影响，需要颜色校正
float3 ReflectionColor = SampleReflection(ReflectionVector);

// 雪山在倒影中偏蓝（大气透视）
float AtmosphericBlue = Exp(-ReflectionDistance * 0.0003);
ReflectionColor = Lerp(
    ReflectionColor,
    float3(0.4, 0.5, 0.7),    // 大气蓝色
    1.0 - AtmosphericBlue
);

// 雪峰高亮增强（让白雪更突出）
float Luminance = dot(ReflectionColor, float3(0.299, 0.587, 0.114));
float SnowHighlight = Saturate((Luminance - 0.6) * 3.0);
ReflectionColor += SnowHighlight * float3(0.1, 0.1, 0.15);
```

### 4.3 倒影模糊（随风浪增大）

```
// 波浪越大，倒影越模糊
float WaveAmplitude = length(WaveDisplacement);
float ReflectionBlur = Saturate(WaveAmplitude / 0.5) * 0.8;

// SSR 配置
SSRParams.MaxRoughness = 0.6 + ReflectionBlur * 0.4;
SSRParams.StepCount = Lerp(64, 16, ReflectionBlur);   // 模糊时减少步数
SSRParams.MaxDistance = 8000.0;

// Planar Reflection 动态分辨率
// 波浪大时降低 Planar 分辨率以节省性能
float PlanarResolutionScale = Lerp(1.0, 0.5, ReflectionBlur);
```

**UE5 Material Graph：**
```
[ReflectionVector] ──┬→ [SceneTexture: SSR] → [SSR Strength ×]
                     │                              │
                     ├→ [PlanarReflection] → [Planar Strength ×]
                     │                              │
                     └→ [ReflectionCubemap] → [Cubemap Strength ×]
                                                        │
                                                   [Add / Lerp]
                                                        │
                                              [AtmosphereCorrection]
                                                        │
                                              [Color: ReflectionColor]

[WaveAmplitude] → [Saturate/0.5] → [Power(2)] → [Opacity / Blur]
```

---

## 5. 高海拔大气散射集成

班公湖海拔 ~4250m，大气层薄，呈现独特的视觉特征。

### 5.1 瑞利散射（Rayleigh Scattering）

```
// 薄大气 → 散射路径短 → 天穹更深蓝
// 瑞利散射强度 ∝ 1/λ⁴（短波蓝光散射强）

float Rayleigh opticalDepth = AtmosphereDensity * PathLength;

// 天空颜色
float3 RayleighScattering = float3(0.1, 0.25, 0.8) *
    (1.0 - Exp(-RayleighOpticalDepth * 4.0));

// 海拔修正：4250m 处大气密度约为海平面的 60%
float AltitudeFactor = 0.6;
RayleighScattering *= AltitudeFactor;

// 天顶深蓝，地平线浅蓝
float ViewZenith = dot(ViewDir, float3(0, 0, 1));
float SkyGradient = Pow(Saturate(ViewZenith), 0.5);
float3 SkyColor = Lerp(
    float3(0.3, 0.45, 0.7),    // 地平线
    float3(0.05, 0.15, 0.5),   // 天顶（极深蓝）
    SkyGradient
);
```

### 5.2 米氏散射（Mie Scattering）

```
// 米氏散射：大颗粒（气溶胶、薄云）散射，各向同性较强
// 高海拔：米氏散射弱，云层薄且边界清晰

float MiePhaseG = 0.8;  // 强前向散射
float CosAngle = dot(ViewDir, LightDir);
float MiePhase = (1.0 - MiePhaseG * MiePhaseG) /
    (4.0 * PI * Pow(1.0 + MiePhaseG * MiePhaseG - 2.0 * MiePhaseG * CosAngle, 1.5));

float3 MieScattering = SunColor * MiePhase * 0.15;  // 薄云效果弱

// 太阳光晕（Sun Disk Halo）
float SunViewAngle = acos(CosAngle);
float SunGlow = Exp(-SunViewAngle * 80.0) * 0.5;
MieScattering += SunColor * SunGlow;
```

### 5.3 大气透视（Aerial Perspective）

```
// 远处山脉偏蓝，对比度降低
float AerialDistance = SceneDepth;
float AerialDensity = 0.00015;  // 高海拔，散射密度低

float Transmittance = Exp(-AerialDistance * AerialDensity);
float3 Inscattering = SkyColor * (1.0 - Transmittance);

// 最终大气混合
float3 FinalColor = SceneColor * Transmittance + Inscattering;

// UE5 节点：
[SceneTexture: SceneColor] → [Multiply: Transmittance]
                                                          │
[SkyColor] → [OneMinus: Transmittance] → [Multiply] ──►[Add]
                                                          │
                                                   [FinalColor]
```

**UE5 集成方式：**
- 推荐使用 **Post Process Material** 实现大气散射
- 或使用 UE5 内置 **Sky Atmosphere** 组件，设置：
  - `MieScatterScale = 0.3`（弱米氏散射）
  - `RayleighScatterScale = 1.5`（增强瑞利散射）
  - `PlanetAltitude = 4250.0`（海拔设置）
  - `AtmosphereLengthScale = 0.8`（稀薄大气）

---

## 6. 日落/日出变体

### 6.1 材质参数动态切换

```
// === Material Parameter Collection (MPC) ===
// 使用 MPC_DynastyVR 持有全局时间参数

// MPC 参数：
//   TimeOfDay: [0, 24] 小时
//   SunElevation: [-18, 90] 度
//   TransitionFactor: [0, 1] 过渡混合

float TimeOfDay = MPC_TimeOfDay;
float SunElevation = MPC_SunElevation;

// === 时间段检测 ===
float IsSunrise = SmoothStep(SunElevation, 5.0, 15.0) *
                  (1.0 - SmoothStep(TimeOfDay, 4.0, 8.0));

float IsSunset = SmoothStep(SunElevation, 5.0, 15.0) *
                 SmoothStep(TimeOfDay, 16.0, 20.0);

float IsDay = SmoothStep(SunElevation, 10.0, 30.0);
```

### 6.2 日落变体（Golden Hour）

```
// 日落时湖水变为金橙色
float3 SunsetWaterColor = float3(0.85, 0.45, 0.15);   // 金橙色
float3 SunsetDeepColor  = float3(0.4, 0.2, 0.1);       // 深棕橙
float3 SunsetSpecular   = float3(1.0, 0.6, 0.2);       // 金色高光

// 水体颜色日落混合
float3 DayWaterColor = WaterColor;  // 前面计算的白天颜色
float3 SunsetBlend = Lerp(DayWaterColor, SunsetWaterColor, IsSunset * 0.7);

// 深水区日落更暗
SunsetBlend = Lerp(SunsetBlend, Lerp(SunsetDeepColor, DeepBlue, 0.5),
                   IsSunset * 0.4 * (1.0 - DepthInfluence));
```

### 6.3 日出变体（Pink Dawn）

```
// 日出时粉色渐变
float3 SunriseWaterColor = float3(0.75, 0.4, 0.55);   // 粉紫
float3 SunriseSkyRef     = float3(0.9, 0.5, 0.6);     // 粉色天空倒影

float3 SunriseBlend = Lerp(DayWaterColor, SunriseWaterColor, IsSunrise * 0.6);

// 日出倒影增强
float3 SunriseReflection = Lerp(ReflectionColor, SunriseSkyRef, IsSunrise * 0.5);
```

### 6.4 完整时间混合

```
// 最终颜色：白天为基础，叠加日出/日落
float3 FinalWaterColor = DayWaterColor;
FinalWaterColor = Lerp(FinalWaterColor, SunriseBlend, IsSunrise);
FinalWaterColor = Lerp(FinalWaterColor, SunsetBlend, IsSunset);

// 高光颜色也随时间变化
float3 FinalSpecularColor = Lerp(SunColor, SunriseSpecular, IsSunrise);
FinalSpecularColor = Lerp(FinalSpecularColor, SunsetSpecular, IsSunset);

// 焦散颜色
float3 FinalCausticsColor = Lerp(SunColor, SunsetSpecular, IsSunset);
FinalCausticsColor = Lerp(FinalCausticsColor, SunriseWaterColor * 1.5, IsSunrise);
```

**UE5 Material Graph：**
```
[MPC: TimeOfDay] ──────┐
[MPC: SunElevation] ───┤
                       ▼
              [SmoothStep × 2]
              ┌─────┴─────┐
         [IsSunrise]  [IsSunset]
              │            │
              ▼            ▼
    [SunriseColor]  [SunsetColor]
              │            │
              └──────┬─────┘
                     ▼
             [Select: Day/Sunrise/Sunset]
                     │
              [Final Water Color]
```

---

## 7. UE5 Material 节点图完整伪代码

### 7.1 主材质函数（MF_BangongWater）

```hlsl
// ============================================================
// MF_BangongWater — 班公湖水体主材质函数
// ============================================================

// === 输入 ===
float3 WorldPosition;       // 世界坐标
float3 WorldNormal;         // 世界法线
float3 ViewDirection;       // 相机方向
float  Time;                // 材质时间
float  SceneDepth;          // 场景深度
float  PixelDepth;          // 像素深度
float3 LightDirection;      // 光照方向
float3 LightColor;          // 光照颜色
float  TimeOfDay;           // MPC: 时间
float  SunElevation;        // MPC: 太阳高度

// === 输出 ===
float3 BaseColor;
float3 Normal;
float  Roughness;
float  Specular;
float  Opacity;
float3 Emissive;
float  Refraction;

// ────────────────────────────────────────
// STEP 1: 渐变水色
// ────────────────────────────────────────
float Salinity = Remap(WorldPosition.X / 10000.0, -8.0, 0.0, 0.0, 1.0);
float SmoothSalinity = SmoothStep(Salinity, 0.3, 0.7);

float3 ShallowBlue = float3(0.4, 0.7, 0.95);
float3 Turquoise   = float3(0.2, 0.55, 0.85);
float3 DeepBlue    = float3(0.1, 0.35, 0.65);

float3 WaterBaseColor = (SmoothSalinity > 0.5)
    ? Lerp(Turquoise, ShallowBlue, (SmoothSalinity - 0.5) * 2.0)
    : Lerp(DeepBlue, Turquoise, SmoothSalinity * 2.0);

// 深度影响
float WaterDepth = SceneDepth - PixelDepth;
float DepthFade = Saturate(WaterDepth / 10.0);
WaterBaseColor += Lerp(float3(0.03, 0.06, 0.04), float3(0, 0, 0.02), DepthFade);

// ────────────────────────────────────────
// STEP 2: Gerstner 波（3 层）
// ────────────────────────────────────────
float3 WaveDisp = float3(0, 0, 0);
float3 WaveTan  = float3(1, 0, 0);
float3 WaveBi   = float3(0, 0, 1);

// --- Layer 1: 风浪 (NW) ---
GerstnerWaveLayer(WaveDisp, WaveTan, WaveBi,
    WorldPosition, Time,
    Q=0.5, A=0.3, Wavelength=10.0,
    Direction=float2(-0.7, 0.7), PhaseSpeed=2.5);

// --- Layer 2: 涌浪 (W) ---
GerstnerWaveLayer(WaveDisp, WaveTan, WaveBi,
    WorldPosition, Time,
    Q=0.3, A=0.1, Wavelength=4.0,
    Direction=float2(-1.0, 0.0), PhaseSpeed=1.8);

// --- Layer 3: 涟漪 (3 子方向) ---
for (int i = 0; i < 3; i++) {
    float angle = Time * 0.1 + i * 2.094;
    GerstnerWaveLayer(WaveDisp, WaveTan, WaveBi,
        WorldPosition, Time,
        Q=0.8, A=0.02, Wavelength=0.7,
        Direction=float2(cos(angle), sin(angle)), PhaseSpeed=3.0);
}

float3 WaveNormal = normalize(cross(WaveTan, WaveBi));

// 法线贴图混合
float3 DetailNormal1 = UnpackNormal(TexNormal1.Sample(UV1 + Time * Speed1));
float3 DetailNormal2 = UnpackNormal(TexNormal2.Sample(UV2 - Time * Speed2));
float3 DetailNormal  = BlendAngleCorrectedNormals(DetailNormal1, DetailNormal2, 0.5);
float3 FinalNormal   = BlendAngleCorrectedNormals(WaveNormal, DetailNormal, 0.3);

// 视距 LOD
float DistToCam = length(WorldPosition - CameraPos);
float LODBlend = Saturate((DistToCam - 500.0) / 1500.0);

// ────────────────────────────────────────
// STEP 3: Fresnel + 透明度
// ────────────────────────────────────────
float CosView = dot(ViewDirection, FinalNormal);
float3 Fresnel = float3(0.025, 0.025, 0.03) +
    (1.0 - float3(0.025, 0.025, 0.03)) * Pow(1.0 - CosView, 5.0);

float ViewOpacity = Lerp(0.3, 0.95, 1.0 - Abs(CosView));

// 水下可见度
float UnderwaterVis = Exp(-WaterDepth * 0.08);

// ────────────────────────────────────────
// STEP 4: 焦散效果
// ────────────────────────────────────────
float2 CauUV1 = WorldPosition.xy * 0.5 + Time * float2(0.1, 0.05);
float2 CauUV2 = WorldPosition.xy * 0.3 - Time * float2(0.08, 0.03);
float Caustics = (TexCaustics.Sample(CauUV1).r + TexCaustics.Sample(CauUV2).r) * 0.5;
Caustics = Pow(Caustics, 3.0) * 8.0 * Exp(-WaterDepth * 0.15) * 0.4;

// ────────────────────────────────────────
// STEP 5: 日出/日落变体
// ────────────────────────────────────────
float IsSunrise = SmoothStep(SunElevation, 5, 15) * (1 - SmoothStep(TimeOfDay, 4, 8));
float IsSunset  = SmoothStep(SunElevation, 5, 15) * SmoothStep(TimeOfDay, 16, 20);

float3 SunriseColor = float3(0.75, 0.4, 0.55);
float3 SunsetColor  = float3(0.85, 0.45, 0.15);

float3 TimeBlendColor = WaterBaseColor;
TimeBlendColor = Lerp(TimeBlendColor, SunriseColor, IsSunrise * 0.6);
TimeBlendColor = Lerp(TimeBlendColor, SunsetColor,  IsSunset  * 0.7);

// ────────────────────────────────────────
// STEP 6: 反射
// ────────────────────────────────────────
float PlanarStrength = Pow(1.0 - Saturate((DistToCam - 100) / 700), 2.0);
float SSRStrength    = 1.0 - Saturate((DistToCam - 500) / 3000);

float3 ReflectionVector = reflect(-ViewDirection, FinalNormal);
float3 SSRColor         = SampleSSR(ReflectionVector);
float3 PlanarColor      = SamplePlanar(ReflectionVector);

float3 ReflectionResult = SSRColor * SSRStrength + PlanarColor * PlanarStrength;

// 倒影模糊（随波浪增大）
float WaveAmp = length(WaveDisp);
float ReflBlur = Saturate(WaveAmp / 0.5) * 0.8;

// ────────────────────────────────────────
// STEP 7: 最终合成
// ────────────────────────────────────────
// 混合：基础水色 + Fresnel 反射 + 焦散 + 倒影
float3 DiffuseColor = TimeBlendColor * UnderwaterVis + Caustics * LightColor;

BaseColor  = Lerp(DiffuseColor, ReflectionResult, Fresnel.r);
Normal     = FinalNormal;
Roughness  = Lerp(0.02, 0.15, WaveAmp);  // 水面光滑度随波浪变化
Specular   = 1.0;
Opacity    = ViewOpacity;
Emissive   = Caustics * LightColor * 2.0;  // 水底焦散自发光
Refraction = 1.0 / 1.33;                   // 水的 IOR
```

### 7.2 节点图速查表

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         班公湖水体 Shader 节点图速查                       │
├──────────┬──────────────────────────────────────────────────────────────┤
│  输出通道 │  节点链路                                                    │
├──────────┼──────────────────────────────────────────────────────────────┤
│          │ WorldPos.X → Remap(-80km,0km→0,1) → SmoothStep              │
│ BaseColor│     → 3段Lerp(DeepBlue→Turquoise→ShallowBlue)              │
│          │     + DepthTint → × UnderwaterVis                           │
│          │     + Caustics × SunColor                                   │
│          │     + Lerp(Diffuse, Reflection, Fresnel.r)                  │
├──────────┼──────────────────────────────────────────────────────────────┤
│  Normal  │ GerstnerWave ×3 → Tangent/Binormal → Cross → WaveNormal    │
│          │     + NormalMap ×2 → BlendNormals                           │
│          │     → BlendAngleCorrectedNormals(0.7, 0.3)                  │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Roughness│ Base: 0.02 + WaveAmplitude × 0.26                           │
│          │     → Clamp(0.02, 0.15)                                     │
├──────────┼──────────────────────────────────────────────────────────────┤
│  Opacity │ Fresnel → Power(5) → Saturate → Lerp(0.3, 0.95)            │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Emissive │ Caustics × SunColor × 2.0                                   │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Refract  │ Constant: 1/1.33 = 0.7519                                  │
├──────────┼──────────────────────────────────────────────────────────────┤
│  World   │ GerstnerWave Displacement → WorldPosition Offset            │
│  Offset  │     + LOD Blend (500m–2000m)                                │
└──────────┴──────────────────────────────────────────────────────────────┘
```

### 7.3 Material Instance 参数暴露

```
// 暴露到 Material Instance 的参数（可由蓝图/MPC 动态控制）

Scalar Parameters:
├── SalinityShift        [-0.2, 0.2]    // 盐度偏移（可动态调整渐变位置）
├── WaterTurbidity       [0.0, 1.0]     // 浊度（影响透明度和焦散强度）
├── WaveScale            [0.5, 2.0]     // 波浪整体缩放
├── WaveSpeed            [0.5, 3.0]     // 波浪速度倍率
├── CausticsIntensity    [0.0, 2.0]     // 焦散强度
├── FresnelF0            [0.01, 0.05]   // 菲涅尔基础反射率
├── DepthAbsorption      [0.01, 0.2]    // 水体吸收系数
├── ReflectionStrength   [0.0, 1.0]     // 反射强度
└── LODDistance           [500, 3000]    // LOD 过渡距离

Vector Parameters:
├── ShallowColor         float3         // 东段颜色
├── MidColor             float3         // 中段颜色
├── DeepColor            float3         // 西段颜色
├── SunriseTint          float3         // 日出色调
└── SunsetTint           float3         // 日落色调

Texture Parameters:
├── NormalMapA           float2 tiling  // 法线贴图 A
├── NormalMapB           float2 tiling  // 法线贴图 B
├── CausticsMap          float2 tiling  // 焦散纹理
└── DepthRamp            Texture2D      // 深度颜色渐变贴图（可选）
```

---

## 8. 性能优化建议

### 8.1 Shader 复杂度分析

| 特性 | ALU 指令 | Texture 采样 | 性能影响 |
|------|----------|-------------|----------|
| 渐变水色 | ~15 | 0 | 🟢 极低 |
| Gerstner 3层 | ~90 | 0 | 🟡 中等 |
| 法线贴图混合 | ~20 | 2 | 🟢 低 |
| Fresnel | ~8 | 0 | 🟢 极低 |
| 焦散 | ~25 | 2 | 🟢 低 |
| SSR 反射 | ~50-200 | 4-8 | 🔴 高 |
| Planar 反射 | ~30 | 2-4 | 🟡 中等（分辨率依赖） |
| 大气散射 | ~60 | 0 | 🟡 中等（PostProcess） |

### 8.2 优化策略

1. **LOD 系统**：距离 > 2km 时关闭焦散和精细法线贴图
2. **焦散纹理分辨率**：1024×1024 足够，不需要 4K
3. **SSR 距离限制**：最大反射距离 8km，步长根据距离自适应
4. **Planar Reflection 分辨率**：波浪大时降至 0.5x
5. **Compute Shader 替代**：Gerstner 波的顶点位移可用 Compute Shader 预计算
6. **RVT（Runtime Virtual Texture）**：远距离水面使用虚拟纹理烘焙
7. **Overdraw 控制**：避免多层透明水面重叠

### 8.3 目标帧率

| 平台 | 目标 | 水面面积占比 |
|------|------|-------------|
| PC (RTX 3070+) | 60 FPS | 全屏 30% |
| VR (Quest 3 PCVR) | 90 FPS | 全屏 20% |
| 移动端 | 30 FPS | 简化版 |

---

## 附录

### A. 参考资料

- [1] Jerry Tessendorf, "Simulating Ocean Water" — Gerstner Wave 原始论文
- [2] UE5 Water Plugin 官方文档
- [3] O'Neil, "Real-Time Rendering of Atmosphere" — 大气散射参考
- [4] 班公湖卫星影像 — 用于验证颜色渐变真实性

### B. 文件路径

```
dynasty-ev/
├── Content/
│   ├── Materials/
│   │   ├── MI_BangongWater         (主材质实例)
│   │   ├── M_BangongWater          (主材质 — 引用 MF_BangongWater)
│   │   ├── MF_BangongWater         (材质函数 — 核心逻辑)
│   │   └── M_BangongWater_Auto     (自动材质 — Lumen 兼容)
│   ├── Textures/
│   │   ├── T_Water_Normal_01       (法线贴图 A)
│   │   ├── T_Water_Normal_02       (法线贴图 B)
│   │   └── T_Caustics              (焦散纹理)
│   └── MaterialParameterCollections/
│       └── MPC_DynastyVR           (全局参数：时间/太阳)
└── Blueprints/
    └── BP_BangongWaterController   (水体控制蓝图)
```

### C. 快速启动

```
// UE5 中创建材质的最简步骤：
// 1. 新建 Material: M_BangongWater
// 2. Blending: Default Lit, Two Sided: OFF
// 3. 将 MF_BangongWater 作为 Material Function 节点拖入
// 4. 连接所有输出通道
// 5. 创建 Material Instance: MI_BangongWater
// 6. 调整暴露参数至合适值
// 7. 将材质赋予水体 Plane/Plane 物体
// 8. 确保场景中有 DirectionalLight 和 SkyLight
// 9. 创建 MPC_DynastyVR，设置 TimeOfDay 动态参数
// 10. 在 Level Blueprint 中绑定时间驱动
```

---

> **文档状态**：v1.0 完成。待 Shader 原型实现后补充截图和性能实测数据。
>
> **下一步**：
> - [ ] 创建 UE5 Material Function (MF_BangongWater)
> - [ ] 制作/采购法线贴图和焦散纹理
> - [ ] 实现 Gerstner Wave Compute Shader（可选优化）
> - [ ] 配置 Sky Atmosphere 参数
> - [ ] VR 性能测试
