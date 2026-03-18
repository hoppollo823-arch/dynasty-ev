# ⚡ DynastyVR — 性能优化技术规范

> 东方皇朝 EV 元宇宙部 · 性能工程组
> 版本：v1.0 | 2026-03-18

---

## 一、性能目标

### 1.1 帧率目标矩阵

| 平台 | 目标帧率 | 最低帧率 | 备注 |
|------|----------|----------|------|
| PC VR（高配） | 120 fps | 90 fps | RTX 4080+ / Quest Pro Link |
| PC VR（标准） | 90 fps | 72 fps | RTX 3060+ / Quest 2 Link |
| Standalone VR（Quest 3） | 90 fps | 72 fps | 原生渲染，Mobile SRP |
| Standalone VR（Quest 2） | 72 fps | 60 fps | 降级模式，自动降质 |
| 移动端（手机/平板） | 30 fps | 24 fps | 降级模式，简化渲染 |

### 1.2 帧时间预算分解（90fps 目标 ≈ 11.1ms/frame）

```
┌─────────────────────────────────────────────────────────┐
│  帧时间预算：11.1ms (90fps)                             │
├──────────────────────────────┬──────────────────────────┤
│  CPU Game Logic              │  2.0ms                   │
│  CPU Render Submit           │  1.5ms                   │
│  GPU Vertex / Geometry       │  2.0ms                   │
│  GPU Lighting / Shadows      │  2.5ms                   │
│  GPU Post-Processing         │  1.5ms                   │
│  GPU Overhead / Present      │  1.6ms                   │
├──────────────────────────────┼──────────────────────────┤
│  Total                       │  11.1ms                  │
└──────────────────────────────┴──────────────────────────┘
```

### 1.3 其他性能指标

| 指标 | 目标 |
|------|------|
| 首屏加载时间 | < 5s（初始地球可见） |
| 场景切换 | < 2s（城市间飞行） |
| 内存峰值（PC VR） | ≤ 8 GB |
| 内存峰值（Quest 2） | ≤ 2.8 GB（含系统） |
| 显存峰值（PC VR） | ≤ 6 GB |
| 网络带宽（城市级加载） | ≤ 50 Mbps 峰值 |
| 载具物理步长 | 60 Hz（固定时间步） |

---

## 二、地形流式加载优化

### 2.1 LOD 层级设计（10 级）

DynastyVR 使用 **Cesium 3D Tiles + 自定义 LOD 系统**，实现从太空到街景的无缝缩放。

| LOD | 覆盖范围 | 地形精度 | 影像分辨率 | 数据源 | 典型用例 |
|-----|----------|----------|------------|--------|----------|
| L0 | 全球 | ~10 km/px | 1 km/px | ETOPO1 | 太空视角 |
| L1 | 大陆 | ~5 km/px | 500 m/px | SRTM | 洲际飞行 |
| L2 | 区域 | ~1 km/px | 100 m/px | Copernicus | 省际导航 |
| L3 | 城市圈 | ~200 m/px | 30 m/px | Sentinel-2 | 城市接近 |
| L4 | 城市 | ~50 m/px | 10 m/px | Aerial | 城市概览 |
| L5 | 街区 | ~10 m/px | 2 m/px | 高精度航拍 | 街区飞行 |
| L6 | 建筑 | ~5 m/px | 0.5 m/px | 倾斜摄影 | 建筑近景 |
| L7 | 精细 | ~2 m/px | 0.2 m/px | LiDAR | 近距离观察 |
| L8 | 高精 | ~1 m/px | 0.1 m/px | 精修模型 | 门面细节 |
| L9 | 街景 | < 1 m/px | 0.05 m/px | UGC/手工 | 人眼视角 |

#### LOD 切换策略

```csharp
// 基于相机高度的 LOD 索引计算
int CalculateLOD(float cameraAltitude)
{
    // 对数插值：高度越高，LOD越粗
    // LOD 9 (街景): 0-20m
    // LOD 0 (全球): > 2,000,000m
    float t = Mathf.Log10(Mathf.Max(cameraAltitude, 1f));
    int lod = Mathf.Clamp(9 - Mathf.FloorToInt(t * 2.5f), 0, 9);
    return lod;
}

// 基于屏幕空间误差 (SSE) 的动态调整
float CalculateSSE(Vector3 worldPos, float pixelSize, float distance)
{
    // Screen Space Error: 几何误差在屏幕上的像素投影
    float projectedError = (pixelSize / distance) * screenHeight;
    return projectedError;
}
```

#### LOD 混合过渡

- **Dithering 混合**：高 LOD → 低 LOD 时使用屏幕空间抖动过渡（避免 popping）
- **距离滞回区间**：加载距离 > 卸载距离 × 1.3（防止边界震荡）
- **异步 LOD 切换**：新 LOD 级别在后台加载完成后 blend-in

### 2.2 视锥裁剪 + 距离衰减

#### 视锥体裁剪（Frustum Culling）

```
相机视锥体
    │
    ├── 预裁剪：BVH 空间树（每帧 O(log N)）
    ├── 精细裁剪：Mesh 级别精确判断
    └── 分层裁剪：Tile → Node → Mesh 三级漏斗
```

**实现方案：**
- 使用 **Hierarchical Z-Buffer (Hi-Z)** 进行 GPU 端快速裁剪
- CPU 端使用 **AABB Tree** 对 Tile 进行预筛选
- 视锥体扩展 15% 余量，防止快速转向时出现空白

#### 距离衰减（Distance-Based Culling）

```csharp
// 基于距离和 LOD 的综合衰减
float CalculateRenderWeight(Tile tile, Camera cam)
{
    float distance = Vector3.Distance(tile.center, cam.position);
    float screenArea = tile.screenProjectedArea;
    
    // 距离衰减因子：越远权重越低
    float distanceFade = 1.0f / (1.0f + distance * distanceFadeFactor);
    
    // 屏幕空间面积：占屏幕面积比例
    float screenWeight = screenArea / (screenWidth * screenHeight);
    
    return distanceFade * screenWeight;
}

// 权重低于阈值 → 进入卸载队列
// 权重低于 2x 阈值 → 降级到低 LOD
// 权重高于阈值 → 保持/升级 LOD
const float CULL_THRESHOLD = 0.001f;    // 屏幕占比 0.1%
const float DOWNGRADE_THRESHOLD = 0.002f;
```

### 2.3 异步加载队列

#### 优先级队列设计

```
┌──────────────────────────────────────────────────────────┐
│                异步加载调度器                             │
│                                                          │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐   │
│  │ P0:紧急  │  │ P1:高   │  │ P2:中   │  │ P3:低   │   │
│  │ 视锥内   │  │ 视锥边缘 │  │ 远处LOD │  │ 预取   │   │
│  │ 低LOD→高 │  │ 可见Tile │  │ 缓存Tile│  │ 后台   │   │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘   │
│                                                          │
│  每帧执行预算：                                            │
│  - P0: 不限（必须完成，否则掉帧）                          │
│  - P1: ≤ 4 tiles/帧                                      │
│  - P2: ≤ 2 tiles/帧                                      │
│  - P3: ≤ 1 tile/帧（空闲时）                              │
└──────────────────────────────────────────────────────────┘
```

#### 加载管线

```
请求 Tile
    │
    ▼
[1] 内存缓存检查 → 命中 → 直接返回
    │ 未命中
    ▼
[2] 磁盘缓存检查 → 命中 → 解压 → 返回
    │ 未命中
    ▼
[3] 网络请求 (HTTP/2 多路复用)
    │
    ▼
[4] 解压 (Draco/KTX2) → 写入磁盘缓存 → 返回
    │
    ▼
[5] GPU 上传 (异步 Compute Shader)
    │
    ▼
[6] LOD 切换 + Dithering Blend-in
```

#### 三级缓存策略

| 层级 | 类型 | 容量 | 命中率目标 | 替换策略 |
|------|------|------|------------|----------|
| L1 | GPU VRAM Cache | 512 MB | > 80% | LRU + 优先级 |
| L2 | CPU RAM Pool | 2 GB | > 95% | LRU |
| L3 | Disk Cache | 20 GB | > 99% | LFU |

### 2.4 内存预算管理

#### 分平台内存预算

```
┌──────────────────────────────────────────────────────┐
│  PC VR (8 GB RAM 预算)                               │
├──────────────────────────────┬───────────────────────┤
│  地形几何 + 纹理             │  3.0 GB               │
│  建筑模型 + 材质             │  1.5 GB               │
│  植被 + 装饰                │  1.0 GB               │
│  渲染资源 (RT, Shadows)     │  1.0 GB               │
│  系统 + UI + 音频           │  0.5 GB               │
│  缓冲区/预留                │  1.0 GB               │
├──────────────────────────────┼───────────────────────┤
│  Total                      │  8.0 GB               │
└──────────────────────────────┴───────────────────────┘

┌──────────────────────────────────────────────────────┐
│  Quest 2 (2.8 GB 限制，可用 ~1.8 GB)                 │
├──────────────────────────────┬───────────────────────┤
│  地形几何 + 纹理             │  600 MB               │
│  建筑模型 + 材质             │  300 MB               │
│  植被 + 装饰                │  200 MB               │
│  渲染资源                    │  200 MB               │
│  系统 + UI + 音频           │  300 MB               │
│  缓冲区/预留                │  200 MB               │
├──────────────────────────────┼───────────────────────┤
│  Total                      │  1.8 GB               │
└──────────────────────────────┴───────────────────────┘
```

#### 内存压力响应策略

```csharp
enum MemoryPressureLevel
{
    Normal,     // > 80% 可用 → 正常加载
    Caution,    // 60-80% 可用 → 停止预取
    Warning,    // 40-60% 可用 → 卸载远处 LOD，降低纹理质量
    Critical    // < 40% 可用 → 强制卸载所有非视锥内资源
}

void OnMemoryPressure(MemoryPressureLevel level)
{
    switch (level)
    {
        case MemoryPressureLevel.Caution:
            streamingScheduler.StopPreloading();
            break;
        case MemoryPressureLevel.Warning:
            streamingScheduler.ForceUnloadFarTiles(2.0f); // 卸载 2x 距离外
            QualitySettings.masterTextureLimit = 1; // 半分辨率纹理
            break;
        case MemoryPressureLevel.Critical:
            streamingScheduler.ForceUnloadFarTiles(1.0f);
            DisableShadows();
            QualitySettings.masterTextureLimit = 2; // 1/4 分辨率纹理
            break;
    }
}
```

---

## 三、渲染优化

### 3.1 GPU Instancing 策略

#### 应用场景

| 对象类型 | Instancing 方式 | 典型实例数 | 优化效果 |
|----------|----------------|------------|----------|
| 植被（树木/草地） | GPU Instancing | 50,000+ | Draw Call -99% |
| 建筑窗户 | GPU Instancing | 10,000+ | Draw Call -95% |
| 车辆（交通流） | GPU Instancing + Animation | 500-2,000 | Draw Call -90% |
| 路面标线 | GPU Instancing | 5,000+ | Draw Call -80% |
| 粒子（雨/雪） | Instanced Indirect | 100,000+ | GPU 亲和性优化 |

#### 实现要点

```hlsl
// GPU Instancing Vertex Shader
// 支持：位置/旋转/缩放每实例不同
struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    uint instanceID : SV_InstanceID;
};

struct v2f
{
    float4 pos : SV_POSITION;
    float3 worldNormal : TEXCOORD0;
    float4 color : TEXCOORD1;
};

// Instance Data Buffer (StructuredBuffer)
StructuredBuffer<InstanceData> _InstanceBuffer;

v2f vert(appdata v)
{
    InstanceData data = _InstanceBuffer[v.instanceID];
    
    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    worldPos = worldPos * data.scale + data.position;
    
    v2f o;
    o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
    o.worldNormal = mul(data.rotation, v.normal);
    o.color = data.color;
    return o;
}
```

#### 距离场 LOD (GPU Instancing LOD)

```
近距离 → 高模 (300 tris/instance)
中距离 → 中模 (80 tris/instance)
远距离 → 低模 (12 tris/instance / BillBoard)
```

### 3.2 Occlusion Culling 方案

#### 多层级遮挡剔除

```
┌──────────────────────────────────────────────────────┐
│  Level 0: Hi-Z Buffer Occlusion (GPU)               │
│  - 每帧生成 Hi-Z 深度金字塔                          │
│  - CPU 端使用 GPU Compute Shader 批量裁剪            │
│  - 适合：建筑物、大型地形特征                        │
│  - 开销：~0.3ms/frame (Compute)                     │
├──────────────────────────────────────────────────────┤
│  Level 1: Software Occlusion (CPU)                   │
│  - 使用 SIMD 优化的软件遮挡器                        │
│  - 针对小物件（路灯、长椅等）                       │
│  - 与 BVH 结合做层次化裁剪                          │
│  - 开销：~0.5ms/frame                               │
├──────────────────────────────────────────────────────┤
│  Level 2: Portal / Sector Culling (Engine)           │
│  - 室内场景使用 Portal 系统                          │
│  - 城市街区使用 Sector 分区                          │
│  - 最粗粒度，大幅减少进入精细裁剪的对象数            │
│  - 开销：~0.1ms/frame                               │
└──────────────────────────────────────────────────────┘
```

#### Hi-Z Occlusion 实现

```csharp
// Hi-Z Buffer Pipeline
// 1. 渲染深度 → Hi-Z 金字塔 (Mip Chain)
// 2. 对每个候选对象的包围盒进行保守投影
// 3. 在 Hi-Z 中查询 → 被遮挡则剔除

class HiZOcclusionCuller
{
    ComputeShader hiZBuildShader;    // 构建 Hi-Z 金字塔
    ComputeShader occlusionShader;   // 批量遮挡查询
    
    RenderTexture depthTexture;      // 场景深度
    RenderTexture hiZPyramid[];      // Hi-Z mip chain (12 levels)
    
    // 每帧
    void Execute(FrameData frame)
    {
        // Pass 1: 复制深度到 Hi-Z
        Graphics.Blit(frame.depthBuffer, hiZPyramid[0]);
        
        // Pass 2: 逐级下采样构建金字塔
        for (int i = 1; i < 12; i++)
            hiZBuildShader.Dispatch(hiZPyramid[i-1], hiZPyramid[i]);
        
        // Pass 3: 批量遮挡查询
        occlusionShader.SetTexture(0, "_HiZPyramid", hiZPyramid);
        occlusionShader.SetBuffer(0, "_AABBs", candidateBounds);
        occlusionShader.Dispatch(candidateCount / 64);
    }
}
```

#### 遮挡剔除效果预估

| 场景 | 总对象数 | 视锥裁剪后 | 遮挡剔除后 | 剔除率 |
|------|----------|------------|------------|--------|
| 曼哈顿街景 | 500,000 | 45,000 | 12,000 | 97.6% |
| 自然风景 | 200,000 | 80,000 | 55,000 | 72.5% |
| 太空视角 | 10,000 | 10,000 | 8,000 | 20.0% |

### 3.3 Virtual Texturing

#### 设计概述

Virtual Texturing (VT) 将海量纹理虚拟化为一个巨大的虚拟纹理空间，按需加载纹素（texel）到 GPU 显存。

```
┌──────────────────────────────────────────────────────┐
│              Virtual Texture 布局                     │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  虚拟地址空间 (16K × 16K / Tile)              │  │
│  │                                                │  │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐        │  │
│  │  │Tile  │ │Tile  │ │Tile  │ │Tile  │ ...     │  │
│  │  │(0,0) │ │(1,0) │ │(2,0) │ │(3,0) │        │  │
│  │  └──────┘ └──────┘ └──────┘ └──────┘        │  │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐        │  │
│  │  │Tile  │ │Tile  │ │Tile  │ │Tile  │ ...     │  │
│  │  │(0,1) │ │(1,1) │ │(2,1) │ │(3,1) │        │  │
│  │  └──────┘ └──────┘ └──────┘ └──────┘        │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  物理缓存 (256 MB / Page Table)                      │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐    │
│  │ P12  │ │ P37  │ │ P89  │ │ P102 │ │ ...  │    │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘    │
└──────────────────────────────────────────────────────┘
```

#### VT 配置

| 参数 | PC VR | Quest |
|------|-------|-------|
| 虚拟纹理尺寸 | 16K × 16K | 8K × 8K |
| Tile 尺寸 | 128 × 128 px | 64 × 64 px |
| 物理缓存大小 | 256 MB | 64 MB |
| Page Table 大小 | 16 MB | 4 MB |
| 帧内最大加载 Tile | 16 | 4 |
| Mip 偏差 | 0.0 (高配) / +1.0 (低配) | +2.0 |

#### 实现流程

```csharp
// 每帧 VT 更新
void UpdateVirtualTexture(Camera camera)
{
    // 1. 从屏幕空间生成反馈 (Feedback Pass)
    //    → 记录每个像素访问的 VT 坐标和所需 Mip Level
    ComputeBuffer feedbackBuffer = GenerateFeedback(camera);
    
    // 2. 分析反馈 → 统计需要加载的 Page
    var pagesNeeded = AnalyzeFeedback(feedbackBuffer);
    
    // 3. 优先级排序（按屏幕覆盖率）
    pagesNeeded.SortByPriority();
    
    // 4. 限制每帧加载量
    var pagesToLoad = pagesNeeded.Take(MaxPagesPerFrame);
    
    // 5. 从磁盘/网络加载 Page 数据
    foreach (var page in pagesToLoad)
    {
        if (!physicalCache.Contains(page))
        {
            var data = LoadPageFromStreaming(page);
            physicalCache.EvictAndInsert(page, data);
        }
    }
    
    // 6. 更新 Page Table → GPU Uniform Buffer
    UpdatePageTableGPU(physicalCache);
}
```

### 3.4 DLSS / FSR 集成

#### 技术方案对比

| 特性 | DLSS 3.5 (NVIDIA) | FSR 3 (AMD) | XeSS (Intel) |
|------|-------------------|-------------|--------------|
| 超分辨率 | ✅ 2.0 AI | ✅ 3.0 Spatial | ✅ 2.0 ML |
| 帧生成 | ✅ Frame Gen | ✅ Frame Gen | ❌ |
| 光线重建 | ✅ Ray Reconstruction | ❌ | ❌ |
| 最低 GPU | RTX 20+ | 任意 (GPU) | Xe / Arc / DP4a |
| VR 支持 | ✅ VR-ready | ⚠️ 需测试 | ⚠️ 需测试 |
| 推荐使用 | NVIDIA 高配 | 全平台回退 | Intel 平台 |

#### VR 专用注意事项

```
⚠️ VR 特殊约束：
├── 帧生成可能导致运动 sickness（需要保守使用）
├── 需要保证双眼渲染的一致性
├── 延迟增加 < 2ms
└── 眼动追踪辅助的注视点渲染 (Foveated Rendering) 联动

推荐策略：
├── PC VR (RTX 40+): DLSS Quality Mode + Frame Gen (可选)
├── PC VR (RTX 20-30): DLSS Balanced Mode
├── AMD GPU: FSR 3 Quality Mode
└── Quest (内置): 避免使用，依赖内置超分
```

#### 集成管线

```
┌──────────────────────────────────────────────────────────┐
│  渲染管线 (Native Resolution)                            │
│                                                          │
│  Scene Render → TAA/Jitter → DLSS/FSR Input             │
│       │                          │                       │
│       ▼                          ▼                       │
│  Color Buffer          Motion Vectors Buffer             │
│  Depth Buffer          Exposure Buffer                   │
│                                                          │
│  ┌─────────────────────────────────────────────┐        │
│  │  DLSS/FSR Upscale                          │        │
│  │  Input: 75% resolution → Output: 100%       │        │
│  │  运动矢量 + 深度 → 抗鬼影                   │        │
│  └─────────────────────────────────────────────┘        │
│                                                          │
│  ┌─────────────────────────────────────────────┐        │
│  │  Foveated Rendering (眼动追踪联动)          │        │
│  │  中心 10%: 1.0x 分辨率                     │        │
│  │  中间环: 0.75x 分辨率                      │        │
│  │  外围 50%: 0.5x 分辨率                     │        │
│  └─────────────────────────────────────────────┘        │
└──────────────────────────────────────────────────────────┘
```

#### 质量预设

| 预设 | 渲染比例 | 适用场景 | 预期帧率提升 |
|------|----------|----------|-------------|
| Ultra Quality | 77% | 高配 PC | +30% |
| Quality | 67% | 中配 PC | +50% |
| Balanced | 58% | 中低配 | +70% |
| Performance | 50% | 低配 / Quest Link | +100% |

---

## 四、网络优化

### 4.1 数据压缩协议

#### 协议层级

```
┌──────────────────────────────────────────────────────────┐
│  DynastyVR Network Protocol Stack                        │
│                                                          │
│  Layer 4: Game Protocol (Binary, Custom)                 │
│  ├─ Player State, World Events, Chat                     │
│  ├─ Variable-length encoding (varint)                    │
│  └─ Schema-based serialization                           │
│                                                          │
│  Layer 3: Delta Compression                               │
│  ├─ Only send changed fields                             │
│  ├─ XOR delta for float arrays (positions)               │
│  └─ Bitmask for boolean flags                            │
│                                                          │
│  Layer 2: LZ4 / Zstd Compression                         │
│  ├─ LZ4: fast decompress, lower ratio (player data)     │
│  └─ Zstd: better ratio (asset metadata, terrain data)   │
│                                                          │
│  Layer 1: QUIC / WebRTC / WebSocket                      │
│  ├─ QUIC: primary transport (low latency, multiplexed)  │
│  ├─ WebRTC: voice / P2P data                            │
│  └─ WebSocket: fallback                                  │
└──────────────────────────────────────────────────────────┘
```

#### 压缩效果预估

| 数据类型 | 未压缩 | 压缩后 | 压缩率 |
|----------|--------|--------|--------|
| 玩家位置 (60Hz) | 1.4 KB/s | 0.3 KB/s | 79% ↓ |
| 车辆状态 (30Hz) | 4.8 KB/s | 0.8 KB/s | 83% ↓ |
| 语音 (Opus 24kbps) | 24 kbps | 6 kbps | 75% ↓ (硬件编码) |
| 地形 Tile (1MB) | 1 MB | 0.2 MB | 80% ↓ |
| 建筑元数据 | 50 KB | 8 KB | 84% ↓ |

#### 消息格式示例

```protobuf
// 玩家状态更新 (delta encoded)
message PlayerStateUpdate {
  uint32 player_id = 1;          // varint, 1-4 bytes
  uint32 seq = 2;                // 序列号，用于乱序检测
  
  // 位置: XOR delta from last confirmed state
  sint32 pos_x_delta = 3;        // 偏移量 * 1000 (mm精度)
  sint32 pos_y_delta = 4;
  sint32 pos_z_delta = 5;
  
  // 旋转: 最小角度表示
  sint32 rot_y = 6;              // 偏航角 * 100
  sint32 rot_p = 7;              // 俯仰角 * 100
  
  // 动画状态 (bitmask)
  uint32 anim_flags = 8;
  
  // 速度/方向 (仅变化时发送)
  optional sint32 vel_x = 9;     // 可选字段
  optional sint32 vel_y = 10;
  optional sint32 vel_z = 11;
}
```

### 4.2 优先级队列（近处优先）

#### 优先级计算

```csharp
enum PacketPriority
{
    Critical = 0,   // 立即发送，不可丢弃
    High = 1,       // 本帧发送
    Medium = 2,     // 下1-2帧
    Low = 3,        // 空闲时发送
    Background = 4  // 可合并、可丢弃
}

// 基于距离和速度的优先级计算
PacketPriority CalculatePriority(GameObject obj, Camera localCamera)
{
    float distance = Vector3.Distance(obj.position, localCamera.position);
    float approachSpeed = Vector3.Dot(
        obj.velocity, 
        (localCamera.position - obj.position).normalized
    );
    
    // 距离越近 + 迎面而来 → 优先级越高
    float urgency = 0;
    
    if (distance < 10f) urgency = 100f;           // 10m 内: Critical
    else if (distance < 50f) urgency = 80f;       // 50m: High
    else if (distance < 200f) urgency = 50f;      // 200m: Medium
    else urgency = 20f / (distance / 200f);       // 更远: Low
    
    // 接近速度增加优先级
    urgency += Mathf.Max(0, approachSpeed) * 10f;
    
    return MapUrgencyToPriority(urgency);
}
```

#### 优先级队列结构

```
发送缓冲区 (每帧更新)
┌─────────────────────────────────────────────────────┐
│  Priority Queue                                     │
│                                                     │
│  [Critical] Player tracking data ────────────→ 0ms │
│  [High]     Nearby players (50m) ────────────→ 1ms │
│  [Medium]   Visible players (200m) ──────────→ 2ms │
│  [Low]      Background players ─────────────→ 5ms  │
│  [Bg]       Asset preloading info ──────────→ idle │
│                                                     │
│  帧预算：≤ 2ms 用于网络序列化/发送                    │
└─────────────────────────────────────────────────────┘
```

#### 带宽分配策略

```
总带宽：10 Mbps (典型移动网络)

┌─────────────────────────────────────────────┐
│  语音 (WebRTC Opus)         │  128 kbps     │
│  玩家状态更新 (Critical)    │  64 kbps      │
│  玩家状态更新 (High)        │  128 kbps     │
│  玩家状态更新 (Medium)      │  256 kbps     │
│  玩家状态更新 (Low/BG)      │  128 kbps     │
│  地形/资产流式加载          │  ~9.2 Mbps    │
└─────────────────────────────────────────────┘
```

### 4.3 预测与补偿

#### 客户端预测（Client-Side Prediction）

```csharp
// 本地玩家移动预测
class ClientPrediction
{
    Queue<InputCommand> pendingInputs;
    TransformState lastServerState;
    
    void OnLocalPlayerMove(Input input, float deltaTime)
    {
        // 1. 立即应用预测位置
        var predictedState = ApplyMovement(localPlayer.state, input, deltaTime);
        localPlayer.transform.position = predictedState.position;
        
        // 2. 保存命令待确认
        pendingInputs.Enqueue(new InputCommand
        {
            input = input,
            timestamp = GetNetworkTime(),
            sequenceId = nextSeq++
        });
        
        // 3. 发送到服务器
        SendToServer(input, sequenceId);
    }
    
    void OnServerReconcile(ServerState serverState)
    {
        lastServerState = serverState;
        
        // 移除已确认的输入
        while (pendingInputs.Peek().sequenceId <= serverState.lastAckedSeq)
            pendingInputs.Dequeue();
        
        // 从服务器状态重放未确认输入
        var correctedState = serverState.playerState;
        foreach (var cmd in pendingInputs)
        {
            correctedState = ApplyMovement(correctedState, cmd.input, dt);
        }
        
        // 平滑纠正到纠正后的位置（避免 popping）
        localPlayer.SnapCorrection(correctedState.position, correctionDistance: 0.5f);
    }
}
```

#### 他人位置插值（Remote Player Interpolation）

```csharp
// 远程玩家：接收服务器状态 → 插值显示
class RemotePlayerInterpolation
{
    const float INTERPOLATION_DELAY = 0.1f; // 100ms 延迟插值
    
    Queue<StateSnapshot> stateBuffer;
    
    void Update()
    {
        float renderTime = GetNetworkTime() - INTERPOLATION_DELAY;
        
        // 找到 renderTime 前后的两个快照
        var prev = stateBuffer.Last(s => s.timestamp <= renderTime);
        var next = stateBuffer.First(s => s.timestamp >= renderTime);
        
        float t = (renderTime - prev.timestamp) / (next.timestamp - prev.timestamp);
        
        // Hermite 插值（平滑曲线）
        transform.position = HermiteInterpolate(
            prev.position, prev.velocity,
            next.position, next.velocity,
            t
        );
        transform.rotation = Slerp(prev.rotation, next.rotation, t);
    }
}
}

// 位置外推（应对丢包）
// 如果超时未收到更新 → 根据最后速度外推
// 外推上限：200ms，超过则冻结位置
```

#### 高延迟补偿

```
┌──────────────────────────────────────────────────────────┐
│  延迟补偿策略矩阵                                        │
│                                                          │
│  延迟 < 50ms:  正常模式，标准插值 (100ms)               │
│  延迟 50-150ms: 增加插值缓冲 (200ms)，客户端外推        │
│  延迟 150-300ms: 大缓冲 (300ms)，降低位置更新频率       │
│  延迟 > 300ms: 显示延迟警告，暂停位置外推               │
│                                                          │
│  自适应网络更新频率:                                     │
│  ┌────────────┬──────────────────────────────────┐      │
│  │ 距离       │ 更新频率                          │      │
│  ├────────────┼──────────────────────────────────┤      │
│  │ < 20m      │ 30 Hz (全速)                     │      │
│  │ 20-50m     │ 15 Hz                            │      │
│  │ 50-200m    │ 5 Hz                             │      │
│  │ > 200m     │ 1 Hz (仅位置)                    │      │
│  │ > 500m     │ 0.25 Hz (极低频)                 │      │
│  └────────────┴──────────────────────────────────┘      │
└──────────────────────────────────────────────────────────┘
```

---

## 五、目标硬件与最低配置

### 5.1 硬件档位划分

#### Tier 1: Standalone VR (Quest 2/3)

| 配置项 | Quest 2 (最低) | Quest 3 (推荐) |
|--------|----------------|----------------|
| SoC | Snapdragon XR2 Gen 1 | Snapdragon XR2 Gen 2 |
| RAM | 6 GB | 8 GB |
| 可用 RAM | ~2.8 GB | ~5.0 GB |
| 分辨率 | 1832×1920/眼 | 2064×2208/眼 |
| 刷新率 | 72/90 Hz | 90/120 Hz |
| 渲染分辨率 | 0.7x (1282×1344) | 1.0x (原生) |
| 纹理质量 | 低 (512px max) | 中 (1024px max) |
| 阴影 | 烘焙 + 简单实时 | PCSS 软阴影 |
| 后处理 | Bloom + Tonemapping | Bloom + Tonemapping + AO |
| 植被密度 | 30% | 60% |
| 地形 LOD | L5 最高 | L7 最高 |
| AI 人数 | 10-20 | 30-50 |

**Quest 2 降级策略：**
- 启用 Fixed Foveated Rendering (FFR) Level 2
- 纹理分辨率减半
- 禁用实时阴影，改用烘焙 + 简单投影
- 降低 LOD 切换距离
- 压缩纹理格式 (ETC2/ASTC)

#### Tier 2: PC VR (标准)

| 配置项 | 最低配置 | 推荐配置 |
|--------|----------|----------|
| GPU | RTX 3060 (8GB) | RTX 4070 (12GB) |
| CPU | i5-12400 / R5 5600X | i7-13700K / R7 7700X |
| RAM | 16 GB | 32 GB |
| VRAM | 8 GB | 12 GB |
| 存储 | SSD 512 GB | NVMe SSD 1 TB |
| 头显 | Quest 2 Link | Quest 3 / Valve Index |
| 渲染分辨率 | 1.0x (头显原生) | 1.2x (超采样) |
| 纹理质量 | 高 | 极高 |
| 阴影 | Contact Shadows + PCSS | Ray Traced Shadows |
| 反射 | SSR | Ray Traced Reflections |
| DLSS/FSR | Quality Mode | Ultra Quality |
| 地形 LOD | L8 最高 | L9 最高 |
| AI 人数 | 50-100 | 200+ |

#### Tier 3: 高配 PC (旗舰)

| 配置项 | 配置 |
|--------|------|
| GPU | RTX 4080 (16GB) / RTX 4090 (24GB) |
| CPU | i9-14900K / R9 7950X |
| RAM | 32-64 GB |
| VRAM | 16-24 GB |
| 存储 | NVMe SSD 2 TB |
| 头显 | Quest 3 / Bigscreen Beyond / Pimax |
| 渲染分辨率 | 1.5x (超级采样) |
| 纹理质量 | 极高 + Virtual Texturing |
| 光追 | 全光追 (RTXDI 多光源) |
| DLSS | Performance Mode (仍保持 >120fps) |
| 地形 LOD | L9 最高 |
| AI 人数 | 500+ |
| 特殊效果 | 全体积雾 / 光线重建 / 路径追踪 |

#### Tier 4: 移动端降级模式 (手机/平板)

| 配置项 | 最低配置 | 推荐配置 |
|--------|----------|----------|
| GPU | Adreno 650 / Mali-G78 | Adreno 740 / Mali-G720 |
| RAM | 6 GB | 12 GB |
| 存储 | 4 GB 可用 | 8 GB 可用 |
| 渲染分辨率 | 720p | 1080p |
| 目标帧率 | 30 fps (24 fps 最低) | 30 fps |
| 地形 LOD | L5 最高 | L7 最高 |
| 纹理 | 512px + ASTC | 1024px + ASTC |
| 光照 | 预计算 GI + 1 盏实时光 | 预计算 GI + 3 盏实时光 |
| 阴影 | 烘焙 | 烘焙 + 简单实时 |
| 后处理 | Tonemapping | Bloom + Tonemapping |
| 云层 | Billboard 云 | 程序化体积云（低采样） |
| 海洋 | 静态反射 | 简单波浪 + FFT |
| 植被 | BillBoard | 简单 Mesh |

### 5.2 自动质量调节系统

```csharp
// 运行时动态质量调节
class AutoQualitySystem
{
    float targetFrameTime;  // 目标帧时间 (ms)
    float currentFrameTime;
    int qualityLevel;       // 0 (最低) ~ 10 (最高)
    
    // 监控性能
    void Update()
    {
        currentFrameTime = Time.unscaledDeltaTime * 1000f;
        float ratio = currentFrameTime / targetFrameTime;
        
        // 持续 30 帧低于目标 → 降级
        if (ratio > 1.15f && belowTargetFrames > 30)
            DecreaseQuality(1);
        
        // 持续 120 帧高于目标 → 升级
        if (ratio < 0.85f && aboveTargetFrames > 120)
            IncreaseQuality(1);
    }
    
    // 质量级别对应的设置
    QualityPreset GetPreset(int level)
    {
        return presets[Mathf.Clamp(level, 0, 10)];
    }
}

// 10 级质量预设表
// Level 0 (极低) ──── Level 5 (中) ──── Level 10 (极高)
// Texture: 256px      Texture: 1024px   Texture: 4096px
// Shadow: Off         Shadow: PCSS      Shadow: RT
// LOD: 2km max        LOD: 10km max     LOD: 无限
// Vegetation: 10%     Vegetation: 50%   Vegetation: 100%
// PostFX: Tonemap     PostFX: +Bloom    PostFX: Full chain
```

### 5.3 性能分析与调试工具

| 工具 | 用途 | 集成方式 |
|------|------|----------|
| Unity Profiler | CPU/GPU 帧分析 | 内置 |
| RenderDoc | GPU 帧捕获 | 外部 |
| NVIDIA Nsight | GPU 深度分析 | PC VR 开发 |
| Oculus Debug Tool | Quest 性能分析 | Quest 开发 |
| Unity Frame Debugger | 渲染流程分析 | 内置 |
| 自定义 Stats Overlay | 实时帧率/内存/网络 HUD | Runtime |
| Telemetry Logger | 云端性能数据收集 | 后端集成 |

#### Runtime Stats Overlay

```
┌─────────────────────────────────────────┐
│  DynastyVR Performance Overlay          │
│                                         │
│  FPS: 89.7  |  Frame: 11.15ms          │
│  CPU: 4.2ms |  GPU: 6.8ms              │
│                                         │
│  Draw Calls: 847   Batches: 612         │
│  Triangles: 12.4M  Vertices: 18.7M     │
│                                         │
│  VRAM: 3.2/6.0 GB  RAM: 5.8/8.0 GB    │
│  Texture: 1.8 GB   Mesh: 0.6 GB        │
│                                         │
│  Streaming: 3 loading  12 pending       │
│  Cache Hit: L1 92%  L2 97%  L3 99%    │
│                                         │
│  Network: ↓ 2.4 Mbps  ↑ 0.3 Mbps      │
│  Players: 24 visible  86 total          │
│  Latency: 32ms  |  Packet Loss: 0.0%   │
│                                         │
│  LOD: [7]  Quality: [8/10]  DRS: 1.0x  │
└─────────────────────────────────────────┘
```

---

## 六、性能测试用例

### 6.1 基准测试场景

| 测试 ID | 场景 | 预期指标 |
|---------|------|----------|
| PERF-001 | 太空俯瞰地球 | < 5ms GPU, 30fps |
| PERF-002 | 城市低空飞行 (500m) | < 11ms total, 90fps |
| PERF-003 | 曼哈顿街景 (步行) | < 11ms total, 90fps |
| PERF-004 | 318国道自驾 | < 11ms total, 90fps |
| PERF-005 | 200人同时在线广场 | < 11ms total, 90fps |
| PERF-006 | 黑洞近距离可视化 | < 8ms GPU, 90fps |
| PERF-007 | 极端天气（暴风雪+车流）| < 11ms total, 72fps |
| PERF-008 | 内存压力测试（快速移动）| 无 OOM, 无 freezing |

### 6.2 性能回归 CI

```yaml
# 每次 PR 触发性能测试
performance_ci:
  triggers: [push, pull_request]
  platforms: [pc_vr, quest_3, mobile]
  tests:
    - scene: "benchmark_city_flyover"
      duration: 60s
      metrics:
        avg_fps: "> 89"
        p99_frame_time: "< 14ms"
        memory_peak: "< 7.5GB"
        streaming_stalls: "< 5"
  on_failure: block_merge
```

---

## 七、性能优化检查清单

### 开发阶段

- [ ] 每个新功能附带性能预算评估
- [ ] 新 Shader 通过 GPU 时序分析
- [ ] 新模型通过 LOD 和面数审核
- [ ] 网络消息通过压缩比审核

### 里程碑阶段

- [ ] 完成所有 PERF 基准测试
- [ ] 内存泄漏检测 (24h 长运行测试)
- [ ] 多平台性能对比报告
- [ ] 热点分析 + 优化报告

### 发布阶段

- [ ] 低端设备兼容性验证
- [ ] 弱网环境测试 (< 2 Mbps)
- [ ] 2 小时连续运行稳定性
- [ ] 性能 Overhead Overlay 内置

---

> **文档版本：** v1.0
> **最后更新：** 2026-03-18
> **性能工程组：** EV 元宇宙部
> **审阅状态：** 初稿，待陛下御览
