# 班公湖地形资产创建指南
## DynastyVR — 班公湖 POC（UE5）

**版本：** 1.0  
**创建日期：** 2026-03-18  
**资产工程师：** EV-asset

---

## 1. 数据源

### 1.1 高程数据
- **数据源：** Copernicus DEM（COP30）
- **分辨率：** 30m
- **坐标范围：** 33.5°N - 33.9°N, 78.5°E - 79.3°E
- **下载地址：** https://spacedata.copernicus.eu/collections/copernicus-digital-elevation-model

### 1.2 影像数据
- **主数据源：** Bing Maps Aerial（高分辨率区域）
- **次数据源：** Sentinel-2（10m分辨率，覆盖全区）
- **用途：** 地形纹理、参考图、植被分布分析

### 1.3 精修区域
- **位置：** 班公湖东端（中印边境大桥附近）
- **范围：** 约 3km²
- **重点：** 桥梁周边地形细节、岸边碎石滩、湖岸线

---

## 2. UE5 Landscape 创建步骤

### 2.1 DEM 转 Heightmap

1. 下载 Copernicus DEM GeoTIFF 文件
2. 使用 GDAL 裁剪至目标区域
3. 归一化到 UE5 高度范围（0-65535）
4. 输出为 16bit PNG 格式
5. 导入 UE5 Landscape

### 2.2 Landscape 参数

| 参数 | 精修区 | 远景区 |
|------|--------|--------|
| 尺寸 | 8km × 8km | 全区 40km × 40km |
| 分辨率 | 1m/像素 | 10m/像素 |
| 组件数 | 8×8 | LOD 层级 |
| Section 大小 | 201×201 | 101×101 |

### 2.3 Layer Info 配置

- 创建 6 个 **Weight-Blended Layer**
- 使用 **Landscape Layer Info Object** 配置每个图层
- 权重混合模式：Height Blend（基于高度） + 手动绘制
- 设置合理的 Falloff 参数确保平滑过渡

---

## 3. 地形材质分层

### 3.1 图层定义

| 图层 | 名称 | 高程范围 | 材质描述 |
|------|------|----------|----------|
| Layer 1 | 湖底 | < 4,241m | 盐结晶白色/浅褐色，干涸湖底有龟裂纹理 |
| Layer 2 | 湖岸碎石 | 4,241-4,250m | 灰色砾石，潮湿区域深色，散布小石块 |
| Layer 3 | 山坡 | 4,250-5,000m | 褐色风化岩石，稀疏碎石 |
| Layer 4 | 高海拔岩石 | 5,000-5,500m | 深灰色花岗岩，棱角分明 |
| Layer 5 | 积雪 | > 5,500m | 白色积雪，边缘有融雪痕迹 |
| Layer 6 | 草甸 | 局部湖岸 | 极少量绿色草甸，仅低洼湿润处 |

### 3.2 材质技术要求

- **主材质：** UE5 Runtime Virtual Texture (RVT) 混合
- **混合方式：** Height-based blend + Slope-based mask
- **贴图分辨率：**
  - 精修区：2K per layer
  - 远景区：512px-1K per layer
- **法线贴图：** 必需，增强表面细节
- **Roughness：** 根据材质类型调整（盐结晶粗糙度高，湿润砾石低）

---

## 4. PCG 植被生成

### 4.1 班公湖植被特征

班公湖位于高海拔干旱地区（4,241m+），植被极其稀少：
- **骆驼刺（Alhagi sparsifolia）** — 耐旱灌木，湖岸零星分布
- **红柳（Tamarix）** — 喜盐碱，湖边局部
- **稀疏灌木** — 低洼处少量
- **高海拔区域基本无植被**

### 4.2 PCG Framework 配置

```
PCG Graph 结构：

1. Surface Sampler（地形采样）
   ├── Landscape Layer Blend（基于草甸图层权重）
   ├── Slope Filter（坡度 < 15°）
   └── Height Filter（4,241m - 4,300m）

2. Density Modifier
   ├── 基础密度：10-20 棵/公顷
   └── 随机化：±50%

3. Static Mesh Spawner
   ├── 骆驼刺（30% 概率）
   ├── 红柳（20% 概率）
   └── 小灌木（50% 概率）
```

### 4.3 参数建议

- **密度：** 极低（10-20 棵/公顷）
- **缩放随机：** 0.8x - 1.2x
- **旋转随机：** Y轴 0-360°
- **剔除距离：** 500m（植被稀少，可适当拉近）
- **LOD：** 2级（近距离保持细节）

---

## 5. 岩石资产

### 5.1 资产分类

| 类型 | 描述 | 技术方案 | 数量 |
|------|------|----------|------|
| 砾石滩 | 散布的小碎石（10-50cm） | Nanite mesh，PCG 批量放置 | 大量 |
| 中等石块 | 岸边中型岩石（0.5-2m） | Nanite mesh，随机旋转 | 中等 |
| 巨石 | 标志性大石头（2-5m） | Nanite mesh，手工放置 | 10-20 |
| 山壁 | 风化岩壁露头 | Nanite mesh，沿等高线放置 | 按需 |

### 5.2 资产制作要求

- **Nanite 兼容：** 所有岩石资产必须支持 Nanite
- **LOD 策略：** Nanite 自动处理，无需手动 LOD
- **材质：** 共用岩石主材质，通过参数集区分
- **纹理集：** 建议 4K 纹理图集（Albedo, Normal, Roughness, AO）
- **碰撞：** 简化碰撞体（复杂度适中即可）

### 5.3 放置原则

1. **砾石滩：** 沿湖岸线密集分布，向陆地方向逐渐稀疏
2. **中等石块：** 随机散布，避免规律性排列
3. **巨石：** 桥梁附近、湖岸转角处，作为视觉焦点
4. **山壁：** 沿等高线分布，与地形材质 Layer 4 配合

---

## 6. 数据处理脚本（Python）

### 6.1 DEM → Heightmap 转换流程

```python
#!/usr/bin/env python3
"""
班公湖 DEM 转 UE5 Heightmap 工具
依赖：pip install rasterio numpy
"""

import rasterio
from rasterio.windows import from_bounds
import numpy as np

# 配置参数
BBOX = {
    'west': 78.5,
    'south': 33.5,
    'east': 79.3,
    'north': 33.9
}
UE5_HEIGHT_MIN = 0
UE5_HEIGHT_MAX = 65535
OUTPUT_SIZE = (8192, 8192)  # 8K for refined area

def dem_to_heightmap(input_tif, output_png):
    """将 Copernicus DEM 转换为 UE5 兼容的 16bit PNG Heightmap"""
    
    # 1. 打开 DEM 文件
    with rasterio.open(input_tif) as src:
        # 2. 裁剪至目标区域
        window = from_bounds(
            BBOX['west'], BBOX['south'],
            BBOX['east'], BBOX['north'],
            src.transform
        )
        dem_data = src.read(1, window=window)
    
    # 3. 移除无效值
    dem_data = np.where(dem_data < -1000, 0, dem_data)
    
    # 4. 归一化到 UE5 高度范围
    height_min = dem_data.min()
    height_max = dem_data.max()
    
    normalized = (dem_data - height_min) / (height_max - height_min)
    heightmap = (normalized * UE5_HEIGHT_MAX).astype(np.uint16)
    
    # 5. 调整尺寸（如需要）
    from PIL import Image
    img = Image.fromarray(heightmap, mode='I;16')
    img = img.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS)
    
    # 6. 保存为 16bit PNG
    img.save(output_png, format='PNG')
    
    print(f"✅ Heightmap 已生成: {output_png}")
    print(f"   原始高度范围: {height_min:.1f}m - {height_max:.1f}m")
    print(f"   输出尺寸: {OUTPUT_SIZE}")

if __name__ == "__main__":
    dem_to_heightmap(
        input_tif="Copernicus_DEM_30m.tif",
        output_png="BangongLake_Heightmap_8k.png"
    )
```

### 6.2 批处理流程

```bash
# 1. 下载 DEM（使用 gdown 或 aria2）
gdown <Copernicus_DEM_Link> -O ./raw/

# 2. 裁剪 + 转换
python dem_to_heightmap.py --input ./raw/Copernicus_DEM.tif --output ./processed/

# 3. 生成 Mipmaps（可选）
# 在 UE5 中导入时自动处理

# 4. 生成权重贴图（可选，用于自动材质混合）
python generate_weight_maps.py --heightmap ./processed/BangongLake_Heightmap_8k.png
```

---

## 7. 性能优化

### 7.1 World Partition 分区策略

```
班公湖 World Partition 配置：

Grid Size: 2km × 2km
Loading Range: 4km
Cell Size: 256m × 256m

分区逻辑：
├── 核心区（精修区 8km²）：最高优先级，玩家常驻
├── 湖面区域：中等优先级
├── 远山区域：低优先级
└── 边界区域：最低优先级，延迟加载
```

### 7.2 HLOD 层级

| 层级 | 距离范围 | 简化比例 | 用途 |
|------|----------|----------|------|
| LOD 0 | 0-500m | 100% | 近景完整细节 |
| LOD 1 | 500-2000m | 50% | 中景简化 |
| LOD 2 | 2000-5000m | 25% | 远景高度简化 |
| HLOD 1 | > 5000m | 合并网格 | 超远景合并 |

### 7.3 Virtual Texture 配置

```
Runtime Virtual Texture (RVT) 设置：

- RVT 分辨率：8192 × 8192（精修区）
- 虚拟纹理尺寸：8192 × 8192
- Mip 生成：自动
- 材质采样：RVT Sample 节点

启用 Virtual Texture 的地形材质层：
✅ Layer 1（湖底）
✅ Layer 2（湖岸碎石）
✅ Layer 3（山坡）
✅ Layer 4（高海拔岩石）
❌ Layer 5（积雪）— 纯色，无需 VT
❌ Layer 6（草甸）— 面积太小
```

### 7.4 其他优化建议

- **Nanite 岩石：** 开启 Nanite 替代传统 LOD
- **遮挡剔除：** 启用 occlusion culling
- **流式加载：** 配置 Streaming Source（基于玩家位置）
- **Distance Field Shadow：** 远景使用 DFAO 替代 SSAO
- **Anti-Aliasing：** TSR（Temporal Super Resolution）

---

## 附录

### A. 参考资料

- Copernicus DEM 文档：https://spacedata.copernicus.eu/
- UE5 Landscape 官方文档：https://docs.unrealengine.com/
- UE5 PCG Framework：https://docs.unrealengine.com/
- 班公湖地理信息：Wikipedia - Pangong Tso

### B. 文件结构

```
Content/
└── BangongLake/
    ├── Maps/
    │   └── BangongLake_Map.umap
    ├── Landscape/
    │   ├── Heightmaps/
    │   │   ├── BL_Heightmap_8k.png
    │   │   └── BL_Heightmap_80k.png
    │   └── Layers/
    │       ├── L_LakeBed
    │       ├── L_ShoreGravel
    │       ├── L_Slope
    │       ├── L_HighRock
    │       ├── L_Snow
    │       └── L_Meadow
    ├── Materials/
    │   ├── M_BangongLake_LandscapeMaster.uasset
    │   └── MI_*.uasset
    ├── PCG/
    │   └── PCG_BangongLake_Vegetation.uasset
    ├── RockAssets/
    │   ├── SM_Gravel_*.uasset
    │   ├── SM_Boulder_*.uasset
    │   └── SM_Cliff_*.uasset
    └── Textures/
        ├── T_LakeBed_*.uasset
        ├── T_ShoreGravel_*.uasset
        └── ...
```

---

*文档编写：EV-asset | 东方皇朝元宇宙部*
