# DesktopUp V2 V3 制作计划 V1

2026-09-11最新：[M07收口](../40_Implementation/V3-04/M07_CLOSEOUT.md)测量/安全及交接完成，用户已决定功能欠项处置，M07-01..04 PASS。[预算差距](../40_Implementation/V3-04/M07_RESOURCE_BASELINE_20260910.md)保留。完整减少动效/手动连点队列仅[按需登记](ON_DEMAND_OPTIMIZATIONS.md)；[M08计划](V3_04_M08_AUTO_POSTURE_PLAN.md)的01～05分项PASS，手动抑制规则已确认，生产共享调度/空间通道已接，最小设置UI已通过合同/真实控件门；[M08-06](../40_Implementation/V3-04/M08_LIFECYCLE_REGRESSION.md)生命周期实现/已执行回归通过；最终自动环境与受影响视觉门OPEN；用户已要求进入[M08-07](../40_Implementation/V3-04/M08_EXPERIENCE_AND_RESOURCE_GATE.md)，工具就绪、人工/资源待验，仍未关闭M08。

最后检查：2026-09-11（文档对齐，主线未改）。适用分支：`impl/poster-redesign-V3`。
状态：M07 PASS；M08 IN PROGRESS，01～05分项PASS、TBD-017已解决，M08-06已实现/已执行回归通过，最终自动环境与受影响视觉门OPEN；R4及M05/M06既有结论保留。
当前 [修复/清理记录](../40_Implementation/V3-04/M07_ANIMATION_R4.md) 与 [排查方法](../20_Architecture/PRESENTATION_DIAGNOSIS_PLAYBOOK_V1.md) 已收口本次故障。稳定入口 `scripts/start-m07-animation.ps1`。
最近顺序：M08自动收展最小闭环 → 原04-04基础预览/必要管理 → 04-05托盘 → 04-06配置/恢复及04-07/08生命周期 → V3-05真实内容。先稳定闭环，不直接跳到文件操作，不扩张外观/画布范围。
本总计划维护阶段目标和依赖；子项当前状态归[细化工作流§4](V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#4-v3-04-m多海报与参数基础前置)，交接仅摘要，验证日志保留当时事实。历史状态不自动成为当前待办，也不因上层收口自动补成全范围PASS。

## 1. 总目标

以 DeskBox 已验证的 Windows 原生桌面整理路线为工程基线，从空项目实现 DesktopUp V2：先证明桌面层级、Win+D、透明空白穿透和窗口生命周期，再完成控制中心、文件收纳、内容 Item、活跃度排版、主题热更新和可选 AI。

本计划不恢复旧 Qt/Win32/Axiom 代码，也不把 DeskBox 自身的功能或测试结果当成 DesktopUp 的完成证据。

## 2. 执行原则

1. 每阶段只实现本阶段出口门需要的最小完整能力；
2. P0 桌面层和文件安全失败时停止扩展功能，先修根因；
3. DeskBox 代码可以移植，但每个移植单元必须登记来源、许可证和 DesktopUp 改动；
4. 自动测试验证合同，真实 Windows 桌面人工门验证窗口、Shell 和视觉；二者不能互相替代；
5. 保留主屏单海报回归，正式推进主屏多海报；多屏/混合 DPI 认证仍独立，不以单海报证据冒充多实例支持；
6. 先功能完整，再做 Native AOT、内存压缩和安装器深化；
7. 每个阶段关闭时更新计划、模块目录、代码锚点、验证记录和下一阶段交接。

## 2.1 2026-09-07 范围与执行修订

用户正式确认通用桌面内容、多海报、唯一控制中心/托盘、独立主题、热度排版、AI 辅助定制。七份需求已同步；[通用多海报合同](../20_Architecture/GENERAL_CONTENT_MULTI_POSTER_CONTRACT_V1.md) 和 [细化工作流](V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md) 补齐数据、作用域、依赖与逐项出口。最初调整为仅文档，随后 M01 已完成数据合同实现；不把该子集等同于多窗口产品完成。

历史增量：V3-04-03 R1及M01..03/M04-01证据保留；M04-03..06空间/输入接入时已有313/313和实际进程证据。当时未完成的集中安全项，现由M07记录逐项结论；不再将M04-03..05列为待实现或指示回到旧R2包补验。当前M08-06已实现/已执行回归通过，最终自动环境与受影响视觉门OPEN，未覆盖的环境认证单列。

先交付可日用收纳版本：V3-05 真内容与 V3-06-01..04 的稳定视口并进，基础 Box 与 V3-09 最小安装/恢复/清理提前进入首个可用包；完整 AI 不阻塞日用交付。下图是能力分组，不再要求 AI 结束才开始打包。完整工作卡、证据和未决裁决见细化工作流。

## 3. 总阶段图

```text
V3-00 文档与仓库重置
  ↓
V3-01 WinUI 工程骨架与许可证基线
  ↓
V3-02 桌面层海报宿主与 Explorer 生命周期
  ↓
V3-03 海报输入、拖缩、锁定和收起
  ↓
V3-04 权威状态、控制中心与托盘
  ↓
V3-05 文件收纳与真实 Item CRUD/启动
  ↓
V3-06 连续 viewport、活跃度与动态排版
  ↓
V3-07 主题平台、热更新与失败隔离
  ↓
V3-08 BYOK AI 与受控 ChangePlan
  ↓
V3-09 安装、恢复、更新与零残留
  ↓
V3-10 性能、AOT 和 Release 认证
```

## 4. V3-00：文档与仓库重置

状态：`COMPLETE — HISTORICAL RESET BASELINE`

### 工作项

- V3-00-01：建立 `impl/poster-redesign-V3`；
- V3-00-02：移除旧 `src/`、`prototype/`、构建工程和测试集；
- V3-00-03：移除旧技术选型、Qt/Axiom 架构和候选证据；
- V3-00-04：保留并同步七份冻结需求；
- V3-00-05：形成 DeskBox 路线总体架构、模块目录和本计划；
- V3-00-06：登记 GPLv3 与品牌边界。

### 出口门

- 工作树除需求、V3 架构、计划、入口和 Git 元数据外无旧实现；
- `src`、`prototype`、旧 `out` 和旧测试不存在；
- 所有当前文档链接可解析；
- Git diff 能清楚显示“旧路线删除 + V3 文档基线”。

## 5. V3-01：WinUI 工程骨架与许可证基线

目标：建立能启动、显示托盘、打开空控制中心并干净退出的最小 Windows 原生工程，但暂不显示真实海报。

状态：`COMPLETE — AUTOMATED 10/10 + USER MANUAL PASS`

### 工作项

- V3-01-01：锁定 .NET、Windows App SDK、WinUIEx、MVVM、DI 与测试框架版本；
- V3-01-02：建立 `DesktopUp.sln` 和 App/Application/Domain/Infrastructure/Windows 五项目；
- V3-01-03：建立单实例和第二实例启动意图转发；
- V3-01-04：建立当前用户数据目录、结构日志和 resource ledger；
- V3-01-05：建立最小托盘、空控制中心和普通退出；
- V3-01-06：建立 `LICENSE`、`THIRD_PARTY_NOTICES.md`、DeskBox 来源登记格式；
- V3-01-07：建立 CI/本地 build、test、run 脚本和第一批合同测试；
- V3-01-08：验证普通用户权限运行，不以管理员权限启动。

### 自动门

- Release x64 可构建；
- 单实例合同通过；
- 数据目录不写入安装目录；
- 退出后无托盘、进程、临时窗口和未释放 ledger 项；
- Domain/Application 不引用 WinUI/Win32。

自动结果（2026-09-03）：

- Release x64：0 warning / 0 error；
- 合同测试：10/10 PASS；
- 真实进程 smoke：托盘创建成功，正常退出，resource ledger 为 `clean=true` 且零资源；
- 当前用户数据根：`%LOCALAPPDATA%\DesktopUp\V3`，不写安装/构建目录；
- 从最终 exe 提取确认 `asInvoker`、PerMonitorV2 与 UTF-8；当前机器的 Explorer Shell 自身已提权，标准 token 实测仍需在非提权账户/环境完成。

### 人工门

- 第一次启动只出现一个托盘图标；
- 双击托盘只打开一个控制中心；
- 关闭控制中心后主进程仍在；
- 托盘退出后完全结束。

## 6. V3-02：桌面层海报宿主与 Explorer 生命周期

目标：以 DeskBox 的 `SHELLDLL_DefView` owner 路线显示最小纯色/诊断海报，先关闭历史上反复出现的白条、层级和 Win+D 风险。

状态：`COMPLETE — R2 AUTOMATED PASS + USER MANUAL P0 PASS`

当前增量（2026-09-03）：

- V3-02-01/02：既有 DefView 定位、五次稳定采样和单调 generation 已实现；
- V3-02-03/04/05：纯色 WinUI 诊断海报、owner attach/read-back/detach、非 topmost/no-activate/sibling bottom 已实现；
- V3-02-06：内容、bounds、边框策略和 owner 全部在隐藏态准备，最后以一次 native desktop-bottom/show 提交变为可见；
- V3-02-07：与 V3-02-08/09 合并执行完整人工 P0 门，不再拆成缺少生命周期覆盖的首轮结论；
- V3-02-08：已监听 `TaskbarCreated` 并以 owner/host 健康探针兜底；失效后等待新 DefView 稳定，在同一 poster HWND 上重挂、读回验证并拒绝旧 shell generation；
- V3-02-09：已建立显示器 bounds/work-area/DPI 签名、独立 topology generation、稳定采样、DIP 到物理像素投影、工作区夹紧和显示消息/低频探针恢复；
- V3-02-08/09：恢复请求按 `(DesktopShellGeneration, DisplayTopologyGeneration)` 版本对在提交前后校验，较新请求使迟到结果失效；
- V3-02-10：启动、监控、重挂、重投影、retry、stale、commit、detach 和停止均有结构日志；
- 首次完整人工门发现冷启动及点击后海报可能进入普通窗口前方，判定 P0 FAIL；日志证明当时 recovery 未运行，根因为原 V3 只吸收了 owner/no-activate，遗漏 DeskBox 的原子 bottom/show 与 native pointer activation guard；
- V3-02 层级修订 R1：隐藏态先准备内容和 bounds，最后以 `HWND_BOTTOM + SWP_SHOWWINDOW` 原子显示；`WM_MOUSEACTIVATE` 同步 no-show bottom reassert 后返回 `MA_NOACTIVATE`；失败转入既有 generation 恢复；
- R1 实机仍可在桌面右键/刷新后被 Explorer owner 组抬高；R2 补齐 `WM_ACTIVATE`/`WM_ACTIVATEAPP` 同步归位，并在 `WM_WINDOWPOSCHANGING` 提交前把非 bottom Z-order 改写为 `HWND_BOTTOM + SWP_NOACTIVATE`；
- 自动证据：Release 构建 0 warning / 0 error，合同测试 45/45，真实 Explorer 启动 smoke 及 clean ledger 通过；
- DeskBox 吸收项与 V3 补强合同见 `../40_Implementation/V3-02/DESKBOX_RECOVERY_REFERENCE_AND_V3_STRENGTHENING.md`。

### 工作项

- V3-02-01：实现 `ExplorerDesktopLocator`，只发现现有 DefView，不强制生成 WorkerW；
- V3-02-02：实现启动稳定采样和 desktop generation；
- V3-02-03：实现无边框 WinUI PosterVisualWindow；
- V3-02-04：实现 owner attach/read-back/detach；
- V3-02-05：实现非 topmost、no-activate 和 sibling Z-order；
- V3-02-06：实现首帧准备后一次显示，禁止白底中间帧；
- V3-02-07：实现 Win+D、桌面刷新、文件选择器、普通窗口和全屏矩阵；
- V3-02-08：实现 Explorer 重启后的稳定重挂：监听失效信号、建立独立 `DesktopShellGeneration`、等待新 DefView 稳定、对同一 PosterWindowCluster 执行单航班 owner 重挂、读回验证并拒绝旧 generation；禁止新建第二海报、hide/show、激活或临时 topmost；
- V3-02-09：实现分辨率、DPI、工作区变化重投影：建立独立 `DisplayTopologyGeneration`，合并原生消息与低频拓扑兜底，等待稳定后按 DIP 尺寸、显示器身份、锚点和工作区一次提交物理 bounds；恢复期间禁止持久化暂态几何；
- V3-02-10：建立窗口 handle、owner、style、generation 诊断事件。

V3-02-08/09 共同遵循：恢复任务捕获 `(DesktopShellGeneration, DisplayTopologyGeneration)`；提交前后任一 generation 改变，本轮结果作废并由最新状态接管。Explorer 恢复只修复宿主关系，显示恢复只按稳定快照重投影几何，两者都不能绕过 owner、样式、层级和实际 bounds 的读回验证。

### 自动门

- attach/detach 幂等；
- 错误 handle 不污染缓存；
- Explorer generation 变化只产生一个有效重挂，迟到 generation 不得提交；
- 同一 HWND 集完成重挂，不产生第二张海报、第二套输入岛或第二个托盘；
- 重挂不得出现 hide/show、激活、topmost 或未绘制中间帧；
- 退出后 owner 和 HWND 全释放；
- 100/125/150/200% 的逻辑/物理坐标转换合同通过；
- 拓扑恢复至少需要稳定样本、提交后验证和有界重试；恢复期间的 bounds 变化不得进入正式持久状态。

### 人工 P0 门

- 海报可见且无系统标题栏、顶部白条、灰框和矩形未绘制区；
- 层级严格为普通/全屏程序 > 海报 > 桌面图标 > 壁纸；
- 连续 Win+D 不隐藏、恢复或闪烁海报；
- 桌面右键/刷新和文件选择器不令海报瞬时置顶；
- Explorer 重启后自动恢复且只有一张海报；
- 显示变化后海报仍在工作区内。

失败处理：任何 P0 不通过时停留 V3-02，不进入海报内容和文件功能。

## 7. V3-03：海报输入、拖缩、锁定和收起

目标：把诊断海报升级为 NIGHT OPS 结构，并完成 DesktopUp 独有的视觉/输入分离。

状态：`COMPLETE — R5 AUTOMATED 73/73 + USER MANUAL PASS 2026-09-04`

当前增量（2026-09-03）：

- V3-03-01/02：建立全幅透明视觉 HWND 与同 owner 的稀疏原生 InputIsland；USING 空白不进入输入岛；
- V3-03-03：输入 layout 使用单调 revision，每次视觉重排后一次提交对应形状区域；
- V3-03-04：标题拖动及 EDITING 的四边、四角八向缩放使用系统 pointer capture 和屏幕坐标增量，跨重排阈值不重启手势；
- V3-03-05/06：外框几何不缩放字体/图标语义；USING、EDITING、LOCKED 权限由独立策略约束；
- V3-03-07：收起固定 60 DIP，保持 top/left/width 和展开高度，展开按钮不改变屏幕位置；
- V3-03-08：预览逐帧应用，取消回滚，松开只产生一次 `poster.geometry_committed`；正式 SQLite 持久化由 V3-04 状态权威接管；
- V3-03-09：NIGHT OPS 受保护 chrome 与六张测试 Item 已替换诊断卡；
- V3-03-10：无边框/透明视觉、物理 bounds read-back、输入区域边界校验、结构日志和退出 ledger 已接入；像素级视觉稳定性仍由本阶段人工门裁决；
- 首轮人工门：Item、标题拖动、锁定、收起、缩放、层级和退出通过；应用外框、跨进程空白穿透及缩小后滚动失败，因此未关闭本阶段；
- V3-03-R1：实机定位 WinUI `DesktopChildSiteBridge` 为真实空白拦截者；在 post-composition 阶段恢复并持续校验视觉 HWND 的 layered+transparent style，原生探针证明空白落到 Explorer、Item 留在 InputIsland；
- V3-03-R1：USING 取消外框，EDITING 仅保留 1 DIP 状态边线；六项区域改由标准 WinUI `ScrollViewer` 连续滚动，InputIsland 转发 Item/滚动轨 wheel；
- V3-03-R1：吸收 DeskBox 最新采样逐帧合并、最终帧刷新和单次提交意图，同列数 resize 不再反复重建 Item grid；
- V3-03-R1 人工反馈：空白框选/右键、原生图标、Item、交互、层级均通过，细编辑边线可接受；仅四个圆角露出矩形 HWND 默认底色；
- V3-03-R2：视觉 HWND 与 InputIsland 同 revision 提交 8-DIP 物理圆角 HRGN，尺寸/DPI 变化时重新生成；采用 DeskBox 常规 Box 的默认圆角、细边、标题栏、操作按钮和深色实色回退尺度，但不提前实现 V3-07 主题系统；
- V3-03-R2 人工反馈：四角问题不再作为主要反馈，但 DeskBox 默认 0.8-DIP 浅色边线在桌面上形成明显白框，整体视觉近似未获接受；
- V3-03-R3：USING 外边界改为透明/零厚度，EDITING 保留 1-DIP 状态线；不在本阶段引入跨窗口阴影层，DeskBox 风格重新设计延后；
  - 自动证据：Release 构建 0 warning / 0 error，合同测试 53/53，真实桌面 smoke 证明视觉窗和输入岛同代际创建并在退出后零残留；原生探针证明左上角在 HRGN 外、中心在 HRGN 内；
- V3-03-R4：修复右上角视觉位置与 InputIsland 命中位置由两套常量分别计算造成的错位；标题栏 padding、24-DIP 操作尺寸、2-DIP 间距及 46-DIP 高度统一进入 Domain 几何合同；
- V3-03-R4：受保护操作改为“收起/展开 + 更多”；更多菜单提供锁定/解除锁定、进入/完成编辑、打开控制中心，并预留“新建文件/新建文件夹”入口。文件入口在 V3-05 完成事务、恢复账本和真实 Item CRUD 前保持禁用，不提前制造无保护的文件写入；
  - 自动证据：Release 构建 0 warning / 0 error，合同测试 54/54，真实桌面 smoke 证明视觉窗和输入岛同代际创建并在退出后零残留；R4 交互焦点复测待执行。
- V3-03-R5：补齐四边四角八向 InputIsland、方向光标与对边锚定纯几何策略；尺寸提交改为“先准备 layout/region，再以 `NOCOPYBITS | DEFERERASE` 一次提交 bounds”；HRGN 负责窗口外透明，WinUI 根层与 `WM_ERASEBKGND` 统一使用 `#21242A` 回退，关闭扩大窗口时系统默认白底入口；
  - 自动证据：正式 Release 构建 0 warning / 0 error，合同测试 73/73；真实桌面 smoke PID 14672 证明视觉窗和输入岛同代际创建并在退出后零残留；
  - 人工裁决：2026-09-04 用户确认八向缩放、对边锚定及无白底通过，V3-03 正式关闭。

### 工作项

- V3-03-01：建立 PosterWindowCluster 和最小 InputIsland；
- V3-03-02：验证半透明空白对 Explorer 的真实穿透机制；
- V3-03-03：按同一 layout revision 生成视觉和输入区；
- V3-03-04：实现标题栏拖动、右下缩放和 pointer capture；
- V3-03-05：实现 FrameGeometry 与 ContentMetrics 分离；
- V3-03-06：实现 USING/EDITING/LOCKED 权限；
- V3-03-07：实现 expanded/collapsed bounds 与顶部锚定收起；
- V3-03-08：实现按帧几何事务和松开后一次持久化接口；
- V3-03-09：实现 NIGHT OPS 受保护 chrome 与六个测试 Item；
- V3-03-10：建立白条、灰框、裁切和输入岛错位检测。
- V3-03-11：统一标题栏操作的视觉/命中几何，建立收起 + 更多菜单。
- V3-03-12：补齐八向缩放与对边锚定，建立无白色默认擦除的 prepared bounds 提交边界。

### 自动门

- geometry transaction 的 prepare/commit/rollback；
- 一次按下跨列数阈值仍保持 capture；
- 收起/展开不改变顶部、水平位置和宽度；
- 锁定时外框不变，按钮仍工作；
- 收起与更多图标的可见矩形和 InputIsland 命中矩形来自同一布局合同；
- 视觉与命中 revision 永远一致。
- 四边四角共享一套最小/最大/工作区边界，左/上缩放保持相反边不动；
- 尺寸提交不得暴露系统默认白色客户区，不得改变 owner、Z-order 或激活态。

### 人工 P0 门

- 海报空白左拖出现 Windows 桌面框选；
- 同一空白右键出现 Windows 桌面菜单；
- 空白覆盖范围内原生快捷方式可单击/双击；
- 六个 Item、标题拖动、锁定、收起和缩放分别可用；
- 右上角仅显示收起/展开与更多，点击各自可见图标只触发对应动作；更多菜单可切换锁定、编辑并打开控制中心；
- 收起按钮保持原位，展开后不跳动；
- 连续拖动/缩放无规律性闪烁、白条、灰框或置顶。

## 8. V3-04：权威状态、控制中心与托盘

目标：用一个状态权威连接海报、控制中心和托盘，消除 UI 回弹和两端状态不一致。

状态：`M07-01..04 PASS；M08 IN PROGRESS（01～05分项PASS，06实现/余门OPEN）`

2026-09-07 回归已关闭：OutputPath 缺分隔符造成 XBF/PRI 名称错误已修复，DUV3001、真实开窗 smoke 与 97/97 原有证据保留；用户确认人工通过。尚未进入V3-04-04；M01..07后的最新必要补项为M08自动收展，先按其任务卡执行。

历史实现证据（2026-09-04，以下计数仅适用于对应版本）：

- V3-04-01：引入 `Microsoft.Data.Sqlite 10.0.11`，建立独立 `IStateStore` 端口与 Infrastructure adapter；
- schema v1 建立 migration ledger、单例 state head 和 operation idempotency journal；每次提交按 expected revision 执行 SQLite 事务 CAS；
- 首次创建、重复初始化、旧 v0 迁移、迁移前在线备份、备份完整性验证、冲突、幂等重放、事务回滚和双 store revision 竞争均有真实 SQLite 自动测试；
- 状态初始化现在位于托盘、控制中心和海报创建之前；损坏数据库保留原文件并中止启动，不以第二份默认状态掩盖失败；
- 自动证据：Release 0 warning / 0 error，合同测试 81/81；真实启动日志顺序为 `state_store.initialized → tray.created → poster.shown`，退出 `activeResources=0`。
- V3-04-02：schema v2 建立 Poster/Settings 持久字段、repository snapshot/CAS 和应用服务边界；拖缩完成、锁定、收起已提交同一 global revision，冲突/失败恢复 committed snapshot；
- V3-04-02 自动证据：Release 0 warning / 0 error，合同测试 87/87；随后普通退出/重启恢复人工门已通过；本条保留原自动结果。
- V3-04-03：建立读写分离 command/query 端口、单事务 aggregate snapshot、三端同 revision projection、单调分发、冲突/权限/幂等语义；
- V3-04-03 自动证据：Release 0 warning / 0 error，合同测试 97/97；真实 v1→v2 迁移、projection 发布、启动/退出和临时文件零残留 smoke 通过。

### 工作项

- V3-04-01：建立 SQLite schema、迁移、revision/CAS 和备份；
- V3-04-02：建立 Poster/Settings repository；
- V3-04-03：建立 command/query 和三端 projection；
- V3-04-M01：PASS，schema v3、独立实体/初始化/版本和目录仓储；[实际合同](../40_Implementation/V3-04/POSTER_CATALOG_SCHEMA_V3.md)；117/117 自动回归，不新增桌面人工门；
- V3-04-M02：AUTO PASS，目标化命令、三端集合投影、实体读集CAS/批量事务已实现；147/147为[该阶段证据](../40_Implementation/V3-04/TARGETED_COMMAND_PROJECTION_V2.md)。后续当前版本交互/确认/退出范围见M07，不把旧待人工标签当新待办。
- V3-04-M03：AUTO PASS；会话registry与共享恢复已落地，[M03合同](../40_Implementation/V3-04/M03_SESSION_RECOVERY.md)保留163/163及0/1/3进程证据。M07已覆盖所列普通重启/零目录/交互安全，不由此推定多实例Explorer重建、多屏或混合DPI全部人工通过。
- V3-04-M04：AUTO PASS，目录选择、拖缩碰撞、保尺寸展开、连续推让/归位与多海报输入均已接入；M07所列主屏空间/输入安全已PASS。G1～G4、R2及[M04记录](../40_Implementation/V3-04/M04_MANUAL_VALIDATION.md)保留为对应版本证据，不再安排重复实现或旧包集中人工；非M07覆盖的认证不补PASS。
- V3-04-M05：CLOSED / CONTRACT/BUILD/PROCESS PASS；[最小参数合同](../40_Implementation/V3-04/M05_PRESENTATION_PARAMETERS.md)已接入v1声明式JSON、兼容/只读fallback、目标草稿/CAS、独立度量/颜色/标题和中心只读摘要；395/395、Release通过，2026-09-09参数两轮及空间回归均PASS。不做真实文件Item、完整主题编辑器或AI。
- V3-04-M06：CLOSED / CONTRACT/BUILD/PROCESS PASS；[多实例合同夹具](../40_Implementation/V3-04/M06_MULTI_INSTANCE_FIXTURES.md)共用三用途数据，新增10项，完整复跑405/405；Exercise 9阶段、单实例激活及Restart完整配置读回全通过，exit0/资源0。不重复建设状态权威/空间求解；当时的人工安全余项现已在M07按范围归并。
- V3-04-M07：PASS；[收口记录](../40_Implementation/V3-04/M07_CLOSEOUT.md)508/508、24项稳定绑定、5.1启动、36/36资源、1/3/6空间及7项自动安全、S1～S5人工与同库零目录重启齐。用户已决定F03/F06按需记录、F07必要先做，04交接关闭；不是三个功能已实现。权限只记录，内存差距交V3-10且不改预算。
- V3-04-M08：IN PROGRESS；[自动收展最小闭环](V3_04_M08_AUTO_POSTURE_PLAN.md)七项：纯模型、设置迁移、指针/交互保护、调度接线、最小中心设置、生命周期回归、人工/增量资源门。01～05分项PASS，TBD-017已解决，06..07尚未实施；子项状态见细化工作流。默认OFF，逐海报1～300秒/默认3秒；复用既有空间/CAS/R4通道。无新动画引擎或手动连点队列。
- V3-04-04：M08门后复用M04-01目录/选择器，补用户可用的新增/配置克隆/重命名/删除、基础全桌面预览及参数编辑；几何复用M04空间服务，删除复用确认；不重造列表/状态权威或额外增加复杂画布产品。
- V3-04-05：复用开窗/暂停/退出，增加托盘新增海报；补数量/可见数/锁定汇总及单张/全部作用域，零海报仍能新增；
- V3-04-06：最近可用状态/数据库失败回退＋单文件跨主机习惯配置；A 格式/恢复/策略、B 一致快照导出、C 预览确认导入/映射、D 自动与实际跨机验证；内置主题/参数先支持，自定义载荷复用 V3-07-04；
- V3-04-07：复用窗口释放/重开机制，扩充多实例预览、草稿与订阅资源清理验收；
- V3-04-08：复用普通退出/恢复，扩充多海报、零海报与局部降级恢复。

### 出口门

- 海报、控制中心、托盘显示同一 committed revision；
- slider/主题/几何编辑无回弹；
- CAS 冲突保留用户待处理意图并显示可理解结果；
- 关闭控制中心不影响海报；
- 重启恢复位置、尺寸、锁定和收起状态；
- 数据库损坏不会启动空白或第二份默认状态。

2026-09-07 用户补充门：首次无配置恰好一张默认；已有或导入的有效 0..N 配置按集合恢复，不额外播种。中心/托盘均可新增；中心/海报“...”均可删除且必须提示后果，真实内容不删除。配置文件覆盖海报集合、几何/启动状态及独立主题/排版偏好；跨机先映射/预览/确认，再进入本机唯一权威。详细子项见 [细化工作流 §5.1](V3_GENERAL_CONTENT_MULTI_POSTER_WORKFLOW_V1.md#51-v3-04-06-配置迁移细化planned)；属于新增计划，M01 117/117 不证明这些入口和迁移 UI 已实现。

## 9. V3-05：文件收纳与真实 Item CRUD/启动

目标：实现 DesktopUp 作为桌面整理工具的核心内容能力，替换六个演示 Item。

2026-09-10推进约束：先以05-01/04/05/06及06-01..04形成非破坏性添加/引用、真实图标、打开/定位/移除引用、重启保持的纵向闭环，再扩展磁盘执行。Mapped/Managed及完整Shell能力仍保留原任务，不把“移除引用”冒充删除源文件或物理收纳；05-08账本/补偿安全门仍先于05-07真实执行。详见[M08之后的安全路径](V3_04_M08_AUTO_POSTURE_PLAN.md#7-完成标准与后续最短安全路径)。

### 工作项

- V3-05-01：冻结 `ContentItem` 与 `Collection` schema；
- V3-05-02：实现 Mapped Collection，不移动原文件；
- V3-05-03：实现 Managed Collection 目录与所有权标识；
- V3-05-04：实现文件/文件夹/快捷方式/应用添加和去重；
- V3-05-05：实现图标、显示名、失效状态和后台刷新；
- V3-05-06：实现启动、在资源管理器中显示和移除 Item；
- V3-05-07：实现 native drag/drop 与 Windows copy/move/link 语义；
- V3-05-08：先实现 dry-run、recovery journal 与失败夹具，再开放 05-07 的实际磁盘执行；验证和补偿不能后补；
- V3-05-09：实现新建文件/文件夹/快捷方式；
- V3-05-10：实现重命名、复制、移动、回收站与系统菜单；
- V3-05-11：实现 watcher 合并、断盘和恢复；
- V3-05-12：建立孤儿受管目录检查与卸载恢复入口；
- V3-05-13：应用/Shell 命名空间、网络/云与虚拟流能力矩阵及认证；
- V3-05-14：三种用途的真实收纳竖向场景与日用交付门。

依赖：05-08 的落盘 journal/失败保留是 05-07 实际移动和 05-10 文件变更前置；ShellProxy 在启用第三方扩展前建立，不拖到安装阶段。通用内容不只等于文件路径。

### 安全 P0 门

- 未经确认不移动、覆盖、永久删除用户文件；
- 每次移动前已有落盘 journal；
- 跨盘、冲突、取消、占用、权限失败和崩溃均有逐项结果；
- 映射集合移除只移除关联，不删除文件夹；
- 受管集合卸载前能打开、迁回或保留独立入口；
- 原生拖入、拖出、快捷方式和普通文件启动符合 Windows 语义。

## 10. V3-06：连续 viewport、活跃度与动态排版

目标：完成 DesktopUp 区别于 DeskBox 普通文件格子的核心内容模型。

### 工作项

- V3-06-01：复用已验证连续 viewport，在 V3-05 接真 Item 时同步接稳定 identity/虚拟化，移除演示 Grid 重建，不重新发明滚动；
- V3-06-02：实现连续像素裁剪和连续 scroll offset；
- V3-06-03：实现独立滚动入口，零完整 Item 时仍可滚动；
- V3-06-04：实现动态列数与稳定 Item identity；
- V3-06-05：实现 recency/frequency/newness 原始统计；
- V3-06-06：冻结 30 天存在保护的最终公式；
- V3-06-07：实现 50%–300% 尺寸映射和新 Item 初始值；
- V3-06-08：实现自动、锚定+自动、全手动三层模式；
- V3-06-09：实现拖动重排、冲突偏移、撤销和跳变机制；
- V3-06-10：实现重排延迟，不让刚启动的 Item 当场跳走。

### 出口门

- 外框缩放不改变内容参数；
- 部分行连续显示，不整排消失留大空位；
- 所有 Item 可达，滚动不依赖鼠标停在 Item 上；
- 跨 2/3/4/6 列连续缩放不丢 capture、不闪帧；
- 排名稳定、固定项稳定、自动调整可撤销；
- 零/一/多/失效/重复 Item 全部有测试。

## 11. V3-07：主题平台、热更新与失败隔离

目标：保持 WinUI 原生宿主稳定，同时允许视觉资源和受限模板热更新。

### 工作项

- V3-07-01：冻结 ThemeManifest、tokens、资源和能力 schema；
- V3-07-02：实现 transparent-default、neon-hud、cinema-lightbox；
- V3-07-03：实现不可选但始终可用的 minimal-fallback；
- V3-07-04：实现主题目录、导入/导出和路径安全；
- V3-07-05：实现 ResourceDictionary/ControlTemplate 原子切换；
- V3-07-06：实现文件监听、去抖、候选预览和热更新；
- V3-07-07：验证受保护 chrome 不能被主题删除；
- V3-07-08：实现资源配额、错误隔离和最近可用主题；
- V3-07-09：评估用户代码主题；需要时建立 ThemeHost 进程；
- V3-07-10：建立主题兼容版本与迁移。

### 出口门

- 三主题可即时切换且不改变业务状态；
- 主题文件修改后无需重启即可预览/应用；
- 语法错误、缺资源、超限和 ThemeHost 崩溃不影响海报操作；
- 失败后保持上一主题或进入 minimal fallback；
- 主题没有 Shell、任意文件、进程、注册表和默认网络权限。

## 12. V3-08：BYOK AI 与受控 ChangePlan

目标：AI 只作为主题和布局的可选建议器，不成为程序正常运行的依赖。

### 工作项

- V3-08-01：建立 provider-neutral `IAIProvider`；
- V3-08-02：接入 OpenAI、DeepSeek 和 OpenAI-compatible；
- V3-08-03：实现凭据保存、测试、替换和删除；
- V3-08-04：实现上下文裁剪和隐私预览；
- V3-08-05：实现结构化 AIChangePlan schema；
- V3-08-06：实现 R0–R5 权限、拒绝与确认矩阵；
- V3-08-07：实现 diff 预览、应用、撤销和失败保留；
- V3-08-08：实现速率、超时、取消和费用提示；
- V3-08-09：验证无 Key/离线时主题和 Item 全功能可用。

### 出口门

- AI 不能直接执行 Shell、进程、任意文件和窗口层级操作；
- 未确认 ChangePlan 不改变正式状态；
- provider 输出异常只产生可见错误；
- Key 不进入日志、主题包、备份或诊断包；
- AI 关闭、断网或删除 Key 后程序正常工作。

## 13. V3-09：安装、恢复、更新与零残留

目标：形成当前用户可安装、可选择目录、可升级、可卸载且保护受管文件的完整生命周期。

### 工作项

- V3-09-01：当前用户安装器和用户可选安装目录；
- V3-09-02：依赖探针和离线/联网运行时策略；
- V3-09-03：自启显式 opt-in 和状态同步；
- V3-09-04：单实例安装版重启、更新和路径锁定；
- V3-09-05：ShellProxy/Updater 退出协调；
- V3-09-06：数据库与主题备份恢复；
- V3-09-07：卸载前检测受管文件并提供保留/迁回入口；
- V3-09-08：正式卸载后程序、托盘、自启、缓存和临时文件零残留；
- V3-09-09：隐私过滤诊断包。

### 出口门

- 可安装到用户选择的有效目录；
- 覆盖安装不产生第二份实例或第二份状态；
- 自启关闭时绝不写入启动项；
- 卸载不删除未迁出的用户文件；
- 用户选择完全清理后无进程、托盘、注册表自启和程序缓存残留。

## 14. V3-10：性能、AOT 和 Release 认证

目标：在功能冻结后优化常驻成本，并决定 Native AOT 发布形态。

### 工作项

- V3-10-01：复用 V3-04-M07 提前建立的 CPU、Working Set、GPU、handle、首帧和延迟测量合同，补完整功能场景；
- V3-10-02：测量 Debug/Release、海报静态、控制中心打开和文件突发场景；
- V3-10-03：虚拟化、缓存上限、窗口释放、动画和 watcher 优化；
- V3-10-04：Native AOT 兼容审计，隔离动态 XAML/主题边界；
- V3-10-05：x64 AOT/非 AOT A/B；ARM64 后续决定；
- V3-10-06：全量 P0 回归和长时间稳定性；
- V3-10-07：安装、升级、卸载、Explorer、Win+D、DPI 和文件安全矩阵；
- V3-10-08：Release 证据、许可证材料和交接。

### 最终门

- 冻结需求的每条验收项均有证据；
- 常驻资源目标达成，或经用户形成明确、量化的例外决定；
- AOT 不能破坏主题热更新、状态恢复或 Shell 能力；
- 没有已知文件丢失、窗口置顶、Win+D 闪烁、白条或空白输入回归；
- 发布包包含必要许可证和第三方通知。

## 15. 测试梯度

每阶段按下列顺序执行：

1. Domain 纯单元/性质测试；
2. Application command、CAS、事务和失败注入测试；
3. Windows adapter 的 handle、owner、style 与 Shell 合同测试；
4. 真实进程 smoke；
5. 主屏 Release 人工门；
6. 前阶段核心回归。

任何人工观察必须记录启动命令、构建路径、版本/commit、显示器/DPI、步骤、预期、实际和结论。截图只能证明画面，不能代替 handle、状态或文件事务证据。

## 16. DeskBox 移植规则

每个直接移植工作项必须同时提交：

- DeskBox 源文件相对路径和来源 commit/tag；
- 原许可证声明；
- DesktopUp 目标模块；
- 删除了哪些 DeskBox 非需求功能；
- 修改了哪些行为以满足 DesktopUp 需求；
- 新增的 DesktopUp 自动测试；
- 是否需要 GPLv3 对应源码交付。

禁止整仓复制后再删除。应按一个可验证模块一批迁入，优先顺序为窗口层、生命周期、文件安全和 Shell 适配器。

## 17. Git 与文档节奏

- V3 实现只在 `impl/poster-redesign-V3` 进行；
- 未经用户指示不推送、不合并；
- 每个阶段至少形成一个可复核提交，不把所有阶段压成一次大提交；
- 阶段开始前更新 ACTIVE_PATH，阶段关闭后记录证据和下一步；
- 需求变更必须先回写需求文档，再调整架构和计划；
- DeskBox 更新不会自动覆盖已经冻结的 DesktopUp 模块端口。

## 18. 立即下一步

2026-09-11当前：M07已关闭；M08-01～05及[设置入口](../40_Implementation/V3-04/M08_CONTROL_CENTER_SETTINGS.md)分项PASS，TBD-017已确认，M08-06已实现/已执行回归通过，最终自动环境与受影响视觉门OPEN。生产自动通道已接，M07稳定App未替换；F03/F06按需登记而非完成。零目录同库重启0张/revision19证据保留，不重复验收。权限不阻止启动，50MiB预算不改；未commit/push。以下旧阶段证据按其当时版本保留。

M06完成历史：M05参数×2/空间尾门与M06 Exercise/Restart全部通过，完整合同405/405；结论保留，不因新测量权限限制撤销已有合同证据，也不将旧管理员进程读回冒充新普通权限基线。

M04-06历史自动基线：313/313、Release0warning0error，真实三张输入就绪/确认开关及退出、链式6次/77帧回归通过，见 [输入与确认合同](../40_Implementation/V3-04/M04_INPUT_CONFIRMATION.md)。这些结果不代替M04-07人工PASS。

M04-01阶段历史：本轮已完成M04-01中心列表/稳定目标切换与DeskBox空间来源吸收记录。176/176、最终Release0warning0error、3/0真实会话/中心/退出smoke通过；实际点击因computer-use环境1385未执行，仍需短人工确认。下一开发项为M04-02空间合同/纯求解夹具，再03碰撞、04保尺寸展开、05推让与条件归位；[M04-01合同与启动](../40_Implementation/V3-04/M04_CONTROL_CENTER_SELECTION.md) 给出当前交付边界。下方为本轮实现前的任务来源，不代表仍未实现中心列表。

任务来源：上轮将“中心仅A”定位为默认别名绑定缺口并提前M04-01；本轮已实现该项。M03已有0..N会话和163/163历史证据，本轮增至176/176；未明确报告的Explorer/Win+D核心人工证据仍在M04-07集中补齐。M系列后原04-04继续完善管理/预览，单一权威及既有Host不重写。
