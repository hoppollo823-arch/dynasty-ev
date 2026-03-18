# DynastyVR Earth 🌏🏛️

> 东方皇朝元宇宙部 — VR地球体验项目  
> Unity + Cesium 构建的沉浸式地球探索应用

## 项目概述

DynastyVR Earth 是一个基于 Unity 引擎和 Cesium for Unity 插件构建的 VR 地球体验应用。用户可以通过 VR 设备沉浸式地探索地球表面的著名地标、古城遗迹和自然奇观。

## 核心功能

- 🌍 **全球地标探索** — 30+ 精选全球地标数据
- 🗺️ **路线漫游** — 自贡→班公湖完整路线（60+ GPS坐标点）
- 🕐 **动态时间系统** — 可调时间与光照
- 🌤️ **天气系统** — 多种天气效果
- 👁️ **VR沉浸体验** — 支持主流VR设备

## 数据文件结构

```
UnityVR-Earth/
├── Data/
│   ├── Route/
│   │   └── zigong_to_bangong.json    # 自贡→班公湖完整路线坐标
│   ├── Places/
│   │   └── landmarks.json            # 全球热门地标数据
│   └── Config/
│       └── SceneConfig.json          # 场景全局配置
├── Scripts/                          # Unity C# 脚本（待接入）
├── Resources/                        # Unity资源文件（待接入）
└── README.md
```

## 数据格式

### 路线数据 (Route)

```json
{
  "name": "站点名称",
  "lng": 104.7786,     // 经度
  "lat": 29.3454,      // 纬度
  "alt": 310,          // 海拔（米）
  "description": "站点简介"
}
```

### 地标数据 (Landmarks)

```json
{
  "id": "unique_id",
  "name": "地标名称",
  "lng": 121.5055,
  "lat": 31.2397,
  "alt": 50,
  "country": "国家",
  "category": "城市|古迹|自然|地标|太空",
  "description": "地标描述"
}
```

## 朝圣之路路线

从 **自贡彩灯公园** 出发，沿川藏南线/滇藏线一路向西：

1. **四川段**：自贡 → 乐山 → 西昌 → 攀枝花
2. **云南段**：丽江 → 大理（崇圣三塔、洱朵酒店）→ 香格里拉（时轮大坛城）
3. **藏东南**：梅里雪山 → 芒康 → 然乌湖 → 巴松措
4. **拉萨周边**：拉萨 → 布达拉宫 → 桑耶寺 → 羊卓雍错
5. **后藏**：日喀则（扎什伦布寺）→ 纳木错
6. **藏北无人区**：色林错 → 当惹雍错 → 扎日南木措 → 措勤
7. **阿里腹地**：塔若措 → 扎布耶茶卡 → 昂拉仁措 → 亚热乡
8. **冈底斯山**：玛旁雍错 → 冈仁波齐 → 狮泉河
9. **终点**：暗夜星空公园 → **班公湖**

全程约 **6200公里**，预计 **20天** 行程。

## 技术栈

| 组件 | 技术 |
|------|------|
| 引擎 | Unity 2022.3+ |
| 地形 | Cesium for Unity |
| 影像 | Bing Maps / Cesium ion |
| VR | OpenXR / XR Interaction Toolkit |
| 数据 | JSON |

## 快速开始

1. 安装 [Unity 2022.3 LTS](https://unity.com/releases)
2. 安装 [Cesium for Unity](https://cesium.com/cesium-for-unity/)
3. 导入本项目至 Unity Hub
4. 运行 `Assets/Scenes/MainEarth.unity`

## 项目信息

- **项目代号**：DynastyVR Earth
- **所属**：东方皇朝元宇宙部
- **版本**：v1.0.0-alpha
- **数据版本**：2026-03-18

---

*🏛️ 东方皇朝 · 天皇国度 · DynastyVR*
