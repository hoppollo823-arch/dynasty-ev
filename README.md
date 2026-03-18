# 🌐 DynastyVR — 数字孪生宇宙

> 东方皇朝 EV 元宇宙部 · 陛下钦定愿景
> 
> **一个与真实世界完全一致的数字地球——你可以飞、可以开、可以住、可以看黑洞。**

## 智能体

| 代号 | 职责 |
|------|------|
| EV-president | 部门主管 |
| EV-asset-engineer | 资产工程 |
| EV-interaction-designer | 交互设计 |
| EV-network-engineer | 网络工程 |
| EV-performance-engineer | 性能工程 |
| EV-scene-designer | 场景设计 |

## 项目结构

```
dynasty-ev/
├── UnityProject/          # Unity 项目目录
│   ├── Assets/            # 资源文件 (脚本/场景/模型/材质)
│   ├── Packages/          # UPM 包依赖
│   └── ProjectSettings/   # Unity 项目设置
├── docs/                  # 项目文档
│   ├── PROJECT.md         # 📋 项目总体规划
│   ├── PROJECT_SETTINGS.md # ⚙️ Unity 项目配置指南
│   └── POC_PLAN.md        # 🔬 技术验证方案 (Cesium + HDRP + VR)
├── agents/                # 智能体配置
├── assets/                # 外部资产
├── config/                # 配置文件
├── networking/            # 网络层
├── scenes/                # 场景定义
└── src/                   # 源代码 (后端服务)
```

## 快速开始

详见 `UnityProject/README.md`。

## 技术栈

| 组件 | 技术选型 |
|------|----------|
| 引擎 | Unity 2022.3 LTS |
| 渲染管线 | HDRP 14.x |
| 地球引擎 | Cesium for Unity |
| VR 框架 | XR Interaction Toolkit |
| 3D 建筑 | Google Photorealistic 3D Tiles |
| 地形 | Copernicus DEM 30m |
| 路网 | OpenStreetMap |
| 星表 | Hipparcos/Tycho-2 |
| 黑洞数据 | EHT 公开影像 |

## 开发周期

约 40 周 (10个月)，详见 [PROJECT.md](docs/PROJECT.md)。

---

*东方皇朝 · 数字地球是舞台，真实是参考，自由是规则。*
