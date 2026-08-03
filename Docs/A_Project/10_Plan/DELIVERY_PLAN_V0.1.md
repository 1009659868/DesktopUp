# Desktop-Up 制作与交付计划 V0.2

状态：`PROPOSED`

日期：2026-08-03

## 1. 变更说明（自 V0.1）

本版吸收了 [`POSTER_REDESIGN_V0.1`](POSTER_REDESIGN_V0.1.md) 及其配套设计文档的重构方向决策：

- 海报从 490×392 固定小窗升级为大幅面展示面板
- 接受对数动态缩放、模板系统、四级定制和 WebView2 亮带根治
- 原 M1 "本地收纳 MVP" 拆为两个里程碑：**M1 海报重构**（大幅面 + 模板 + 亮带）和 **M2 本地收纳**（导入/收纳/恢复/启动闭环）
- M3–M5 顺延编号

## 2. 交付目标

完成一个可在 Windows 11 x64 安装、运行、升级和完整卸载的 `DesktopUpSetup.exe`。安装后的核心体验是：大幅面风格化海报展示游戏图标、导入或收纳快捷方式、自动排版、启动游戏、折叠海报、自动暂停，并能安全恢复数据。

## 3. 里程碑与阶段门

### M0：工程与桌面宿主验证

**状态**：`IN PROGRESS`（C-DHF-01–08 执行中）

交付：

- Rust/Tauri/Svelte 最小工程；
- 通知区域图标、原生右键菜单骨架和单实例；
- 透明桌面宿主原型；
- 一张可点击、可拖动的假数据海报；
- 全屏与锁屏暂停探针；
- 关闭主页隐藏到托盘、显式退出结束进程的生命周期探针；
- 性能采样脚本和兼容性记录。

阶段门：`C-DHF-01..08` 全部通过。任何硬门失败都必须先选择修正、回退模式或技术栈调整，不能直接进入海报重构。

参考工作量：5–8 个有效开发日（大部分已完成，剩余 C-DHF-04/05 矩阵与 C-DHF-06 性能基线）。

关键设计文档（已冻结）：
- [`RUNTIME_SURFACE_MODEL_V0.1`](RUNTIME_SURFACE_MODEL_V0.1.md)
- [`TRAY_AND_QUICK_ACCESS_DESIGN_V0.1`](TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md)
- [`POSTER_STATE_MACHINE_V0.1`](POSTER_STATE_MACHINE_V0.1.md)

### M1：海报重构（大幅面 + 模板 + 亮带）

**前置条件**：M0 通过。

交付：

- 大幅面海报窗体：高度近满工作区（上下 12px），宽度 `max(工作区宽 × 0.22, 280px)`，持久保存；
- WebView2 `DefaultBackgroundColor` 全透明亮带根治（Rust + tauri.conf.json + CSS 三层对齐）；
- 模板引擎：Rust 校验 + JS 渲染、插槽语法、CSS 变量注入、命名空间隔离、热切换；
- Neon HUD 和 Cinema Lightbox 两套内置模板完整 CSS/HTML；
- 海报状态机：EXPANDED ↔ COLLAPSED 过渡动画，USING ↔ EDITING 切换，锁定视觉标记，auto_collapse 属性；
- 暂停覆盖：PAUSED 冻结动画和交互，恢复后回到暂停前呈现模式；
- 海报交互：标题条拖动、右边缘宽度调整、右键菜单骨架、图标点击启动；
- 降级回退：模板 CSS/HTML 损坏时使用 minimal-fallback；
- 控制中心模板选择器、深度 0 预设切换。

硬门：
- 大幅面海报透明背景无 10px 亮带残留；
- 两套模板在 280–420px 宽度范围内渲染正确；
- COLLAPSED ↔ EXPANDED 过渡不闪烁、不跳变；
- 模板热切换 < 200ms；
- PAUSED 状态下海报无动画帧、无输入响应；
- 静态桌面 CPU 接近空闲（无持续 requestAnimationFrame）。

参考工作量：10–15 个有效开发日。

关键设计文档：
- [`POSTER_REDESIGN_V0.1`](POSTER_REDESIGN_V0.1.md)
- [`POSTER_TEMPLATE_ENGINE_V0.1`](POSTER_TEMPLATE_ENGINE_V0.1.md)
- [`POSTER_DATA_MODEL_V2`](POSTER_DATA_MODEL_V2.md)
- [`POSTER_VISUAL_NEON_HUD_V0.1`](POSTER_VISUAL_NEON_HUD_V0.1.md)
- [`POSTER_VISUAL_CINEMA_LIGHTBOX_V0.1`](POSTER_VISUAL_CINEMA_LIGHTBOX_V0.1.md)
- [`POSTER_INTERACTION_MAP_V0.1`](POSTER_INTERACTION_MAP_V0.1.md)
- [`POSTER_ERROR_AND_EMPTY_STATE_V0.1`](POSTER_ERROR_AND_EMPTY_STATE_V0.1.md)

实施切片：

| 切片 | 内容 | 预估 |
|------|------|------|
| M1.0 | 大幅面窗口 + 亮带根治（Rust 层 WebView2 DefaultBackgroundColor） | **DONE** |
| M1.1 | 海报 DOM 重构（状态机、标题条、图标区域、过渡动画） | **DONE** |
| M1.2 | 模板引擎（Rust 校验链 + JS 渲染引擎 + 热切换） | **DONE** |
| M1.3 | Neon HUD 模板 CSS/HTML 完整实现 | **DONE** (合入M1.4) |
| M1.4 | Cinema Lightbox 模板 CSS/HTML 完整实现 | **DONE** (合入M1.3) |
| M1.5 | 交互实现（拖动、右键菜单、EDITING 模式） | 2–3 天 |
| M1.6 | 降级与空状态 | 1 天 |

### M2：本地收纳 MVP（原 M1）

**前置条件**：M1 通过。

交付（与 V0.1 M1 相同，顺序无变化）：

- `.lnk`、`.url`、`.exe` 拖入，以及用户主动发起的桌面扫描；
- SQLite 数据与迁移（采用 V2 数据模型）；
- 写入前的解析、图标提取、去重、逐项预览与确认；
- "仅添加"和"收纳并移出桌面"两种明确模式；
- 快捷方式保险库、两阶段恢复账本、异常事务恢复和撤销；
- 海报启动、打开目标位置、异常事务恢复与卸载恢复接口；
- 海报创建、删除（单海报暂不支持多海报）。

实施切片（同 V0.1 A1.x）：

| 切片 | 交付 | 写入用户桌面 |
|------|------|-------------|
| M2.0 | SQLite schema、解析器接口、只读扫描、假目录测试夹具 | 否 |
| M2.1 | 拖入/扫描、图标提取、去重、导入预览、"仅添加" | 否 |
| M2.2 | 保险库、两阶段账本、"收纳并移出桌面"、撤销与恢复 | 经用户逐批确认 |
| M2.3 | 海报启动、打开目标位置、异常事务恢复与卸载恢复接口 | 仅恢复操作 |

阶段门：连续 100 次导入/收纳/恢复故障注入，不覆盖或丢失用户快捷方式；无主动操作不扫描或改变桌面；`.exe` 永不被移动或复制。

参考工作量：10–15 个有效开发日。

关键设计文档：
- [`SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1`](SHORTCUT_IMPORT_AND_COLLECTION_MODEL_V0.1.md)

### M3：排序、排版与动态缩放（原 M2 主题系统的一部分，提前）

**前置条件**：M2 通过（需要真实启动数据）。

交付：

- `GameLaunchRecord` 数据模型、rank_score 计算、`effective_launches` 衰减；
- 行式弹性流 + 子列填充布局引擎；
- 三层排序控制（全自动 → 锚定+自动 → 全手动）；
- 图钉标记视觉和解除交互；
- 对数动态缩放（50%–300%），使用 sqrt 压缩的消费端公式；
- 图标缩放弹性过渡动画（400ms cubic-bezier）；
- 拖拽触发跳变 + 衰减 + 归一化。

实施切片（同 `POSTER_LAYOUT_AND_SORT` §7）：

| 切片 | 内容 | 依赖 |
|------|------|------|
| M3.0 | `GameLaunchRecord` 数据模型、rank_score 计算、有效启动次数 | M2.0 |
| M3.1 | 行式弹性流 + 子列填充布局引擎 | M3.0 |
| M3.2 | 三层排序控制 + 锚定机制 + 图钉标记 | M3.1 |
| M3.3 | 拖拽触发跳变 + 衰减 + 归一化 | M3.2 |

参考工作量：5–8 个有效开发日。

关键设计文档：
- [`POSTER_LAYOUT_AND_SORT_V0.1`](POSTER_LAYOUT_AND_SORT_V0.1.md)

### M4：主题定制与 AI 助手（原 M2 + M3）

**前置条件**：M3 通过（排序/缩放 UI 可作为参数面板验证基础）。

交付：
- 深度 1 结构化参数面板自动生成（从 `params-schema.json`）；
- 深度 2 素材替换（图片、SVG、字体上传和净化）；
- 深度 3 纯代码编辑器（CSS 片段 + HTML 插槽覆盖）；
- 导入/导出体系（`.params.json`、`.theme.zip`、`.full.zip` + 安全校验链）；
- 统一 `DesktopCommand`、`ChangePlan`、预览和撤销；
- OpenAI、DeepSeek 和 OpenAI-Compatible Provider 配置；
- Windows Credential Manager Key 存取；
- AI 通过统一 API 控制海报参数（共用预览/确认/撤销链）；
- 无 Key 不初始化、不联网的启动门。

阶段门：恶意路径包、超大包、外链 CSS 和损坏主题均无法突破应用数据边界；CSS/HTML 注入防御有效；Key 不进入前端、数据库或日志。

参考工作量：12–18 个有效开发日。

关键设计文档：
- [`POSTER_THEME_CUSTOMIZATION_V0.1`](POSTER_THEME_CUSTOMIZATION_V0.1.md)
- [`POSTER_THEME_IMPORT_EXPORT_V0.1`](POSTER_THEME_IMPORT_EXPORT_V0.1.md)
- [`AI_ASSISTANT_DESIGN_V0.1`](AI_ASSISTANT_DESIGN_V0.1.md)
- [`TRAY_AND_QUICK_ACCESS_DESIGN_V0.1`](TRAY_AND_QUICK_ACCESS_DESIGN_V0.1.md)

### M5：游戏平台发现（原 M4）

**前置条件**：M4 通过。

交付（与原 M4 相同）：

- Steam、Epic 本地安装发现；
- Xbox/Microsoft Store Shell 项目枚举；
- 去重、来源角标和手动修正；
- 扫描开关、隐私说明和取消能力。

参考工作量：5–8 个有效开发日。

### M6：发布与卸载封板（原 M5）

**前置条件**：M5 通过。

交付（与原 M5 相同）：

- Release 构建、签名入口和许可证清单；
- `DesktopUpSetup.exe`；
- 安装目录内 `uninstall.exe`；
- 升级迁移、卸载恢复和自清理；
- 用户快速入门、故障排查和隐私说明。

参考工作量：5–8 个有效开发日。

---

## 4. 修订后的实施顺序

```text
桌面宿主硬门 (M0)
  → 大幅面海报重构 (M1)
    → 本地收纳 MVP (M2)
      → 排序、排版与动态缩放 (M3)
        → 主题定制与 AI 助手 (M4)
          → 游戏平台发现 (M5)
            → 安装、升级、卸载验收 (M6)
```

## 5. 能力模块顺序（修订）

| 顺序 | B 模块 | 主要结果 | 对应里程碑 |
|------|--------|---------|-----------|
| 1 | Desktop Runtime Foundation | 桌面宿主、暂停、托盘、自启、恢复 | M0 |
| 2 | Poster Redesign | 大幅面窗口、亮带根治、模板引擎、内置模板、状态机、交互 | M1 |
| 3 | Collection and Launch | 导入、收纳、恢复、启动和元数据 | M2 |
| 4 | Poster Layout and Interaction | 排序得分、衰减、弹性流排布、锚定、拖拽跳变、动态缩放 | M3 |
| 5 | Theme Runtime and Authoring | 四级定制、参数面板、素材替换、代码覆盖、导入导出、Agent API | M4 |
| 6 | Local AI Assistant | BYOK Provider、受控工具、预览和主题草稿 | M4（同里程碑） |
| 7 | Game Source Discovery | 平台发现、去重和来源适配 | M5 |
| 8 | Distribution and Lifecycle | 安装、升级、卸载和发布验证 | M6 |

## 6. 发布产物（不变）

```text
dist/DesktopUpSetup.exe
```

构建内部：

```text
artifacts/<version>/
├─ DesktopUpSetup.exe
├─ SHA256SUMS.txt
├─ THIRD_PARTY_LICENSES.txt
├─ build-manifest.json
└─ test-report/
```

## 7. 验收矩阵（不变，自 V0.1）

| 维度 | 最低覆盖 |
|------|---------|
| 系统 | 当前受支持 Windows 11 x64 版本 |
| DPI | 100%、125%、150%、200% |
| 显示器 | 单屏、双屏、主屏切换、热插拔 |
| Shell | Explorer 正常、重启、崩溃恢复、Win+D |
| 图形 | 集显、独显、远程桌面降级 |
| 数据 | 新装、升级、损坏数据库、同名恢复 |
| 性能 | 静态、动态、全屏暂停、电池模式、200 项目 |
| 生命周期 | 安装、开机自启开/关、退出、升级、完整卸载 |
| 托盘 | 主区域 / 折叠栏、单击/双击/右键、Explorer 重启、快速主题 |
| 模板 | 内置模板渲染、热切换、CSS/HTML 损坏降级、导入导出安全校验 |
| AI | 无 Key、OpenAI、DeepSeek、鉴权失败、超时、取消、无效 Tool Call |

---

## 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-02 | V0.1 | 建立从技术验证到安装包交付的首版计划 |
| 2026-08-03 | V0.2 | 吸收海报重构设计：插入 M1 海报重构、M1→M2 收纳、原 M2–M5 顺延为 M3–M6；新增 Poster Redesign 能力模块；更新关键设计文档引用 |
