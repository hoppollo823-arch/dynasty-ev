# 🎮 DynastyVR — Unity Project

> 东方皇朝 EV 元宇宙部 · Unity 项目根目录

## 快速开始

### 环境要求
- Unity **2022.3 LTS** (最新补丁版本)
- Windows 10/11 64-bit (开发), macOS (可选)
- GPU: NVIDIA RTX 3080+ (推荐), GTX 1660+ (最低)
- RAM: 32GB (推荐), 16GB (最低)
- 磁盘: 100GB 可用空间 (含 Cesium 缓存)

### 安装步骤
1. 安装 Unity Hub → 安装 Unity 2022.3 LTS
2. 确保勾选: HDRP, Android Build Support, Documentation
3. Clone 此仓库
4. 用 Unity Hub 打开 `UnityProject/` 目录
5. 等待 Package Manager 解析依赖
6. Cesium Ion Token 配置 (见 `docs/POC_PLAN.md`)

### 项目结构
```
Assets/
├── Scripts/          # 源代码
│   ├── Core/         # 核心框架
│   ├── GeoEngine/    # Cesium 地球引擎
│   ├── RoadEngine/   # 路网与车辆
│   ├── CosmosEngine/ # 宇宙模块
│   ├── BuildSystem/  # UGC 建造
│   ├── VR/           # VR 交互
│   └── UI/           # 用户界面
├── Scenes/           # 场景文件
├── Prefabs/          # 预制体
├── Materials/        # 材质
├── Textures/         # 贴图
├── Models/           # 3D 模型
├── Shaders/          # 自定义 Shader
├── Audio/            # 音频资源
├── Resources/        # 运行时加载
├── StreamingAssets/  # Cesium 瓦片缓存
├── Editor/           # 编辑器扩展
└── Plugins/          # 第三方插件
```

### 开发规范
详见 `docs/PROJECT_SETTINGS.md`。

### Git LFS
此项目使用 Git LFS 管理大文件 (纹理、模型、音频)。
确保安装 Git LFS: `git lfs install`
