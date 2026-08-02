# Desktop-Up 技术架构 V0.1

状态：`PROPOSED`

日期：2026-08-02

## 1. 技术栈

| 领域 | 选择 | 理由 |
| --- | --- | --- |
| 桌面壳与后端 | Rust + Tauri 2 | 便于 Win32 集成，复用系统 WebView2，控制安装体积 |
| 前端 | Svelte + TypeScript | 组件轻、适合参数编辑和动态布局 |
| Windows API | `windows-rs` | 类型化调用 User32、Shell、DWM、WTS 和电源事件 |
| 桌面渲染 | 单一透明 WebView2 宿主 | 多海报共用渲染环境，避免每海报一个窗口/进程 |
| 数据 | SQLite + 版本化迁移 | 支持事务、恢复账本、升级和查询 |
| 主题 | HTML/CSS/SVG/Canvas + 受限脚本 | 易于定制，同时划分安全权限 |
| 打包 | Tauri bundler + NSIS 定制 | 生成单文件安装器和明确的卸载器 |

## 2. 进程与组件

```text
DesktopUp.exe
├─ App Core (Rust)
│  ├─ Collection Service
│  ├─ Shortcut Vault and Restore Ledger
│  ├─ Layout Service
│  ├─ Theme Package Validator
│  ├─ Runtime Policy / Pause State Machine
│  ├─ Tray and Quick Access Coordinator
│  ├─ AI Provider / Tool-call Validator (optional)
│  ├─ Windows Shell Adapter
│  └─ Persistence / Migration
├─ Desktop Host (single WebView2)
├─ Control Center (on-demand WebView2)
└─ Tray and Single-instance Coordinator

uninstall.exe
└─ Restore → unregister → remove → self-clean
```

主程序保持单实例。设置中心关闭后释放其窗口；桌面宿主保留一个实例，不按海报创建独立 WebView。

### 2.1 运行表面边界

权威生命周期见 [`RUNTIME_SURFACE_MODEL_V0.1.md`](RUNTIME_SURFACE_MODEL_V0.1.md)。技术实现必须维持以下边界：

- `Desktop Host` 是普通启动后唯一默认可见的产品表面；
- `Control Center`、设置和 Agent 按需创建或显示，不参与桌面宿主挂载；
- 控制中心中的桌面画布是预览消费者，桌面宿主是已提交状态的运行消费者；
- 两者共用 `DesktopCommand`、持久状态和事件通道，但不共用窗口焦点、装饰或显隐生命周期；
- 首选单显示器共享一个宿主并渲染多张海报，不为每张海报常驻独立 WebView2；
- 即使控制中心从未创建，托盘、桌面宿主、暂停状态机和恢复协调器仍必须完整工作。

## 3. 桌面宿主策略

### 3.1 首选模式

通过独立的 Windows 适配层把无边框、无激活、工具型窗口放置于桌面层。海报区域参与命中测试，透明空白返回鼠标穿透。

### 3.2 兼容性边界

Windows 没有为交互式第三方桌面组件提供完整稳定的公开挂载协议。WorkerW/Progman 行为只能放在单独适配器中，不得扩散到业务层。

必须提供回退模式：

- 桌面挂载失败时使用普通 bottom-most 工具窗口；
- Explorer 重启时重新发现桌面窗口并挂载；
- 连续失败时停止重试并展示恢复提示；
- 不注入 Explorer，不读取/写入 Explorer 内存；
- 不把原生桌面 ListView 坐标作为权威数据。

## 4. 暂停状态机

```text
ACTIVE_STATIC ──主题有动画──> ACTIVE_ANIMATED
      │                         │
      └────遮挡/锁屏/断电───────┤
                                v
                              PAUSED
                                │
                  长时间或用户策略=停止
                                v
                              STOPPED
```

触发源包括前台窗口事件、会话锁定、电源/显示器状态、虚拟桌面和显示器变化。事件合并后更新状态，不使用高频前台窗口轮询。

`PAUSED` 停止 CSS 动画、Canvas 循环、定时器和非必要数据推送；`STOPPED` 进一步卸载可选代码主题与大资源。

## 5. 核心数据模型

### CollectionItem

```text
id, kind, display_name, source_kind, source_identity,
launch_target, launch_arguments, working_directory,
icon_source, tags, managed_shortcut_id, created_at, updated_at
```

`kind` 首版为 `game`、`application`、`shortcut`，以后可增加 `folder`、`document` 或插件定义类型。

### ManagedShortcut

```text
id, original_path, vault_path, content_fingerprint,
move_state, restore_state, last_error, timestamps
```

这是安装、运行和卸载共同使用的恢复账本。移动文件和更新账本必须采用可恢复的两阶段操作。

### Poster

```text
id, title, monitor_identity, anchor, normalized_rect,
collapsed_state, layout_kind, theme_instance_id, z_group
```

### PosterItem

```text
poster_id, item_id, order_key, pinned, page, local_position
```

### ThemeInstance

```text
id, package_id, package_version, parameter_json,
trust_level, runtime_policy, asset_cache_version
```

## 6. 快捷方式处理

本节的实现顺序、事务状态和阶段门以 [`快捷方式导入与安全收纳模型 V0.1`](SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md) 为准；A0 不启用真实桌面扫描或移动。

- 使用 Shell COM API 解析 `.lnk`，保留参数、工作目录和图标位置；
- `.url` 使用受限协议白名单；
- `.exe` 只作为启动目标登记，不复制程序；
- 启动时通过 Windows Shell 执行原快捷方式语义；
- 导入扫描在后台、可取消并有数量上限；
- 收纳只移动快捷方式文件，永不移动目标游戏目录。

## 7. 布局引擎

布局输入是容器尺寸、项目尺寸、间距、固定项目和排序规则；输出为确定性位置列表。相同输入必须得到相同输出，便于恢复、撤销和测试。

第一版实现网格；书架和错位卡片建立接口但不阻塞 MVP。调整窗口大小时：固定项不动，未固定项局部重排，越界项进入下一页。

## 8. 主题运行时与安全

主题包先解压到隔离临时目录，完成以下检查后再原子安装：

- 清单/schema 版本；
- 路径穿越、符号链接和压缩炸弹防护；
- 文件类型、单文件大小和包总大小限制；
- CSS 外部 URL 与网络请求拦截；
- 字体、图片和 SVG 安全处理；
- 代码主题的显式信任记录。

代码主题在与主 UI 隔离的执行环境运行，只能接收冻结后的主题数据和绘制/动画 API。不得暴露通用 Tauri `invoke`。宿主必须能限帧、暂停和终止失去响应的主题。

## 9. 数据目录

建议按用户保存：

```text
%LOCALAPPDATA%\DesktopUp\
├─ data\desktop-up.db
├─ vault\shortcuts\
├─ themes\
├─ cache\
├─ backups\
└─ logs\
```

数据库与恢复账本属于关键数据；缓存可随时重建。升级前生成小型迁移备份，最多保留有限版本。API Key 不进入该目录，必须保存到 Windows Credential Manager。

## 10. 安装和卸载架构

安装器暂定输出 `DesktopUpSetup.exe`，按用户安装到：

```text
%LOCALAPPDATA%\Programs\DesktopUp\
```

安装器职责：校验系统版本与 WebView2、复制原子化文件、注册卸载项、创建开始菜单入口，并由用户选择是否开机自启。

`uninstall.exe` 是安装目录中的独立清理入口。它不能依赖正在卸载的主进程完成恢复。卸载状态机：

```text
Acquire lock
  → stop DesktopUp.exe
  → load and verify restore ledger
  → restore managed shortcuts
  → unregister autostart/protocol/uninstall entry
  → remove app-owned data selected by policy
  → remove binaries
  → self-delete
```

恢复未完全成功时，程序数据删除必须降级为“保留恢复保险库”，并向用户展示路径。完整规则见 [`RELEASE_AND_UNINSTALL_POLICY.md`](../../Rules/RELEASE_AND_UNINSTALL_POLICY.md)。

## 11. 测试层

- Rust 单元测试：路径、账本、布局、迁移和暂停状态机；
- 前端组件测试：海报状态、参数编辑和命中区域；
- Windows 集成测试：Shell Link、窗口挂载、Explorer 重启、自启；
- 端到端测试：导入 → 收纳 → 启动 → 恢复 → 卸载；
- 净机测试：Windows 11 x64、100/125/150/200% DPI、单屏/双屏；
- 故障注入：移动中断电、数据库损坏、目标重名、Explorer 崩溃、主题卡死。

## 12. 待 A0 验证后冻结的决策

- Tauri WebView 窗口在桌面层的最终挂载与回退实现；
- 单大窗口和每显示器一窗口的资源/命中测试对比；
- NSIS 定制能否完全满足卸载恢复顺序；必要时使用独立 Rust bootstrap uninstaller；
- 代码主题采用隔离 WebView、QuickJS 还是 WASM 组件模型；
- WebView2 Evergreen 缺失时采用在线引导还是固定运行时包。
- 单全局宿主与每显示器共享宿主的最终拓扑；不考虑每海报一个永久 WebView2 作为默认生产方案。

## 13. 原生托盘与快捷菜单

托盘由 Rust/Tauri 原生层拥有，不依赖控制中心 WebView 常驻。托盘协调器维护只读的 `TraySnapshot`：

```text
posters_visible, edit_mode, animation_paused, autostart,
selected_poster, active_theme, available_quick_themes,
desktop_host_state, ai_configured
```

菜单动作转换为与控制中心相同的本地 Command，不建立第二套业务路径。主页和设置复用单实例窗口；主题菜单使用缓存摘要，打开菜单时不读取磁盘或启动 AI。

Windows Shell/Explorer 重启后通过 `TaskbarCreated` 恢复通知图标。应用不能保证图标位于主通知区域还是“∧”折叠栏，也不修改该系统偏好。

## 14. 可选 AI Provider 与 Agent

```text
Agent Panel
  -> Context Builder
  -> AiProvider
     -> OpenAI
     -> DeepSeek
     -> OpenAI-Compatible
  -> ToolCall Parser
  -> Schema + Semantic Validator
  -> ChangePlan Preview
  -> Unified Command / Undo Stack
```

### 14.1 Provider 契约

```text
test_connection(config_ref)
stream_chat(conversation, tools, cancellation)
cancel(request_id)
capabilities()
```

配置只保存 Provider 类型、Base URL、Model 和非敏感选项。Key 使用 Windows Credential Manager 的资源名引用；Rust 请求层按需读取，用后缩短内存驻留时间，绝不返回前端。

### 14.2 启动约束

- 未配置 Key 时不构造 Provider 客户端、不启动会话、不联网；
- 用户打开 Agent 并提交请求后才建立网络请求；
- 关闭 Agent、退出应用或切换 Provider 时取消流式请求；
- 全屏暂停不强制终止用户主动发出的请求，但退出和卸载必须终止；
- API 错误不能进入桌面宿主主循环。

### 14.3 工具与主题草稿

AI 调用与手动 UI 共用 `DesktopCommand`、`ChangePlan`、预览和撤销接口。模型没有 Shell、文件或 Tauri 通用 IPC 权限。主题草稿进入独立临时目录，完成路径、大小、类型、CSP 和脚本权限检查后才进入隔离预览。

详细设计见 [`AI_ASSISTANT_DESIGN_V0.1.md`](AI_ASSISTANT_DESIGN_V0.1.md) 与 [`TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md`](TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md)。

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-02 | V0.1 | 建立首版架构、数据、安全、宿主和卸载方案 |
| 2026-08-02 | V0.1 | 增加原生托盘快捷菜单与本地 BYOK AI Provider 架构 |
| 2026-08-02 | V0.1 | 固化桌面收纳层默认可见、控制中心按需独立的运行表面边界 |
