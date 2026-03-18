# 🔒 DynastyVR 安全技术方案

> 东方皇朝 EV 元宇宙部 · 安全工程组
>
> 版本：v1.0 | 日期：2026-03-18 | 作者：EV-security

---

## 目录

1. [账户安全](#1-账户安全)
2. [网络通信安全](#2-网络通信安全)
3. [反作弊系统](#3-反作弊系统)
4. [数据安全](#4-数据安全)
5. [UGC 审核](#5-ugc-审核)
6. [语音安全](#6-语音安全)
7. [支付安全](#7-支付安全)
8. [服务端安全](#8-服务端安全)
9. [应急预案](#9-应急预案)

---

## 1. 账户安全

### 1.1 OAuth 2.0 登录

DynastyVR 支持多平台统一登录，采用 OAuth 2.0 + OIDC 协议栈。

#### 支持的 IdP（身份提供方）

| 平台 | 协议 | 备注 |
|------|------|------|
| Apple Sign In | OIDC | iOS/macOS 首选 |
| Google | OIDC | Android/PC 首选 |
| 微信 | OAuth 2.0 | 国内用户 |
| QQ | OAuth 2.0 | 国内用户 |
| 手机号 | 自建 OTP | 无社交账号用户 |

#### 认证流程

```
┌─────────┐     ┌───────────┐     ┌─────────┐     ┌──────────────┐
│  Client  │────▶│ Auth GW   │────▶│  IdP     │────▶│ User Service │
│ (UE5)    │     │ (Nginx)   │     │(Google/  │     │ (Go)         │
│          │◀────│           │◀────│ WeChat)  │◀────│              │
└─────────┘     └───────────┘     └─────────┘     └──────────────┘
```

1. 客户端发起登录请求 → Auth Gateway
2. Auth GW 重定向至 IdP 授权页
3. 用户授权后，IdP 返回 `authorization_code`
4. Auth GW 用 `code` 换取 `id_token` + `refresh_token`
5. Auth GW 签发自有的 JWT（`access_token`：15min，`refresh_token`：30d）
6. JWT 内嵌 `sub`（用户ID）、`scope`、`platform`、`device_id`

#### Token 签发规范

```json
// access_token payload
{
  "sub": "usr_8f3a2b1c",
  "scope": "game:play ugc:create voice:join",
  "platform": "ios",
  "device_id": "dev_x9k2m",
  "iat": 1710700000,
  "exp": 1710700900,
  "iss": "dynasty-auth",
  "jti": "tok_unique_id"
}
```

- 签名算法：RS256，密钥轮换周期 90 天
- Token 传输：`Authorization: Bearer <token>`，严禁 URL 参数传递
- Refresh Token 使用 opaque 格式，存储于服务端黑名单表，支持主动吊销

### 1.2 双因素认证（2FA）

#### 适用场景

| 场景 | 强制程度 | 方式 |
|------|----------|------|
| 地块交易（金额 ≥ 1000 虚拟币） | 强制 | TOTP / SMS |
| 修改绑定手机/邮箱 | 强制 | TOTP / SMS |
| 管理员后台登录 | 强制 | TOTP + 硬件密钥 |
| 普通游戏登录 | 可选 | SMS OTP |
| UGC 审核后台 | 强制 | TOTP |

#### TOTP 实现

- 算法：RFC 6238（TOTP），HMAC-SHA1
- 密钥长度：160 bit
- 时间窗口：30 秒，允许 ±1 个窗口偏移
- 恢复码：8 组，加密存储（bcrypt 哈希）

#### SMS OTP

- 长度：6 位数字
- 有效期：5 分钟
- 频率限制：同一手机号 60 秒内最多 1 次，24 小时内最多 10 次
- 通道：阿里云短信 / Twilio（海外）

### 1.3 Session 管理

#### Session 架构

```
Client (UE5)
  ├── access_token (JWT, 15min)  → 无状态验证
  └── refresh_token (opaque, 30d) → Redis 持久存储
```

#### Session 安全策略

| 策略 | 实现 |
|------|------|
| 设备绑定 | Refresh Token 关联 `device_id`，换设备需重新认证 |
| 并发控制 | 同一账号最多 3 个活跃 Session，超出则踢出最早设备 |
| 闲置超时 | 游戏内 2 小时无操作自动断开，需重新验证 |
| 登录通知 | 新设备登录推送通知（App Push / 邮件） |
| 异常检测 | 短时间内多地登录触发风控（IP 地理跳跃 > 500km/小时） |
| 主动下线 | 用户可在任意设备远程踢出其他 Session |

#### Token 刷新机制

```
1. 客户端发现 access_token 过期（或收到 401）
2. 使用 refresh_token 调用 /auth/refresh
3. 服务端验证 refresh_token：
   - 是否在黑名单中（已吊销）
   - device_id 是否匹配
   - 是否过期
4. 验证通过 → 签发新 access_token + 新 refresh_token（轮替）
5. 旧 refresh_token 加入黑名单（单次使用，防重放）
```

---

## 2. 网络通信安全

### 2.1 TLS 1.3

#### 全链路加密

所有对外通信强制 TLS 1.3，不兼容 TLS 1.2 及以下版本。

```
┌──────────┐   TLS 1.3    ┌──────────┐   TLS 1.3    ┌──────────┐
│  Client   │─────────────▶│   CDN    │─────────────▶│  Backend │
│  (UE5)    │◀─────────────│ (Cloud-  │◀─────────────│  (K8s)   │
│           │              │  flare)  │              │          │
└──────────┘              └──────────┘              └──────────┘
```

#### TLS 配置

| 参数 | 值 |
|------|----|
| 最低版本 | TLS 1.3 |
| 密钥交换 | X25519（首选）/ P-256 |
| 密码套件 | `TLS_AES_256_GCM_SHA384` / `TLS_CHACHA20_POLY1305_SHA256` |
| 证书 | ECC P-256，自动续期（ACME/Let's Encrypt + Cloudflare） |
| HSTS | `max-age=31536000; includeSubDomains; preload` |
| OCSP Stapling | 启用 |

#### UE5 客户端配置

```cpp
// UE5 中配置 TLS
FHttpModule::Get().GetHttpManager().SetSSLVerifyMode(
    EHttpVerifyMode::VerifyPeer | EHttpVerifyMode::VerifyHost
);

// 自定义 SSL Certificate Bundle（用于内网测试）
FHttpModule::Get().SetCertificateBundlePath(
    FPaths::ProjectDir() / "Config/certs/dynasty-ca.pem"
);
```

### 2.2 gRPC 加密

服务间通信使用 gRPC over TLS 1.3。

#### Proto 安全定义

```protobuf
service GameService {
  // 客户端→服务端：玩家位置上报
  rpc ReportPosition(PositionReport) returns (PositionAck);
  
  // 客户端→服务端：建造物提交
  rpc SubmitBuilding(BuildingData) returns (SubmitResult);
  
  // 双向流：实时多人同步
  rpc PlayerSync(stream PlayerState) returns (stream WorldUpdate);
}
```

#### mTLS（双向认证）

服务间调用实施 mTLS：

| 配置项 | 值 |
|--------|----|
| 证书颁发 | 内部 CA（HashiCorp Vault PKI） |
| 证书有效期 | 30 天，自动续签 |
| CN 格式 | `svc-{service-name}.{namespace}.svc.cluster.local` |
| 轮换 | 无缝轮换（Envoy sidecar 自动处理） |

#### gRPC 拦截器

```go
// 服务端认证拦截器
func AuthInterceptor(ctx context.Context, req interface{}, 
    info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (interface{}, error) {
    
    // 从 metadata 提取 token
    md, ok := metadata.FromIncomingContext(ctx)
    if !ok {
        return nil, status.Error(codes.Unauthenticated, "missing metadata")
    }
    
    tokens := md.Get("authorization")
    if len(tokens) == 0 {
        return nil, status.Error(codes.Unauthenticated, "missing token")
    }
    
    // 验证 JWT
    claims, err := jwtService.Validate(tokens[0])
    if err != nil {
        return nil, status.Error(codes.Unauthenticated, "invalid token")
    }
    
    // 注入用户上下文
    ctx = context.WithValue(ctx, "user_id", claims.Subject)
    return handler(ctx, req)
}
```

### 2.3 WebSocket 安全

实时游戏同步使用 WSS（WebSocket Secure）。

#### 连接建立

```
1. 客户端发起 WSS 握手，附带 access_token
2. 服务端验证 JWT，校验 scope 包含 "game:play"
3. 服务端验证 device_id 与 Session 一致
4. 连接建立，绑定到对应 Room（房间/地图区块）
```

#### 安全措施

| 措施 | 说明 |
|------|------|
| 协议强制 WSS | 禁止 WS（明文），Nginx 层强制跳转 |
| 消息帧加密 | 应用层额外使用 AES-256-GCM 加密游戏状态（防中间人即使 TLS 被攻破） |
| 心跳检测 | 每 30 秒 Ping/Pong，超时 60 秒断开 |
| 消息大小限制 | 单帧最大 64KB，防内存炸弹 |
| 频率限制 | 每客户端 100 msg/s，超出则临时禁言 |
| 连接认证 | 每条消息携带 `msg_token`（短期 HMAC），防连接劫持 |
| 压缩 | permessage-deflate，减少带宽 |

#### 游戏消息加密协议

```cpp
// 应用层消息加密（额外安全层）
struct GameMessage {
    uint32_t seq;           // 序列号，防重放
    uint64_t timestamp;     // 时间戳，防重放（过期 > 5s 丢弃）
    uint8_t  iv[12];        // AES-GCM 初始化向量
    uint8_t  ciphertext[];  // 加密的游戏数据
    uint8_t  tag[16];       // AES-GCM 认证标签
};

// 密钥派生：每个 Session 独立密钥
// session_key = HKDF(master_key, session_id)
```

---

## 3. 反作弊系统

### 3.1 UE5 Anti-Cheat 集成

#### 架构

```
┌────────────────────────────────────────────────────┐
│                  UE5 Client                        │
│  ┌──────────────┐  ┌──────────────┐               │
│  │  Easy Anti-   │  │  定制安全    │               │
│  │  Cheat (EAC)  │  │  模块        │               │
│  │  - 内存保护   │  │  - 位置校验  │               │
│  │  - 进程监控   │  │  - 建造验证  │               │
│  │  - 文件完整性 │  │  - 输入加密  │               │
│  └──────────────┘  └──────────────┘               │
└──────────────────┬─────────────────────────────────┘
                   │ 加密通道
┌──────────────────▼─────────────────────────────────┐
│                  安全服务 (Go)                      │
│  ┌──────────────┐  ┌──────────────┐               │
│  │  检测引擎     │  │  处罚系统    │               │
│  │  - 统计分析   │  │  - 警告      │               │
│  │  - 行为模型   │  │  - 禁言      │               │
│  │  - 机器学习   │  │  - 封禁      │               │
│  └──────────────┘  └──────────────┘               │
└────────────────────────────────────────────────────┘
```

#### Easy Anti-Cheat (EAC) 配置

| 功能 | 配置 |
|------|------|
| 内存保护 | 防止读写游戏内存（位置、血量、物品） |
| 进程监控 | 检测注入器、调试器、内存修改器 |
| 文件完整性 | 验证游戏二进制和资源文件哈希 |
| 内核模式 | EAC 内核驱动，检测 Rootkit |
| 反调试 | 防止 IDA、x64dbg 等工具附加 |

#### UE5 安全模块集成

```cpp
// GameSession 安全初始化
void ADynastyGameSession::InitGameSecurity() {
    // 1. 启动 EAC
    IFacialAnimation* EACModule = 
        FModuleManager::Get().LoadModuleChecked<IFacialAnimation>("EasyAntiCheat");
    EACModule->InitializeServer();
    
    // 2. 注册安全回调
    AntiCheatService = GetAntiCheatService();
    AntiCheatService->OnCheatDetected.AddUObject(
        this, &ADynastyGameSession::OnCheatDetected);
    
    // 3. 启动心跳校验
    GetWorldTimerManager().SetTimer(
        IntegrityCheckTimer, this,
        &ADynastyGameSession::RunIntegrityCheck,
        30.0f, true);  // 每30秒一次
}
```

### 3.2 位置校验（Server-Authoritative）

**核心原则：服务端权威，客户端只上报意图。**

#### 位置同步协议

```
客户端 → 服务端：
{
  "type": "move_intent",
  "seq": 12345,
  "timestamp": 1710700000,
  "input": {
    "forward": 1.0,      // W 键输入值 [-1, 1]
    "right": 0.0,         // D 键输入值 [-1, 1]
    "jump": false
  },
  "view_angle": { "pitch": 15.2, "yaw": 230.5 }
}

服务端 → 客户端：
{
  "type": "position_update",
  "seq": 12345,
  "position": { "x": 100.5, "y": 200.3, "z": 50.1 },
  "velocity": { "x": 5.2, "y": 0.0, "z": -3.1 },
  "timestamp": 1710700001
}
```

#### 校验规则

| 检测项 | 阈值 | 处理 |
|--------|------|------|
| 移动速度 | ≤ 基准速度 × 1.2（含冲刺） | 超出则回滚位置 |
| 瞬移检测 | 单帧位移 > 50m | 标记审查 |
| 穿墙检测 | 路径上碰撞体密度 + 位移不匹配 | 回滚 + 计数 |
| 地下穿行 | Z 坐标 < 地形高度 | 拉回地面 |
| 高空飞行 | Z > 最大合法高度（非飞行器模式） | 拉回 + 警告 |
| 加速度异常 | Δv/Δt 超过物理引擎合理值 | 回滚 |

#### 服务端位置校验伪代码

```go
func (s *PlayerService) ValidatePosition(playerID string, newPos Position) error {
    lastPos := s.GetLastPosition(playerID)
    dt := time.Since(lastPos.Timestamp).Seconds()
    
    // 速度校验
    distance := lastPos.Position.DistanceTo(newPos)
    speed := distance / dt
    maxSpeed := s.GetMaxSpeed(playerID) * 1.2 // 含20%宽容度
    
    if speed > maxSpeed {
        s.logger.Warn("speed hack detected",
            "player", playerID,
            "speed", speed,
            "max_allowed", maxSpeed)
        
        // 回滚到上次合法位置
        s.RollbackPosition(playerID, lastPos)
        s.antiCheat.Flag(playerID, "speed_hack", 1)
        return ErrSpeedHack
    }
    
    // 地形校验
    terrainHeight := s.geoService.GetTerrainHeight(newPos.X, newPos.Y)
    if newPos.Z < terrainHeight - 2.0 { // 允许2m容差
        s.RollbackPosition(playerID, lastPos)
        return ErrUnderground
    }
    
    // 碰撞校验（简化版，完整版在物理模拟中）
    if s.CheckCollision(lastPos.Position, newPos) {
        s.RollbackPosition(playerID, s.FindNearestValidPosition(lastPos, newPos))
        return ErrCollision
    }
    
    // 合法，更新
    s.SetLastPosition(playerID, newPos)
    return nil
}
```

### 3.3 建造物验证

#### 验证层次

```
Layer 1: 格式验证（客户端本地）
  → 模型格式、尺寸范围、多边形数量上限

Layer 2: 服务端验证（提交时）
  → 位置合法性、地块归属、碰撞检测

Layer 3: 审核验证（异步）
  → AI 内容审核 + 人工抽检
```

#### 服务端建造物校验

```go
func (b *BuildService) ValidateBuilding(playerID string, building *BuildingData) error {
    // 1. 大小限制
    if building.Bounds.SizeX() > MAX_BUILDING_SIZE ||
       building.Bounds.SizeY() > MAX_BUILDING_SIZE ||
       building.Bounds.SizeZ() > MAX_BUILDING_HEIGHT {
        return ErrBuildingTooLarge
    }
    
    // 2. 多边形数量
    if building.PolygonCount > MAX_POLYGON_COUNT {
        return ErrTooManyPolygons
    }
    
    // 3. 地块归属
    plot := b.plotService.GetPlot(building.Position)
    if plot == nil || plot.OwnerID != playerID {
        return ErrNotPlotOwner
    }
    
    // 4. 位置在地块范围内
    if !plot.Contains(building.Bounds) {
        return ErrOutOfBounds
    }
    
    // 5. 与其他建造物碰撞检测
    if b.CheckBuildCollision(building) {
        return ErrBuildingCollision
    }
    
    // 6. 高度限制（不超出地块高度范围）
    if building.Bounds.MaxZ > plot.MaxHeight {
        return ErrExceedsHeightLimit
    }
    
    return nil
}
```

---

## 4. 数据安全

### 4.1 用户数据加密存储

#### 分层加密架构

```
┌─────────────────────────────────────────────┐
│            应用层加密                        │
│  用户聊天记录、建筑蓝图、个人资料            │
│  算法：AES-256-GCM                          │
│  密钥：per-user key（由 KDF 派生）           │
├─────────────────────────────────────────────┤
│            传输层加密                        │
│  TLS 1.3 / WSS / gRPC TLS                  │
├─────────────────────────────────────────────┤
│            存储层加密                        │
│  数据库透明加密（TDE）                       │
│  磁盘加密：LUKS / AWS EBS 加密              │
│  备份加密：AES-256-CBC + GPG                │
└─────────────────────────────────────────────┘
```

#### 密钥管理

| 密钥类型 | 存储方式 | 轮换周期 |
|----------|----------|----------|
| 数据库 TDE 密钥 | AWS KMS / Vault Transit | 180 天 |
| 用户数据加密密钥 | Vault（per-user envelope encryption） | 用户注销时销毁 |
| API 密钥 | Vault KV v2 | 90 天 |
| TLS 私钥 | Cloudflare / Vault PKI | 90 天（自动续期） |
| WebSocket HMAC 密钥 | Vault（共享密钥） | 30 天 |

#### 用户敏感数据清单

| 数据 | 加密级别 | 存储方式 |
|------|----------|----------|
| 密码/Token | 不存储明文 | bcrypt 哈希（cost=12） |
| 手机号 | 应用层加密 | AES-256-GCM，显示时脱敏（138****1234） |
| 邮箱 | 应用层加密 | AES-256-GCM |
| 支付信息 | PCI DSS 合规 | 第三方支付处理，不存储卡号 |
| 建筑蓝图 | 应用层加密 | AES-256-GCM + 签名 |
| 聊天记录 | 应用层加密 | AES-256-GCM，保留 90 天 |
| 语音录音 | 应用层加密 | AES-256-GCM，审核后 30 天删除 |
| 位置历史 | 存储层加密 | TDE，保留 30 天 |

### 4.2 隐私合规

#### GDPR 合规（欧盟用户）

| 要求 | 实现 |
|------|------|
| 同意管理 | 注册时明确勾选隐私协议，分项同意（基础/营销/分析） |
| 数据可携带 | 提供 API 导出用户所有数据（JSON/CSV） |
| 被遗忘权 | 30 天内彻底删除用户数据（含备份标记删除） |
| 数据最小化 | 只收集必要信息（账号、设备信息、支付信息） |
| 72h 报告 | 数据泄露 72 小时内通知监管机构 |
| DPO 任命 | 任命数据保护官，公开联系方式 |
| 跨境传输 | EU→中国传输使用 Standard Contractual Clauses (SCC) |

#### 个人信息保护法（中国）

| 要求 | 实现 |
|------|------|
| 知情同意 | 明确告知收集目的、方式、范围 |
| 单独同意 | 敏感信息（生物识别、位置、支付）需单独授权 |
| 最小必要 | 仅收集实现功能所必需的信息 |
| 本地化存储 | 中国用户数据存储于中国大陆节点（阿里云华东区） |
| 安全评估 | 定期进行个人信息安全影响评估（PIA） |
| 未成年人保护 | 未满 14 岁需监护人同意，限制社交功能 |

#### 用户隐私控制面板

```
游戏内 → 设置 → 隐私中心：
├── 数据管理
│   ├── 📥 导出我的数据
│   ├── 🗑️ 删除账户
│   └── 📋 查看数据收集清单
├── 权限管理
│   ├── 📍 位置服务（开/关）
│   ├── 🎤 语音聊天（开/关/仅好友）
│   ├── 📸 截图/录像（开/关）
│   └── 👥 在线状态（公开/好友/隐身）
├── 通知设置
│   ├── 登录提醒
│   ├── 设备新登录通知
│   └── 数据访问通知
└── Cookie/追踪
    ├── 分析数据收集（开/关）
    └── 个性化推荐（开/关）
```

---

## 5. UGC 审核

### 5.1 审核架构

```
用户提交建造物
      │
      ▼
┌─────────────┐
│ Layer 1     │  自动检测（客户端 + 服务端）
│ 格式/尺寸   │  100% 自动，< 1s
└──────┬──────┘
       │ 通过
       ▼
┌─────────────┐
│ Layer 2     │  AI 内容审核
│ 图像识别    │  100% 自动，< 30s
└──────┬──────┘
       │ 通过 / 疑似违规
       ▼
┌─────────────┐
│ Layer 3     │  人工审核
│ 专家团队    │  疑似违规 + 抽检（10%）
└──────┬──────┘
       │ 通过 / 驳回
       ▼
    上线发布
```

### 5.2 AI 审核系统

#### 审核维度

| 检测项 | 技术 | 说明 |
|--------|------|------|
| 色情内容 | 图像分类 CNN（NSFW 模型） | 裸露、性暗示 |
| 暴力内容 | 图像分类 + 场景识别 | 血腥、恐怖场景 |
| 政治敏感 | OCR + 文本分类 + 图像匹配 | 政治标语、敏感符号 |
| 侵权内容 | 图像哈希 + 特征匹配 | 仿冒品牌、盗用设计 |
| 垃圾广告 | 文本分类 + 图像文字识别 | 二维码、联系方式、引流 |
| 反人类建筑 | 3D 模型分析 | 禁忌符号、侮辱性造型 |

#### AI 审核流水线

```python
# UGC AI 审核核心流程
class UGCAuditPipeline:
    def __init__(self):
        self.render_engine = BuildingRenderEngine()  # 3D→2D 渲染
        self.nsfw_model = load_model("nsfw_resnet50")
        self.political_model = load_model("political_detector")
        self.text_ocr = PaddleOCR()
        self.hash_matcher = PerceptualHashMatcher()
    
    def audit(self, building_data: BuildingData) -> AuditResult:
        # 1. 渲染建筑多角度截图
        screenshots = self.render_engine.render(building_data, angles=8)
        
        results = []
        for img in screenshots:
            # 2. NSFW 检测
            nsfw_score = self.nsfw_model.predict(img)
            if nsfw_score > 0.85:
                return AuditResult(rejected=True, reason="NSFW_CONTENT")
            
            # 3. 政治敏感检测
            political_score = self.political_model.predict(img)
            if political_score > 0.80:
                return AuditResult(rejected=True, reason="POLITICAL_SENSITIVE",
                                   manual_review=True)
            
            # 4. 文字 OCR 检测
            texts = self.text_ocr.detect(img)
            for text in texts:
                if self.contains_sensitive_words(text):
                    return AuditResult(rejected=True, reason="SENSITIVE_TEXT",
                                       manual_review=True)
            
            # 5. 侵权哈希匹配
            matches = self.hash_matcher.match(img)
            if matches:
                return AuditResult(rejected=True, reason="COPYRIGHT_INFRINGEMENT",
                                   manual_review=True)
            
            results.append({
                "nsfw": nsfw_score,
                "political": political_score,
                "texts": texts
            })
        
        # 综合评分
        max_nsfw = max(r["nsfw"] for r in results)
        max_political = max(r["political"] for r in results)
        
        if max_nsfw > 0.6 or max_political > 0.6:
            return AuditResult(rejected=False, manual_review=True,
                               scores={"nsfw": max_nsfw, "political": max_political})
        
        return AuditResult(rejected=False, manual_review=False)
```

### 5.3 人工审核流程

#### 审核团队配置

| 角色 | 人数 | 职责 |
|------|------|------|
| 初审员 | 10 人/班 | 处理 AI 标记的疑似内容 |
| 复审员 | 3 人/班 | 争议内容、申诉处理 |
| 审核主管 | 1 人/班 | 政策解释、疑难案例 |
| 夜间值班 | 3 人 | 紧急事件 + 常规抽检 |

#### 审核工具

```
审核后台 (Web App)：
├── 待审队列
│   ├── AI 标记（优先）
│   ├── 随机抽检（10% 新发布）
│   └── 用户举报
├── 审核操作
│   ├── ✅ 通过
│   ├── ❌ 驳回（选择原因）
│   ├── ⚠️ 驳回 + 处罚
│   └── 📝 标记为复审
├── 审核依据
│   ├── 审核标准手册（可搜索）
│   ├── 历史判例参考
│   └── 政策更新通知
└── 数据看板
    ├── 审核效率（件/小时）
    ├── 误判率统计
    └── 趋势分析
```

### 5.4 违规处理流程

```
违规发现
    │
    ├── 初犯（轻度）
    │   └── 建造物下架 + 站内信警告 + 记录
    │
    ├── 二次违规
    │   └── 建造物下架 + 禁止建造 3 天 + 记录
    │
    ├── 三次违规
    │   └── 建造物下架 + 禁止建造 30 天 + 公共信用 -50
    │
    ├── 严重违规（色情/暴力/仇恨）
    │   └── 建造物下架 + 禁止建造 90 天 + 公共信用 -200
    │
    └── 极端违规（违法内容）
        └── 账号永久封禁 + 上报执法机构
        
申诉渠道：
  → 7 天内提交申诉 → 复审员 48h 内处理
  → 申诉成功：撤销处罚 + 信用恢复
  → 申诉失败：维持原判
```

---

## 6. 语音安全

### 6.1 语音聊天架构

```
┌──────────┐   WebRTC    ┌──────────┐   WebRTC    ┌──────────┐
│ Player A │─────────────▶│  SFU     │◀─────────────│ Player B │
│          │◀─────────────│ (语音    │─────────────▶│          │
│          │              │  服务器) │◀─────────────│          │
└──────────┘              └──────────┘─────────────└──────────┘
                                │
                                │ 音频流副本
                                ▼
                          ┌──────────┐
                          │ 语音审核 │
                          │ (实时+   │
                          │  异步)   │
                          └──────────┘
```

### 6.2 实时语音审核

#### 关键词检测（实时）

```python
# 实时语音流关键词检测
class VoiceSafetyMonitor:
    def __init__(self):
        self.asr_engine = FasterWhisper(model="large-v3")
        self.keyword_filter = KeywordFilter(
            categories=["仇恨言论", "极端主义", "威胁恐吓", "色情骚扰"]
        )
        self.toxicity_model = load_model("toxicity-classifier")
    
    def process_audio_stream(self, audio_chunk: bytes, player_id: str):
        # 1. 语音转文字（流式）
        text_segments = self.asr_engine.transcribe_stream(audio_chunk)
        
        for segment in text_segments:
            # 2. 关键词匹配
            if self.keyword_filter.match(segment.text):
                self.alert_mod(player_id, segment.text, "KEYWORD_MATCH")
                self.mute_player(player_id, duration=300) // 临时禁言 5 分钟
                return
            
            # 3. 语气/情绪检测
            toxicity_score = self.toxicity_model.predict(segment.text)
            if toxicity_score > 0.85:
                self.alert_mod(player_id, segment.text, "TOXIC_SPEECH")
                self.mute_player(player_id, duration=60)
                return
```

#### 语音质量监控

| 检测项 | 说明 | 处理 |
|--------|------|------|
| AI 合成语音 | 检测是否为 TTS/AI 变声 | 标记 + 人工审核 |
| 音量超标 | 持续爆音攻击 | 自动压限 + 警告 |
| 背景噪声 | 播放违规音频内容 | 标记 + 可能禁言 |
| 多人语音 | 识别群体语言攻击 | 识别主导者处理 |

### 6.3 举报系统

#### 举报入口

- **游戏内**：玩家列表 → 右键 → 举报（支持多选原因 + 录音附件）
- **语音中**：长按举报键（默认 F12）→ 自动截取最近 30 秒语音

#### 举报处理

```
用户提交举报
    │
    ├─ 语音举报 → 附带 30s 音频片段
    ├─ 文字举报 → 附带聊天记录
    └─ 行为举报 → 附带录像回放
    
    ▼
┌─────────────┐
│ 举报分级    │
│ AI 初筛     │
└──────┬──────┘
       │
  ┌────┴────┐
  │         │
  高优先级   低优先级
  (威胁/     (轻微骚扰)
   恐怖/    
   儿童)    
  │         │
  ▼         ▼
 1h 内人工   24h 内人工
 处理        处理
  │         │
  ▼         ▼
处罚 + 通知举报人结果
```

#### 举报保护

- **反举报滥用**：恶意举报（连续 3 次不成立）→ 信用分降低
- **匿名性**：被举报者不知道举报人身份
- **反馈闭环**：处理完成后通知举报人（仅告知"已处理"/"未发现违规"，不含细节）

---

## 7. 支付安全

### 7.1 虚拟交易安全

#### 支付架构

```
┌──────────┐      ┌──────────────┐      ┌──────────────┐
│  Client   │─────▶│  Payment GW  │─────▶│  第三方支付  │
│  (UE5)    │      │  (隔离区)    │      │  支付宝/微信 │
│           │◀─────│              │◀─────│  /Apple Pay  │
└──────────┘      └──────────────┘      └──────────────┘
                         │
                         ▼
                  ┌──────────────┐
                  │  Order DB    │
                  │  (独立实例)  │
                  └──────────────┘
```

#### 支付安全措施

| 措施 | 说明 |
|------|------|
| 支付网关隔离 | Payment GW 独立部署，与游戏服务网络隔离 |
| PCI DSS 合规 | 不存储卡号，第三方处理敏感支付数据 |
| 订单签名 | 每笔订单 HMAC-SHA256 签名，防篡改 |
| 幂等性 | 同一订单号不重复扣款（Redis + DB 原子操作） |
| 对账机制 | 每日与第三方支付对账，差异报警 |
| 大额验证 | ≥ 1000 虚拟币交易触发 2FA |
| 异常检测 | 短时间多笔交易、异地交易触发风控 |

#### 订单处理流程

```go
func (p *PaymentService) ProcessPurchase(req *PurchaseRequest) (*Order, error) {
    // 1. 验证用户身份
    claims := p.authValidate(req.AccessToken)
    
    // 2. 幂等性检查
    existingOrder := p.db.GetOrderByID(req.OrderID)
    if existingOrder != nil {
        return existingOrder, nil // 已处理，直接返回
    }
    
    // 3. 价格校验（防客户端篡改价格）
    item := p.catalogService.GetItem(req.ItemID)
    if item.Price != req.ExpectedPrice {
        return nil, ErrPriceMismatch // 价格被篡改
    }
    
    // 4. 库存检查
    if !p.inventoryService.CheckStock(req.ItemID, req.Quantity) {
        return nil, ErrOutOfStock
    }
    
    // 5. 生成订单（签名）
    order := &Order{
        ID:        req.OrderID,
        UserID:    claims.Subject,
        ItemID:    req.ItemID,
        Quantity:  req.Quantity,
        Price:     item.Price,
        Signature: p.signOrder(req.OrderID, item.Price),
        Status:    OrderPending,
        CreatedAt: time.Now(),
    }
    
    // 6. 调用第三方支付
    payURL, err := p.thirdParty.CreatePayment(order)
    if err != nil {
        return nil, err
    }
    
    // 7. 存储订单
    p.db.CreateOrder(order)
    
    return order, nil
}

// 支付回调验证
func (p *PaymentService) HandleCallback(callback *PaymentCallback) error {
    // 1. 验证签名
    if !p.verifyCallback(callback) {
        return ErrInvalidSignature
    }
    
    // 2. 查找订单
    order := p.db.GetOrderByID(callback.OrderID)
    if order == nil {
        return ErrOrderNotFound
    }
    
    // 3. 幂等性（已处理则跳过）
    if order.Status != OrderPending {
        return nil
    }
    
    // 4. 金额校验
    if callback.Amount != order.Price {
        p.alertFinanceTeam("金额不一致", order, callback)
        return ErrAmountMismatch
    }
    
    // 5. 确认订单
    p.db.UpdateOrderStatus(order.ID, OrderPaid)
    
    // 6. 发放虚拟物品（事务）
    p.inventoryService.GrantItem(order.UserID, order.ItemID, order.Quantity)
    
    return nil
}
```

### 7.2 地块购买流程

```
购买流程：
1. 用户浏览地块 → 查看价格、位置、大小
2. 确认购买 → 生成订单（服务端签名价格）
3. 2FA 验证（≥ 1000 虚拟币）
4. 跳转支付（支付宝/微信/Apple Pay）
5. 支付回调 → 服务端校验
6. 地块产权转移（区块链 / DB 原子操作）
7. 用户收到确认通知 + 地契 NFT（可选）

安全要点：
- 地块价格由服务端控制，客户端无法修改
- 产权转移为原子操作，防止"一地多卖"
- 购买记录永久保存（审计日志）
- 7 天冷静期：购买后 7 天内可申请退款（扣除 10% 手续费）
```

---

## 8. 服务端安全

### 8.1 DDoS 防护

#### 防护层次

```
Layer 1: 网络层（Cloudflare / 阿里云高防）
  → 清洗大流量攻击（SYN Flood、UDP Flood）
  → 全球 Anycast 分流
  → 吸收能力：Tbps 级别

Layer 2: 传输层（Nginx + ModSecurity）
  → 限制连接速率
  → 慢连接攻击防护（Slowloris）
  → 连接数限制

Layer 3: 应用层（自研 WAF）
  → API 频率限制
  → 请求特征分析
  → 异常行为检测
```

#### Nginx DDoS 防护配置

```nginx
# /etc/nginx/nginx.conf

# 连接限制
limit_conn_zone $binary_remote_addr zone=perip:10m;
limit_conn perip 100;

# 请求速率限制
limit_req_zone $binary_remote_addr zone=api:10m rate=30r/s;
limit_req zone=api burst=50 nodelay;

# 游戏 WebSocket（更高限制）
limit_req_zone $binary_remote_addr zone=game:10m rate=200r/s;
limit_req zone=game burst=500 nodelay;

# 连接超时（防慢连接）
client_header_timeout 10s;
client_body_timeout 10s;
send_timeout 10s;

# 请求体大小限制
client_max_body_size 10m;

# 日志异常检测
log_format ddos_detect '$remote_addr - $request_uri '
                       '$request_time $body_bytes_sent '
                       '$upstream_response_time';

# 防爬虫 User-Agent 白名单
if ($http_user_agent ~* "(bot|crawler|spider)") {
    return 403;
}
```

### 8.2 API 限流

#### 限流策略

| API 类型 | 限制 | 突发 | 说明 |
|----------|------|------|------|
| 登录 | 5/min | 10 | 防暴力破解 |
| 注册 | 3/hour | 5 | 防机器人注册 |
| 游戏操作 | 100/s | 200 | 正常游戏操作 |
| 建造物提交 | 10/min | 20 | 防刷建造物 |
| 举报 | 30/min | 50 | 正常举报频率 |
| 聊天消息 | 20/s | 40 | 防刷屏 |
| 支付相关 | 5/min | 10 | 防重复支付 |
| 数据导出 | 1/hour | 2 | 防数据爬取 |

#### 分布式限流（Redis + Lua）

```lua
-- rate_limit.lua
-- 令牌桶算法实现
local key = KEYS[1]
local rate = tonumber(ARGV[1])  -- 令牌产生速率（个/秒）
local burst = tonumber(ARGV[2]) -- 桶容量
local now = tonumber(ARGV[3])   -- 当前时间戳

local data = redis.call("HMGET", key, "tokens", "last_refill")
local tokens = tonumber(data[1]) or burst
local last_refill = tonumber(data[2]) or now

-- 计算应产生的令牌
local elapsed = now - last_refill
local new_tokens = math.min(burst, tokens + elapsed * rate)

if new_tokens >= 1 then
    -- 消耗一个令牌
    new_tokens = new_tokens - 1
    redis.call("HMSET", key, "tokens", new_tokens, "last_refill", now)
    redis.call("EXPIRE", key, math.ceil(burst / rate) + 60)
    return 1  -- 允许
else
    redis.call("HMSET", key, "tokens", new_tokens, "last_refill", now)
    redis.call("EXPIRE", key, math.ceil(burst / rate) + 60)
    return 0  -- 拒绝
end
```

### 8.3 SQL 注入防护

#### 防护措施

| 层次 | 措施 |
|------|------|
| ORM 层 | 使用 parameterized queries（GORM auto-escape） |
| 输入校验 | 白名单校验 + 类型强制转换 |
| WAF | SQL 注入特征规则（ModSecurity CRS） |
| 数据库权限 | 最小权限原则，禁止 `GRANT ALL` |
| 审计日志 | 记录所有 SQL 执行（慢查询 + 异常查询） |

#### 安全查询示例

```go
// ✅ 正确：使用 ORM 参数化查询
func (u *UserRepo) GetUserByID(id string) (*User, error) {
    var user User
    result := u.db.Where("id = ?", id).First(&user)
    return &user, result.Error
}

// ✅ 正确：使用预编译语句
func (u *UserRepo) SearchUsers(keyword string) ([]User, error) {
    var users []User
    result := u.db.Where("username LIKE ?", "%"+keyword+"%").Find(&users)
    return users, result.Error
}

// ❌ 错误：字符串拼接（绝不允许）
func (u *UserRepo) GetUserUnsafe(id string) (*User, error) {
    var user User
    // SQL 注入风险！
    result := u.db.Raw("SELECT * FROM users WHERE id = '" + id + "'").Scan(&user)
    return &user, result.Error
}

// 输入校验层
func validateUserID(id string) error {
    // 白名单：只允许字母数字和短横线
    matched, _ := regexp.MatchString(`^[a-zA-Z0-9_-]{1,64}$`, id)
    if !matched {
        return fmt.Errorf("invalid user ID format")
    }
    return nil
}
```

#### 数据库安全配置

| 配置项 | 值 |
|--------|----|
| 数据库版本 | PostgreSQL 16 |
| 连接加密 | SSL 强制（`sslmode=verify-full`） |
| 密码策略 | bcrypt 哈希，≥ 12 rounds |
| 慢查询日志 | > 100ms 记录 |
| 连接池 | max_connections=100，idle_timeout=300s |
| 备份 | 每日全量 + 每小时增量，加密存储 |
| 防火墙 | 数据库仅允许应用服务器 IP 访问 |

### 8.4 安全审计

```
日志收集 (ELK Stack)：
├── Nginx 访问日志 → 异常请求检测
├── 应用日志 → 业务异常 + 安全事件
├── 数据库审计日志 → SQL 注入检测
├── 安全模块日志 → 反作弊 + 举报
└── 支付日志 → 交易异常

告警规则：
├── 5 分钟内登录失败 > 10 次 → 账号锁定 + 告警
├── API 错误率 > 5% → 服务异常告警
├── 支付异常 → 即时告警（钉钉/短信）
├── DDoS 检测 → 自动切换高防 IP
└── 数据库异常查询 → 安全团队通知
```

---

## 9. 应急预案

### 9.1 数据泄露响应

#### 响应流程

```
发现泄露
    │
    ▼
┌─────────────────────────────┐
│ 0. 评估阶段（0-15 分钟）    │
│ - 确认泄露范围和类型        │
│ - 通知安全负责人            │
│ - 启动应急响应团队          │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 1. 遏制阶段（15-60 分钟）   │
│ - 隔离受影响系统            │
│ - 吊销泄露的凭证/Token      │
│ - 保留取证数据              │
│ - 切断攻击者访问            │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 2. 通知阶段（1-4 小时）     │
│ - 内部：管理层 + 法务       │
│ - 外部：用户通知（如影响）  │
│ - 监管：72h 内报 GDPR/个保法│
│ - 执法：如涉及犯罪          │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 3. 恢复阶段（4-48 小时）    │
│ - 修复漏洞                  │
│ - 重置受影响用户的凭据      │
│ - 恢复服务                  │
│ - 加强监控                  │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│ 4. 总结阶段（1-2 周）       │
│ - 事件调查报告              │
│ - 根因分析                  │
│ - 改进措施                  │
│ - 预案更新                  │
└─────────────────────────────┘
```

#### 泄露类型及处理

| 泄露类型 | 优先级 | 处理措施 |
|----------|--------|----------|
| 用户密码哈希 | 高 | 强制所有用户重置密码，检查是否被破解 |
| 个人信息（手机/邮箱） | 高 | 通知用户，提供信用监控建议 |
| 支付信息 | 紧急 | 冻结账户，联系支付机构，通知银行 |
| 游戏数据 | 中 | 评估影响范围，必要时回档 |
| 源代码 | 高 | 评估泄露代码中的密钥/配置，轮换所有密钥 |

### 9.2 账号盗用处理

#### 检测机制

```go
// 账号异常登录检测
func (a *AccountSecurity) DetectAccountTakeover(userID string, loginInfo *LoginInfo) *Alert {
    alerts := []Alert{}
    
    // 1. 地理跳跃检测
    lastLogin := a.getLastLogin(userID)
    if lastLogin != nil {
        distance := geo.Distance(lastLogin.IP, loginInfo.IP)
        hours := time.Since(lastLogin.Time).Hours()
        speed := distance / hours
        
        if speed > 900 { // km/h（超过飞机速度）
            alerts = append(alerts, Alert{
                Level:    "HIGH",
                Reason:   "GEO_LEAP",
                Detail:   fmt.Sprintf("Geographic leap: %.0f km in %.1f h", distance, hours),
            })
        }
    }
    
    // 2. 新设备检测
    if !a.isKnownDevice(userID, loginInfo.DeviceID) {
        alerts = append(alerts, Alert{
            Level:    "MEDIUM",
            Reason:   "NEW_DEVICE",
            Detail:   fmt.Sprintf("New device: %s", loginInfo.DeviceName),
        })
    }
    
    // 3. 异常时间检测
    if a.isAbnormalTime(userID, loginInfo.Time) {
        alerts = append(alerts, Alert{
            Level:    "LOW",
            Reason:   "ABNORMAL_TIME",
            Detail:   "Login at unusual hour",
        })
    }
    
    // 4. IP 风险检测
    if a.isRiskyIP(loginInfo.IP) {
        alerts = append(alerts, Alert{
            Level:    "HIGH",
            Reason:   "RISKY_IP",
            Detail:   "Login from known proxy/VPN/datacenter",
        })
    }
    
    // 综合评级
    if len(alerts) >= 2 || hasHighAlert(alerts) {
        // 触发额外验证
        a.triggerStepUpAuth(userID)
        a.notifyUser(userID, "UNUSUAL_LOGIN")
    }
    
    return &Alert{UserID: userID, Alerts: alerts}
}
```

#### 盗用处理流程

```
用户报告账号被盗
    │
    ▼
┌─────────────────────────┐
│ 立即锁定账号            │
│ - 冻结所有交易          │
│ - 吊销所有活跃 Session  │
│ - 禁止登录              │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ 身份验证                │
│ - 手机号验证码          │
│ - 邮箱验证              │
│ - 2FA 验证              │
│ - 如均失效 → 人工审核   │
│   （需提供注册信息证明） │
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ 账号恢复                │
│ - 重置密码              │
│ - 重新绑定手机/邮箱     │
│ - 重新设置 2FA          │
│ - 检查并恢复被盗虚拟资产│
└──────────┬──────────────┘
           │
           ▼
┌─────────────────────────┐
│ 安全加固                │
│ - 检查关联设备          │
│ - 审计近期操作          │
│ - 恢复异常交易          │
│ - 安全建议推送          │
└─────────────────────────┘
```

### 9.3 应急响应团队

| 角色 | 职责 | 联系方式（示例） |
|------|------|------------------|
| 安全负责人 | 整体指挥，对外沟通 | 安全组群 |
| 运维工程师 | 系统隔离、恢复 | 运维组群 |
| 后端开发 | 漏洞修复、数据修复 | 开发组群 |
| 法务顾问 | 合规报告、法律应对 | 法务组群 |
| PR 负责人 | 用户沟通、媒体应对 | PR 组群 |

### 9.4 应急联系方式

```
内部通讯：飞书安全应急群
外部报告：security@dynasty-vr.com
漏洞赏金：https://dynasty-vr.com/security/bug-bounty
监管报告：合规法务部 → 国家互联网应急中心 (CNCERT)
```

---

## 附录

### A. 安全检查清单

#### 部署前

- [ ] 所有 API 启用 TLS 1.3
- [ ] 数据库启用 TDE
- [ ] 防火墙规则已配置
- [ ] 限流规则已启用
- [ ] 审核系统已上线
- [ ] 应急预案已演练
- [ ] 日志收集已配置
- [ ] 告警规则已测试

#### 日常

- [ ] 每日安全日志审查
- [ ] 每周漏洞扫描
- [ ] 每月渗透测试
- [ ] 每季度安全审计
- [ ] 密钥轮换（按周期）
- [ ] 依赖漏洞更新

### B. 安全工具链

| 工具 | 用途 |
|------|------|
| Easy Anti-Cheat | 客户端反作弊 |
| HashiCorp Vault | 密钥管理 |
| Cloudflare / 阿里云高防 | DDoS 防护 |
| ModSecurity | WAF |
| ELK Stack | 日志分析 |
| Trivy | 容器漏洞扫描 |
| SonarQube | 代码安全扫描 |
| OWASP ZAP | 自动化安全测试 |

### C. 相关法规

| 法规 | 适用范围 |
|------|----------|
| GDPR（欧盟通用数据保护条例） | 欧盟用户数据 |
| 《中华人民共和国个人信息保护法》 | 中国用户数据 |
| 《中华人民共和国数据安全法》 | 数据出境、分类分级 |
| 《中华人民共和国网络安全法》 | 网络安全等级保护 |
| 《未成年人网络保护条例》 | 未成年用户保护 |
| PCI DSS | 支付卡数据安全 |

---

> **文档维护**：本安全方案应至少每季度审查更新一次。
>
> **变更记录**：
>
> | 日期 | 版本 | 变更内容 | 作者 |
> |------|------|----------|------|
> | 2026-03-18 | v1.0 | 初始版本 | EV-security |
