# 🎮 DynastyVR — 资产管线技术方案

> 东方皇朝 EV 元宇宙部 · 资产工程组

---

## 1. UE5 资产管线架构

### 1.1 全流程概览

```
原始数据源                   UE5 编辑管线                    最终交付
─────────────               ──────────────                 ──────────
SRTM / DEM          ┌────► Datasmith 导入器       ┌────► Landscape .uasset
OSM / 3D Tiles      │       ↓                     │       ↓
卫星影像             │    HLOD / Nanite 构建       │    World Partition 分区
NeRF / 3DGS        │       ↓                     │       ↓
CAD / 扫描数据       │    材质实例 + LOD 生成      │    蓝图资产打包
AI 纹理生成         │       ↓                     │       ↓
```

### 1.2 Datasmith 导入管线

Datasmith 是连接外部 DCC 工具与 UE5 的核心桥梁。DynastyVR 使用 Datasmith 处理以下场景：

| 导入通道 | 源格式 | 目标 | 工具链 |
|----------|--------|------|--------|
| 地形通道 | GeoTIFF / DEM | Landscape Heightmap | Datasmith → Landscape Import |
| 建筑通道 | .udatasmith / FBX | Static Mesh + Material | Datasmith → Mesh Importer |
| 车辆通道 | FBX / GLTF | Skeletal Mesh + Physics | Datasmith → Chaos Vehicle Setup |
| 天体通道 | EXR / HDR | Cubemap / Material | HDRI Backdrop + Custom Import |

**Datasmith 导入配置标准：**

```yaml
# ProjectConfig/DatasmithImportSettings.yaml
mesh:
  generate_lightmap_uvs: true
  combine_meshes: false          # 保留原始层级用于 World Partition
  nanite_fallback_triangle_threshold: 50000  # >50k 三角面自动启用 Nanite
  collision:
    generate_collision: false    # 建筑用 Nanite 默认碰撞，车辆手动指定
    collsion_type: "Use Complex As Simple"  # 建筑精细碰撞

materials:
  import_textures: true
  texture_resolution_cap: 4096
  compression: "BC7"             # 高质量压缩
  texture_groups:
    diffuse: 4096
    normal: 4096
    orm: 2048                    # Occlusion/Roughness/Metallic 合并纹理
    emissive: 2048
```

### 1.3 资产目录结构

```
Content/
├── DynastyVR/
│   ├── Terrain/                 # 地形资产
│   │   ├── Heightmaps/          # SRTM DEM 原始灰度图
│   │   ├── Landscapes/          # UE5 Landscape .uasset
│   │   └── TileSource/          # 瓦片切割源文件
│   ├── Architecture/            # 建筑资产
│   │   ├── GlobalCities/        # 全球主要城市建筑
│   │   ├── China/               # 中国城市精修
│   │   ├── Landmarks/           # 地标建筑
│   │   └── Modular/             # 模块化建筑部件
│   ├── Vehicles/                # 车辆资产
│   │   ├── SportsCars/          # 跑车系列
│   │   ├── LuxurySedans/        # 豪华轿车
│   │   ├── SUVs/                # 越野车
│   │   └── Aircraft/            # 飞行器
│   ├── Cosmos/                  # 天体资产
│   │   ├── BlackHoles/          # 黑洞
│   │   ├── Planets/             # 行星
│   │   ├── Stars/               # 恒星/星空
│   │   └── Nebulae/             # 星云
│   ├── Vegetation/              # 植被资产
│   │   ├── Trees/
│   │   ├── Grass/
│   │   └── Flora/
│   ├── Materials/               # 共享材质
│   │   ├── MasterMaterials/     # 母材质
│   │   ├── MaterialInstances/   # 材质实例
│   │   └── Functions/           # 材质函数库
│   ├── Textures/                # 共享纹理
│   │   ├── Terrain/
│   │   ├── Architecture/
│   │   └── HDRI/
│   └── VFX/                     # 特效资产
│       ├── Particles/           # Niagara 粒子
│       └── PostProcess/         # 后处理
```

### 1.4 资产命名规范

```
{类型}_{类别}_{变体}_{细节级别}_{LOD}

示例:
Lnd_Terrain_WorldPart_01_LOD0        # 地形瓦片 01，LOD0
SM_Bldg_Residential_ModA_LOD0        # 建筑静态网格
SK_Vehicle_Ferrari_488_White_LOD0    # 车辆骨架网格
MI_Mat_Stone_Granite_01              # 材质实例
VFX_Particle_BlackHole_Accretion     # 特效粒子
```

### 1.5 自动化管线工具

| 工具 | 用途 | 实现方式 |
|------|------|----------|
| Asset Validation Suite | 导入后自动检测面数/纹理/碰撞 | UE5 Editor Utility Blueprint |
| Nanite Analyzer | 扫描 Mesh 是否满足 Nanite 阈值 | Python + UE5 Commandlet |
| Batch Material Builder | 批量创建材质实例 | Editor Utility Widget |
| LOD Generator | 多级 LOD 自动降级 | Simplygon 集成 |
| Texture Compressor | 纹理批量压缩 + Mip 生成 | BuildCookRun Pipeline |

---

## 2. 地形资产

### 2.1 数据源与精度层级

| 层级 | 数据源 | 精度 | 覆盖范围 |
|------|--------|------|----------|
| L0 — 基础全球 | Copernicus DEM (SRTM) | 30m | 全球 |
| L1 — 城市区域 | Copernicus DEM-30m + 本地 DEM | 10m | 主要城市 |
| L2 — 精修区域 | LiDAR 点云 / 倾斜摄影 | 1-5m | 重点区域 |
| L3 — 交互区域 | 摄影测量扫描 | cm 级 | 地标/建筑基底 |

### 2.2 SRTM → UE5 Landscape Heightmap 转换流程

```
步骤 1: 下载 SRTM 数据
  └─ USGS EarthExplorer / Copernicus Open Access Hub
  └─ 格式: GeoTIFF (.tif), WGS84, 32-bit float

步骤 2: 坐标系转换
  └─ WGS84 (EPSG:4326) → UE5 世界坐标
  └─ 使用 PROJ 库或 QGIS 批量投影
  └─ 转换公式: UE5_Z = HeightInMeters * 100 (UE 单位: cm)

步骤 3: 分辨率适配
  └─ UE5 Landscape 最大尺寸: 8129 × 8129 verts (单 tile)
  └─ SRTM 30m → 1km 区域 ≈ 33 verts
  └─ 需要降采样至合适的 Landscape 组件大小

步骤 4: 灰度图生成
  └─ 输出 16-bit TIFF (无损高度)
  └─ 确保无负值（海平面以下用 offset 补偿）
  └─ 文件名: Heightmap_{Region}_{TileX}_{TileY}_{Resolution}.tif

步骤 5: UE5 导入
  └─ Landscape Mode → Import from Heightmap
  └─ 设置 Scale: Z = 100 (米→厘米)
  └─ 分段 (Section Size): 63×63 或 127×127 verts
  └─ 组件 (Component): 1×1, 2×2, 或 4×4
```

**Python 转换脚本示例：**

```python
"""
srtm_to_ue5_heightmap.py
将 SRTM GeoTIFF 转换为 UE5 兼容的 16-bit 灰度高度图
"""
import numpy as np
import rasterio
from pathlib import Path

# UE5 参数
UE5_MAX_VERTS = 8129
UE5_SCALE_Z = 100.0  # meters to cm
UE5_SEA_LEVEL_OFFSET = 0.0  # 如需补偿海平面以下高度

def convert_srtm_to_ue5(input_tif: Path, output_png: Path, target_verts: int = 4033):
    """将 SRTM GeoTIFF 转换为 UE5 Landscape 高度图"""
    
    with rasterio.open(input_tif) as src:
        data = src.read(1).astype(np.float32)
        nodata = src.nodata
    
    # 处理无数据区域
    if nodata is not None:
        data[data == nodata] = 0.0
    
    # 高度偏移（UE5 不支持负高度）
    min_height = data.min()
    if min_height < 0:
        data -= min_height  # 所有高度提升
        print(f"  海平面偏移: {abs(min_height):.1f}m")
    
    # 重采样到目标分辨率
    from scipy.ndimage import zoom
    scale_factor = target_verts / data.shape[0]
    data_resampled = zoom(data, scale_factor, order=3)
    
    # 归一化到 16-bit (0 ~ 65535)
    h_min, h_max = data_resampled.min(), data_resampled.max()
    data_normalized = (data_resampled - h_min) / (h_max - h_min + 1e-8)
    data_16bit = (data_normalized * 65535).astype(np.uint16)
    
    # 保存为 16-bit PNG
    import cv2
    cv2.imwrite(str(output_png), data_16bit)
    
    print(f"  输入: {input_tif} ({data.shape[0]}×{data.shape[1]})")
    print(f"  输出: {output_png} ({target_verts}×{target_verts})")
    print(f"  高度范围: {h_min:.1f}m ~ {h_max:.1f}m")
```

### 2.3 World Partition 瓦片切割

UE5 World Partition 将超大世界自动切割为可流式加载的网格区块。

**World Partition 配置：**

| 参数 | 设置值 | 说明 |
|------|--------|------|
| Grid Cell Size | 1km × 1km | 每个加载单元 |
| Loading Range | 2km | 玩家周围加载半径 |
| Cell Loading Range | 500m | 触发加载的渐进距离 |
| HLOD | Level 1-3 | 远距离简化合并 |
| Streaming Source | 引擎自动 | 基于玩家位置/视角 |

**地形瓦片命名与分区规则：**

```
瓦片坐标系: 以 (0,0) 为原点 (通常是项目中心点, 如上海外滩)

WorldPart_Terrain_{TileX}_{TileY}
例如:
  WorldPart_Terrain_00_00    # 原点区域
  WorldPart_Terrain_01_00    # 东 1km
  WorldPart_Terrain_00_-01   # 南 1km

HLOD 层级:
  HLOD1: 4×4 合并为 1 个 Proxy
  HLOD2: 16×16 合并
  HLOD3: 全局简化 Mesh (用于从太空俯瞰)
```

### 2.4 Landscape 材质

```
M_Landscape_Master (母材质)
├── Layer Blend: Weight Blend
│   ├── Grass (草地)
│   ├── Dirt (泥土)
│   ├── Rock (岩石)
│   ├── Sand (沙地)
│   ├── Snow (雪地)
│   └── Urban (城市地面)
├── Height Blend: 基于高度自动混合
├── Distance Blend: 远处降低纹理细节
└── Nanite 兼容: 使用 Runtime Virtual Texture (RVT)
```

**Runtime Virtual Texture (RVT) 配置：**
- Virtual Texture Size: 8192 × 8192
- Physical Texture: 2048 × 2048
- 支持 Nanite Mesh 与 Landscape 的统一渲染

---

## 3. 建筑资产

### 3.1 数据源管线

```
OpenStreetMap (OSM)
  ↓
Overpass API 导出 (.osm)
  ↓
OSM2UE5 转换器 (自研 / Cesium for Unreal)
  ↓
初步 Static Mesh + Material
  ↓
Datasmith 导入 UE5
  ↓
Nanite 构建 + LOD 生成
  ↓
World Partition 分区放置
```

### 3.2 OSM → UE5 Static Mesh 自动转换

**转换层级：**

| 层级 | 多边形数 | 用途 | 模型策略 |
|------|----------|------|----------|
| LOD0 | 不限 (Nanite) | 0-500m | 全细节，窗户/阳台/屋顶细节 |
| LOD1 | 500-2000 | 500m-2km | 简化几何体，无窗户细节 |
| LOD2 | 100-500 | 2km-5km | 立方体 + 轮廓 |
| HLOD | <100 | >5km | 街区合并 Mesh |

**OSM2UE5 自动转换流程：**

```python
"""
osm_to_ue5_mesh.py
OSM Building 数据 → UE5 Static Mesh (Nanite-ready)
"""
from typing import List, Tuple
from dataclasses import dataclass
import numpy as np

@dataclass
class OSMBuilding:
    """单栋建筑数据"""
    footprint: List[Tuple[float, float]]  # 经纬度底面多边形
    height: float                          # 建筑高度 (米)
    levels: int                            # 楼层数
    building_type: str                     # residential/commercial/industrial
    roof_type: str                         # flat/gabled/hipped
    material_hint: str                     # brick/concrete/glass/stone

class OSM2UE5Converter:
    """OSM 建筑数据转 UE5 Static Mesh"""
    
    MESH_RESOLUTION = 1.0  # 窗户等细节最小分辨率 (米)
    MAX_VERTS_PER_BUILDING = 50000  # Nanite 下限
    
    def __init__(self, world_origin: Tuple[float, float]):
        self.origin_lon, self.origin_lat = world_origin
    
    def geo_to_ue5(self, lon: float, lat: float) -> Tuple[float, float, float]:
        """经纬度 → UE5 世界坐标 (厘米)"""
        # Web Mercator 近似投影
        dx = (lon - self.origin_lon) * 111320.0 * np.cos(np.radians(self.origin_lat))
        dy = (lat - self.origin_lat) * 111320.0
        return (dx * 100.0, dy * 100.0, 0.0)  # 转换为厘米
    
    def generate_mesh(self, building: OSMBuilding, lod: int = 0) -> dict:
        """生成建筑 Static Mesh 数据"""
        mesh_data = {
            "vertices": [],
            "triangles": [],
            "normals": [],
            "uvs": [],
            "materials": []
        }
        
        # 底面多边形 → 3D 拉伸
        base_verts = [self.geo_to_ue5(lon, lat) for lon, lat in building.footprint]
        height_cm = building.height * 100.0
        
        # 生成墙、屋顶几何体
        if lod <= 1:
            mesh_data = self._generate_detailed(building, base_verts, height_cm)
        else:
            mesh_data = self._generate_simplified(building, base_verts, height_cm)
        
        return mesh_data
    
    def _generate_detailed(self, building: OSMBuilding, base_verts: list, height_cm: float) -> dict:
        """LOD0-1: 包含窗户、阳台等细节"""
        # 墙面分割为窗户网格
        window_width = 1.5  # 米
        window_height = 2.0
        wall_thickness = 0.3
        
        # 屋顶生成（根据 roof_type）
        # 平屋顶: 直接封顶
        # 坡屋顶: 生成三角形面片
        ...
        
        return {"vertices": [], "triangles": [], "normals": [], "uvs": [], "materials": []}
    
    def _generate_simplified(self, building: OSMBuilding, base_verts: list, height_cm: float) -> dict:
        """LOD2+: 简化几何体"""
        # 仅生成外轮廓拉伸体 + 屋顶轮廓
        ...
        return {"vertices": [], "triangles": [], "normals": [], "uvs": [], "materials": []}
```

### 3.3 Nanite LOD 规范

**Nanite 入门要求：**

| 指标 | 要求 | 说明 |
|------|------|------|
| 最小三角面数 | 50,000+ | 低于此阈值无需 Nanite |
| 最大三角面数 | 无上限 (显存内) | 建筑单体建议 < 10M |
| UV 数量 | 最多 8 组 | 保留 UV0 用于贴图 |
| 材质数量 | < 8 per mesh | 超过会影响 Nanite 裁剪 |
| 顶点色 | 可选 | 用于 Nanite 偏移/遮罩 |

**Nanite 兼容性检查清单：**

```
□ 静态网格 (Static Mesh) — 不支持 Skeletal Mesh
□ 三角面 — 不支持 Points / Lines
□ 不使用世界位置偏移 (World Position Offset) — Nanite 不支持
□ 无半透明材质 — Nanite mesh 不支持 Translucency
□ LOD 缩放 (LOD Scale) = 1.0
□ 最大绘制距离 (Max Draw Distance) = 0 (无限制)
```

### 3.4 建筑材质系统

```
M_Architecture_Master (母材质)
├── 基于参数化建筑类型
│   ├── M_Arch_Concrete (混凝土)
│   │   ├── Base Color: procedural grunge
│   │   ├── Normal: subtle surface detail
│   │   └── Roughness: 0.4-0.8 (风化程度可调)
│   ├── M_Arch_Glass (玻璃幕墙)
│   │   ├── Base Color: tinted / clear
│   │   ├── Refraction: 1.52 (标准玻璃 IOR)
│   │   ├── Roughness: 0.05
│   │   └── Metallic: 0.0
│   ├── M_Arch_Stone (石材)
│   │   ├── Tiling: world-aligned (无 UV 接缝)
│   │   └── Distance Blend: 远处简化
│   └── M_Arch_Metal (金属)
│       ├── Metallic: 1.0
│       └── Anisotropic: 可选 (拉丝金属)
├── Runtime Virtual Texture 采样 (RVT)
└── 全局光照: Lumen GI 兼容
```

---

## 4. 车辆资产

### 4.1 豪车模型规范

**车辆资产包组成：**

```
SK_Vehicle_Ferrari488GTB/
├── SM_Ferrari488_Body_LOD0          # 车身 (Nanite)
├── SM_Ferrari488_Body_LOD1          # 车身简化
├── SM_Ferrari488_Interior_LOD0      # 内饰 (Nanite)
├── SM_Ferrari488_Wheel_FL           # 前左轮 (可旋转)
├── SM_Ferrari488_Wheel_FR           # 前右轮
├── SM_Ferrari488_Wheel_RL           # 后左轮
├── SM_Ferrari488_Wheel_RR           # 后右轮
├── SM_Ferrari488_SteeringWheel      # 方向盘 (可交互)
├── SM_Ferrari488_Door_Left          # 左车门 (可交互)
├── SM_Ferrari488_Door_Right         # 右车门 (可交互)
├── SM_Ferrari488_Hood               # 引擎盖 (可交互)
├── SM_Ferrari488_Trunk              # 后备箱 (可交互)
├── BC_Ferrari488_Blueprints         # Chaos 车辆蓝图
├── MI_Ferrari488_MatteBlack         # 车漆材质实例
├── MI_Ferrari488_Red
├── MI_Ferrari488_White
└── MI_Ferrari488_Gold               # 皇室金 (陛下专属)
```

### 4.2 Chaos Physics 碰撞体规范

**Chaos Vehicle 组件层级：**

```
BP_Vehicle_Ferrari488GTB (Vehicle Wheeled Pawn)
├── VehicleMesh (Skeletal Mesh Component)
│   ├── Wheel FL (Suspension + Tire + Brake)
│   │   ├── Bone: "WheelFL"
│   │   ├── Radius: 330mm
│   │   ├── MaxForce: 3500N
│   │   └── Friction: 3.5 (运动轮胎)
│   ├── Wheel FR (同上)
│   ├── Wheel RL (同上)
│   ├── Wheel RR (同上)
│   ├── Engine
│   │   ├── MaxRPM: 8250
│   │   ├── TorqueCurve: Ferrari 488 V8 映射
│   │   └── Gearing: 7-speed DCT
│   ├── Transmission
│   │   ├── Type: Automatic
│   │   └── Ratios: [3.08, 2.19, 1.64, 1.29, 1.03, 0.84, 0.67]
│   └── Steering
│       ├── MaxAngle: 38°
│       └── SpeedFactor: curve-based
├── Collision Setup
│   ├── Simple Box Collision (简化碰撞)
│   │   ├── Body_AABB: 包围车体
│   │   └── Wheel_Sphere: 每轮一个 Sphere
│   └── Complex Collision (精确碰撞, 用于精细交互)
│       └── Source: 车身 Nanite Mesh
├── Interaction Points
│   ├── Door_Left_Trigger (进入驾驶位)
│   ├── Door_Right_Trigger (进入乘客位)
│   ├── Hood_Trigger (查看引擎)
│   └── Trunk_Trigger (行李箱)
└── Interior Camera
    ├── DriverPOV (驾驶员视角)
    ├── PassengerPOV (乘客视角)
    ├── ThirdPerson (第三人称)
    └── Cinematic (自由机位)
```

### 4.3 可交互部件系统

| 部件 | 交互方式 | UE5 实现 |
|------|----------|----------|
| 车门 | 点击/VR手柄拉拽 | Physics Handle + Animation Blueprint |
| 方向盘 | 车内视角跟随鼠标/VR | Skeletal Mesh Bone Rotation |
| 车窗 | 升降 | Material Parameter (透明度) |
| 灯光 | 开/关 | Dynamic Material Instance (Emissive) |
| 引擎盖 | 打开 | Physics Constraint + Anim Montage |
| 雨刷 | 开/关 | Anim Montage 循环 |
| 悬挂 | 实时可见 | Chaos Suspension Line Trace 可视化 |

### 4.4 Nanite 车辆适配

**Nanite 适用/不适用区域：**

| 组件 | Nanite | 说明 |
|------|--------|------|
| 车身外壳 | ✅ | 高面数几何体，Nanite 最优场景 |
| 内饰 | ✅ | 方向盘、仪表盘、座椅 |
| 轮毂 | ✅ | 复杂辐条结构 |
| 轮胎 | ❌ | 需要变形 + 物理，用 Skeletal Mesh |
| 灯罩 | ❌ | 半透明材质，Nanite 不支持 |
| 玻璃 | ❌ | 半透明/折射，用普通 Static Mesh |

**Nanite 车辆 LOD 策略：**

```
LOD0 (0-50m):  全细节 Nanite — 200万~500万三角面
LOD1 (50-200m): Nanite 自动裁剪 — ~50万三角面
LOD2 (200-500m): 简化 Mesh — ~10万三角面 (可切换为传统 LOD)
LOD3 (500m+): Billboard 或 低模代理 — <1万三角面
```

### 4.5 车辆物理材质配置

```
PhysicalMaterial_VehicleTire:
  Friction: 3.5 (dry road)
  Restitution: 0.1
  Density: 1.2
  
PhysicalMaterial_VehicleBody:
  Friction: 0.8
  Restitution: 0.05
  Density: 7.8 (steel equivalent)

ChaosVehicleConfig:
  DragCoefficient: 0.31 (Ferrari 488)
  DownforceCoefficient: 0.45 (high-speed)
  Mass: 1370 kg
  CenterOfMass: (0, 0, -15) cm (偏低以增加稳定性)
```

---

## 5. 天体资产

### 5.1 黑洞 — Material Graph + Custom HLSL

黑洞视觉效果由 UE5 Material Graph 驱动，核心引力透镜效果通过 Custom HLSL 节点实现。

**黑洞资产结构：**

```
SK_BlackHole_Schwarzschild/
├── M_BlackHole_Master (母材质)
│   ├── M_BlackHole_AccretionDisk (吸积盘)
│   ├── M_BlackHole_PhotonRing (光子环)
│   └── M_BlackHole_HawkingRadiation (霍金辐射粒子)
├── NS_BlackHole (Niagara System)
│   ├── NS_AccretionDisk_Particles
│   ├── NS_HawkingRadiation
│   └── NS_Jet_Outflow
└── BP_BlackHole (Actor Blueprint)
    ├── StaticMesh: Sphere (Event Horizon)
    ├── PostProcessVolume (引力透镜区域)
    └── NiagaraComponent
```

**引力透镜 Shader (Custom HLSL)：**

```hlsl
// CustomHLSL_Node_GravitationalLensing
// 实现 Schwarzschild 度规下的光线偏折效果

void GravitationalLensing_float(
    float3 WorldPosition,
    float3 BlackHoleCenter,
    float SchwarzschildRadius,
    float3 CameraPosition,
    out float3 LensOffset,
    out float LensIntensity)
{
    float3 toBH = BlackHoleCenter - WorldPosition;
    float dist = length(toBH);
    float distNorm = dist / SchwarzschildRadius;
    
    // Schwarzschild 引力透镜近似
    // 偏折角 = 4GM / (c² * b), b = 碰撞参数
    float deflectionAngle = 2.0 * SchwarzschildRadius / max(dist, SchwarzschildRadius * 1.001);
    
    // 临界半径内 → 光子被捕获 (事件视界)
    float eventHorizonMask = smoothstep(1.0, 1.1, distNorm);
    
    // 光子环增强 (r = 1.5 * Rs 处的不稳定轨道)
    float photonRingDist = abs(distNorm - 1.5);
    float photonRingIntensity = exp(-photonRingDist * 8.0) * 3.0;
    
    // 偏移计算
    float3 dir = normalize(toBH);
    float3 perpendicular = cross(dir, float3(0, 0, 1));
    LensOffset = perpendicular * deflectionAngle * 50.0;  // Scale factor
    
    // 强度衰减
    LensIntensity = photonRingIntensity * eventHorizonMask;
}

// 吸积盘多普勒效应 (相对论性光束增强)
void AccretionDiskDoppler_float(
    float3 Velocity,
    float3 ToCamera,
    float IntrinsicLuminosity,
    out float DopplerFactor)
{
    float cosAngle = dot(normalize(Velocity), normalize(ToCamera));
    // 相对论多普勒频移
    float beta = length(Velocity) / 299792458.0;  // v/c
    float gamma = 1.0 / sqrt(1.0 - beta * beta);
    DopplerFactor = 1.0 / (gamma * (1.0 - beta * cosAngle));
    DopplerFactor = pow(DopplerFactor, 3.0);  // 光束增强 (beaming)
}
```

**吸积盘 Material Graph 结构：**

```
M_BlackHole_AccretionDisk:
├── Base Color
│   ├── Inner Disk: Temperature → Black Body Radiation (3000K-10000K)
│   │   └── Color Ramp: 暗红 → 橙黄 → 白热
│   ├── Outer Disk: Cooler gas (1000K-3000K) → 暗红色
│   └── Doppler Beaming: 前侧增亮，后侧变暗
├── Emissive
│   ├── Intensity: 1000-5000 (HDR Bloom 驱动)
│   ├── Spiral Pattern: Voronoi + Radial distortion
│   └── Turbulence: 噪声驱动的湍流纹理
├── Opacity
│   ├── Edge Fade: 椭圆遮罩 (边缘渐隐)
│   ├── Event Horizon Mask: 中心镂空
│   └── Density Variation: 密度从内到外递减
└── World Position Offset
    └── Orbital Motion: 绕黑洞中心旋转 (材质驱动)
```

### 5.2 行星表面

**行星资产结构：**

```
Planet_Earth/
├── BP_Planet_Earth (Actor)
│   ├── StaticMesh: Sphere (高分段球体)
│   │   ├── Radius: 6371km (真实比例) → UE5 坐标
│   │   └── Segments: 128×128 (近距) / 32×32 (远距)
│   ├── M_Planet_Earth_Master (母材质)
│   │   ├── Albedo: Satellite composite texture (8K)
│   │   ├── Normal: DEM-derived bump (4K)
│   │   ├── Clouds: Separate layer, rotating (4K)
│   │   ├── Atmosphere: Shader-based scattering
│   │   └── City Lights: Night-side emissive (4K)
│   └── M_Planet_Atmosphere (Shell材质)
│       ├── Rayleigh Scattering
│       ├── Mie Scattering
│       └── Ozone Absorption
```

**大气散射 Shader 要点：**

```
M_Planet_Atmosphere:
├── Rayleigh Scattering
│   ├── Wave-dependent: λ^-4
│   ├── Scale Height: 8.5 km
│   └── Scattering Coefficients: (5.8e-6, 13.5e-6, 33.1e-6)
├── Mie Scattering
│   ├── Scale Height: 1.2 km
│   ├── Phase Function: Henyey-Greenstein (g=0.76)
│   └── Extinction: 21e-6
├── Render Settings
│   ├── Two-sided: true
│   ├── Blend Mode: Translucent
│   ├── Shadow: Casts translucency shadow
│   └── Separate Translucency: true
└── Performance
    ├── Atmosphere Shell Radius: PlanetRadius + 60km
    ├── Samples: 16 (quality) / 8 (performance)
    └── God Rays: Optional volumetric
```

### 5.3 月球 / 火星专用资产

| 行星 | 数据源 | 精度 | 材质策略 |
|------|--------|------|----------|
| 月球 | LRO LOLA DEM | 60m 全球 | Heightmap + PBR (无大气) |
| 月球(精修) | LROC NAC 影像 | 0.5m | Nanite Mesh + 高精度纹理 |
| 火星 | MOLA + HiRISE | 128px/deg | Heightmap + PBR + 薄大气 |

---

## 6. AI 辅助资产生成

### 6.1 NeRF / 3DGS 重建流程

**用于精细区域的快速资产生成：**

```
输入阶段
├── 数据采集
│   ├── 倾斜摄影: 无人机环绕拍摄 (150-300 张, 4K+)
│   ├── 地面扫描: DJI L2 LiDAR + 相机
│   └── 手机采集: iPhone LiDAR + Photogrammetry
│
├── NeRF 训练 (Nerfstudio / Instant-NGP)
│   ├── 输入: COLMAP 预处理 (SfM 点云 + 相机位姿)
│   ├── 训练: NVIDIA A100, ~2-4h (单场景)
│   ├── 输出: 隐式神经场表示
│   └── 网格提取: Marching Cubes → Mesh
│
├── 3D Gaussian Splatting (3DGS) 训练
│   ├── 输入: 同上 + 深度图 (如有序列)
│   ├── 训练: ~15min (单场景, A100)
│   ├── 输出: 高斯椭球体集合
│   └── 优势: 实时渲染, 更高保真
│
└── UE5 集成
    ├── 网格优化 (Decimation → <500k tris)
    ├── UV 展开 (Auto UV / Xatlas)
    ├── 法线烘焙 (高模 → 低模)
    ├── 纹理烘焙 (Albedo / Normal / Roughness)
    └── Datasmith 导入 → Nanite 构建
```

**3DGS → UE5 转换管线：**

```python
"""
3dgs_to_ue5.py
3D Gaussian Splatting 结果转换为 UE5 可用的 Static Mesh
"""
from dataclasses import dataclass
import numpy as np

@dataclass
class Gaussian:
    position: np.ndarray    # (x, y, z)
    covariance: np.ndarray  # 3x3
    color: np.ndarray       # SH coefficients
    opacity: float
    scale: np.ndarray

def convert_3dgs_to_mesh(ply_path: str, output_fbx: str, 
                          max_triangles: int = 500000,
                          texture_resolution: int = 4096):
    """
    将 3DGS PLY 文件转换为 UE5 Static Mesh
    """
    # 1. 加载高斯点云
    gaussians = load_ply(ply_path)
    
    # 2. Poisson Surface Reconstruction → 初始网格
    mesh = poisson_reconstruct(gaussians, depth=8)
    
    # 3. 网格简化 (保持特征)
    mesh_simplified = quadric_decimation(mesh, target=max_triangles)
    
    # 4. UV 展开
    mesh_uvs = xatlas_unwrap(mesh_simplified)
    
    # 5. 法线 + 纹理烘焙 (从高斯渲染)
    normal_map, albedo_map = bake_from_gaussians(
        mesh_simplified, gaussians, resolution=texture_resolution
    )
    
    # 6. 导出 FBX
    export_fbx(mesh_simplified, albedo_map, normal_map, output_fbx)
    
    return output_fbx
```

### 6.2 AI 纹理生成

**AI 纹理生成管线：**

| 阶段 | 工具 | 输入 | 输出 |
|------|------|------|------|
| 基础纹理 | Substance 3D Sampler + AI | 照片 | PBR 材质 (Base Color/Normal/Roughness) |
| 无缝化 | Stable Diffusion Inpainting | 有接缝纹理 | 无缝 Tileable 纹理 |
| 风格化 | ControlNet + SD | 参考风格 | 保持 PBR 参数的风格化纹理 |
| 特定材质 | MaterialMaker + AI | 参数描述 | 程序化材质 (GDL / SDF) |
| 分辨率提升 | ESRGAN / Real-ESRGAN | 低分辨率纹理 | 4x/8x 超分辨率 |
| 频率分离 | 自研工具 | 高频/低频 | 距离混合 LOD 纹理 |

**AI 纹理生成管线示例 (城市建筑)：**

```python
"""
ai_texture_generation.py
使用 Stable Diffusion + ControlNet 批量生成建筑纹理
"""
from diffusers import StableDiffusionControlNetPipeline
from controlnet_aux import CannyDetector

class ArchitecturalTextureGenerator:
    """建筑纹理 AI 生成器"""
    
    BASE_PROMPT = (
        "photorealistic {material} wall texture, PBR material, "
        "seamless tileable, 4k, detailed surface, "
        "architectural material, building facade"
    )
    
    MATERIAL_PRESETS = {
        "concrete": {"material": "weathered concrete", "roughness_range": (0.4, 0.8)},
        "brick": {"material": "red brick", "roughness_range": (0.6, 0.9)},
        "glass": {"material": "reflective glass panel", "roughness_range": (0.05, 0.15)},
        "stone": {"material": "natural limestone", "roughness_range": (0.3, 0.7)},
        "metal_panel": {"material": "brushed aluminum", "roughness_range": (0.1, 0.4)},
    }
    
    def generate_base_color(self, material_type: str, seed: int = 42) -> str:
        """生成 Albedo/BaseColor 贴图"""
        prompt = self.BASE_PROMPT.format(**self.MATERIAL_PRESETS[material_type])
        return self.pipeline(prompt=prompt, num_inference_steps=30, 
                           generator=torch.manual_seed(seed)).images[0]
    
    def generate_normal_map(self, base_color_image, material_type: str) -> str:
        """从 Base Color 生成法线贴图"""
        return self.normal_generator(
            image=base_color_image,
            prompt=f"{material_type} surface detail normal map"
        )
    
    def generate_roughness_map(self, material_type: str) -> str:
        """生成粗糙度贴图"""
        roughness_range = self.MATERIAL_PRESETS[material_type]["roughness_range"]
        return self.roughness_generator(
            material_type=material_type,
            base_roughness=np.random.uniform(*roughness_range)
        )
    
    def generate_material_set(self, material_type: str, resolution: int = 4096) -> dict:
        """生成完整 PBR 材质集"""
        return {
            "base_color": self.generate_base_color(material_type),
            "normal": self.generate_normal_map(material_type),
            "roughness": self.generate_roughness_map(material_type),
            "ao": self.generate_ao_map(material_type),
            "height": self.generate_height_map(material_type),
        }
```

### 6.3 AI 辅助资产工作流集成

```
UE5 编辑器 → Editor Utility Widget (AI 辅助面板)
├── 一键纹理生成: 选择 Mesh → 自动生成 PBR 材质
├── NeRF 重建入口: 拖入照片文件夹 → 自动重建
├── 风格迁移: 选择参考图 → 批量材质风格化
├── 智能 LOD: 自动分析 Mesh 并生成 LOD Chain
└── 质量检查: AI 检测 UV/法线/碰撞问题
```

---

## 7. 资产规模估算

### 7.1 各类资产数量估算

| 资产类别 | 数量级 | 单体面数 (平均) | 说明 |
|----------|--------|----------------|------|
| **全球地形瓦片** | ~15,000 tiles | N/A (Heightmap) | 1km×1km 瓦片覆盖地表 |
| **城市建筑 (全局)** | ~500,000 栋 | 5k-50k tris | OSM 自动转换，LOD 递减 |
| **精修建筑 (重点城市)** | ~5,000 栋 | 50k-500k tris | Nanite, 手工优化 |
| **地标建筑** | ~200 栋 | 500k-5M tris | Nanite, 电影级品质 |
| **豪宅模块部件** | ~2,000 件 | 500-10k tris | UGC 模块化建筑系统 |
| **车辆 (车型)** | ~50 款 | 200k-2M tris | Nanite + Chaos Physics |
| **车辆 (变体)** | ~500 变体 | 同上 | 颜色/内饰/改装变体 |
| **飞行器** | ~20 款 | 300k-1.5M tris | Nanite + 飞行物理 |
| **行星 (全尺寸)** | ~8 颗 | N/A (Shader) | 材质驱动 + 次表面 |
| **黑洞** | ~5 种 | N/A (Shader) | Material + Niagara |
| **星空系统** | ~120,000 颗 | Billboard | Hipparcos 星表 |
| **星云** | ~50 个 | N/A (Volumetric) | Niagara / Volume |
| **树木种类** | ~200 种 | 5k-50k tris | Nanite, PCG 实例化 |
| **植被草丛** | ~100 种 | 200-2k tris | 实例化渲染 |
| **共享材质实例** | ~3,000 个 | N/A | Material Instance |
| **纹理贴图** | ~15,000 张 | N/A | 各分辨率混合 |

### 7.2 存储预算

| 资产类别 | 单体大小 (平均) | 数量 | 预估总大小 | 备注 |
|----------|----------------|------|-----------|------|
| **地形 Heightmaps** | 16MB/瓦片 | 15,000 | **240 GB** | 16-bit TIFF |
| **地形材质纹理** | 8MB/瓦片 | 15,000 | **120 GB** | 4K RVT |
| **建筑 Static Mesh** | 2MB/栋 | 505,200 | **~1 TB** | `.uasset` 打包后 |
| **建筑纹理** | 20MB/栋 (avg) | 505,200 | **~2 TB** | Base/Normal/ORM |
| **车辆资产包** | 100MB/车型 | 50 | **5 GB** | Mesh + Texture + Phys |
| **车辆变体** | 5MB/变体 | 450 | **2.25 GB** | 仅纹理差异 |
| **天体资产** | 500MB/行星 | 8 | **4 GB** | 高分辨率贴图 |
| **黑洞资产** | 50MB/黑洞 | 5 | **250 MB** | Shader + 粒子 |
| **星空数据** | 200MB | 1 | **200 MB** | 星表 + 纹理 |
| **共享材质** | 1MB/实例 | 3,000 | **3 GB** | Material Instance |
| **共享纹理库** | 8MB/张 (avg) | 5,000 | **40 GB** | 4K PBR 材质集 |
| **Niagara 特效** | 10MB/系统 | 200 | **2 GB** | 粒子系统 |
| **蓝图 + 配置** | — | — | **10 GB** | 蓝图 + DataTable + 音效 |

### 7.3 总存储预算

| 层级 | 大小 | 说明 |
|------|------|------|
| **源文件 (DCC / 原始数据)** | **~4 TB** | 未压缩的高精度源文件 |
| **UE5 编辑器资产** | **~2.5 TB** | `Content/` 目录完整 |
| **打包后 Build** | **~800 GB** | Cook + Pak 后的发行版 |
| **CDN 分发包** | **~600 GB** | 压缩 + 流式加载分块 |

### 7.4 内存预算 (运行时)

| 场景 | 内存占用 | 说明 |
|------|----------|------|
| **地表飞行 (2km 视距)** | ~8 GB | World Partition 流式 |
| **城市地面** | ~12 GB | 建筑 + 车辆 + 植被 |
| **太空视图** | ~4 GB | 星空 + 行星 + 大气 |
| **黑洞场景** | ~6 GB | 吸积盘 + 引力透镜后处理 |
| **目标 VR 内存** | **< 12 GB** | Quest 3 / PC VR |

### 7.5 性能指标

| 指标 | 目标值 | 说明 |
|------|--------|------|
| FPS (VR) | ≥ 90 FPS | 双眼渲染，VR 最低帧率 |
| FPS (Desktop) | ≥ 60 FPS | 桌面端最低帧率 |
| Draw Calls | < 5,000/frame | Nanite + RVT 帮助合并 |
| Triangle Budget | < 100M/frame | Nanite 自动裁剪 |
| Texture Memory | < 4 GB | 流式加载 + 压缩 |
| Shader Compile | < 30s | PSO 预编译 |

---

## 附录

### A. 工具链清单

| 工具 | 用途 | 许可 |
|------|------|------|
| Unreal Engine 5.4+ | 核心引擎 | 免费 (版税) |
| Cesium for Unreal | 全球 3D 地理数据 | 商业 |
| Houdini | 高级地形 + 程序化 | 商业 |
| Substance 3D Sampler | AI 纹理生成 | Adobe CC |
| Nerfstudio | NeRF 训练 | MIT |
| xatlas | 自动 UV 展开 | MIT |
| Simplygon | LOD 生成 | 商业 |
| Blender | 3D 建模 + 预处理 | GPL |
| Python 3.10+ | 管线脚本 | PSF |
| QGIS | 地理数据处理 | GPL |

### B. 关键文件路径

| 文件 | 路径 |
|------|------|
| UE5 项目文件 | `Content/DynastyVR/` |
| Datasmith 导入配置 | `Config/DatasmithImportSettings.yaml` |
| World Partition 配置 | `Config/WorldPartition.ini` |
| 资产命名规范 | `Config/AssetNamingRules.yaml` |
| Build Pipeline | `Build/` |
| AI 工具脚本 | `Tools/AIPipeline/` |

---

> 📋 **文档版本**: v1.0  
> 👤 **编写**: EV-asset-engineer  
> 📅 **日期**: 2026-03-18  
> 🏛️ **项目**: DynastyVR — 数字孪生宇宙
