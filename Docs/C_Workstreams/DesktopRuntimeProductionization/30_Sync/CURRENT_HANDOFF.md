# Desktop Runtime Productionization 当前交接

最后更新：2026-08-11

状态：`CLOSED — C-DRP-06 COMPLETE / PLATFORM LIMIT ACCEPTED`

## 已建立

- C-DHF-01..08 已关闭，A0 的 ToolWindow/WorkerW 适配、桌面 Z 序、命中、恢复、暂停和性能采集是输入基线；
- V1 已冻结单全局透明宿主；当前阶段只要求主显示器单海报闭环，后续多海报共享同一宿主；
- C-DRP 已拆为接口契约、单宿主拓扑、合并命中、恢复协调、生命周期和 Release/性能六个可验证任务；
- A 层路线已明确 C-DRP 完成后依次进入 C-PCB、C-PSS、C-IBC/C-RLC，再进入大幅面海报核心；
- C-DRP-01 已完成：Rust/JS `host-contract-v1`、DisplayTopology、HostSurface revision、native geometry owner、单次 canonical sync 和旧 A0 命令兼容包装均已落地；
- C-DRP-01 收起同步缺陷已修复：interaction region 使用目标业务尺寸，不再读取 resize 前的旧展开高度；同步参数构造异常已纳入可恢复边界；
- C-DRP-02 代码闭环已实现：唯一桌面宿主拓扑、DisplayTopology scale policy、monitor/renderer 比例分层、effective logical work area、响应宿主尺寸与前端等比呈现均已落地；
- 当前 2560×1440 / WebView renderer 1.2 环境得到 responsive `1.074`、effective `1.2888`；收起宿主/区域 `657×175 / 631×149`，展开宿主 `657×531`，DOM 与 viewport 无裁切；
- 冷启动实测 `1` 个桌面宿主 WebView、`1` 个 renderer；控制中心首次请求后才创建第二个 WebView/renderer，单宿主不变量保持为真；
- 冷启动顶部 DWM 白条第一轮结论已撤销：用户复测证明 ready 顺序不是充分条件；异常现场 Win32 style `0x14C80000` 显示 `WS_CAPTION` 被 Tauri/tao 后续生命周期重新写回；
- 第二轮 `WM_STYLECHANGING` 非客户区 style 不变量已由用户重新启动 Release 确认：首次显示无顶部白条，收起/展开无裁切且按钮命中正常；
- 用户决定暂不执行第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔物理矩阵；C-DRP-02 以“当前门完成、物理矩阵延期至 C-DRP-06”关闭，不把延期项目表述为已验证；
- 用户在旧 C-DRP-03 实现上确认三项失败：从海报内空白起始的桌面左键/框选会选中 WebView 文本，右键出现 WebView 菜单，海报下方原生桌面图标无法点击；该证据推翻“当前空白穿透已可验收”的判断；
- 根因不是海报整体 Z 序过高，而是 `SetWindowRgn` 同时决定可见形状与输入范围：用完整海报区域保留半透明背景时，WebView 必然占有全部内部空白；缩小窗口区域又会裁掉背景；
- 当前修复把单海报登记拆成完整 `visualRegion` 与标题栏/卡片 `interactionRegions`。原生层保持完整视觉轮廓，并用低级鼠标事件钩子按命中结果切换 `WS_EX_TRANSPARENT | WS_EX_LAYERED`；WebView 默认右键菜单已禁用；
- 首版视觉/输入分离 Release 曾出现进程存活但海报隐藏：首次 DOM 命中宽度基于旧窗口，越出目标逻辑宽度，导致 canonical sync 被拒绝；现已映射到目标视觉宽度、过滤隐藏零尺寸卡片，并阻止显示后 `APPWINDOW` 回写；
- C-DRP-03 范围保持单海报；零/一/多/重叠只指同一海报的输入命中矩形。52 项前端/契约检查、38 项 Rust 测试、Release 构建与用户七项人工矩阵均已通过，任务已关闭。

- C-DRP-04 已完成入口审计：原生 Shell/显示工作器、Tauri DPI 分支和手动重连原先各自恢复，且原生内存位置缓存与应用层 `PlacementStore` 所有权重复；
- 第一实现切片已收敛为 `HostRecoveryRequest` → 原生尾沿合并 → 应用层 `HostRecoveryCoordinator` → 宿主重配/唯一位置恢复/HostSurface 重同步；
- 恢复次数、代数、合并事件数、最近原因和错误已进入 `DesktopHostDiagnostics`；Shell、Explorer、显示/DPI、Tauri scale-factor 与手动重连均只走该路径；
- `pnpm run check` 53/53、Rust 39/39、Release 与 NSIS 构建通过；
- 第一版协调器经用户实测发现连续 Win+D 闪烁；`shellRepair` 已消除 Shell-only 的完整宿主重配，但用户二次复测确认仍会在按下后立即闪，证明此前只消除了延迟重配因素，并未关闭主因；
- 5ms 原生 Z 序探针确认 Shell 会在前台事件后再次把 Progman 提到海报上方，且不发送可用的 `EVENT_OBJECT_REORDER`。默认 ToolWindow 现于该切换窗口使用 48ms `TOPMOST | NOACTIVATE` 防护，普通应用恢复前台或超时后立即降回 Progman 上方；10 轮探针均无正 Z 序差、无 topmost 残留；用户随后确认最终 Release 已较稳定并接受 C-DRP-04 通过；
- C-DRP-05 已完成 `runtime-lifecycle-v1` 第一切片：starting/running/exiting 与 activeStatic/activeAnimated/paused/stopped 由 Rust 单一归约器裁决；
- `generation + eventCount`、`lastTrigger`、手动/自动暂停、显隐、ready 和退出进入 RuntimeSnapshot；退出为粘性，安全暂停优先于动态模式请求；
- RuntimeState 锁内只修改/克隆状态，诊断、托盘、窗口、HostTopology/DisplayTopology 和性能 OS 查询均在锁外；
- 海报显隐改为状态优先，退出在 `app.exit` 前发布；托盘与两个 WebView 接收同一 `runtime-state-changed` 快照，旧事件只作兼容；
- 两个前端消费者以 generation/eventCount 拒绝乱序快照，`document.hidden` 不再回写原生状态；
- 控制中心持续 2 秒轮询已删除；海报 paused/stopped 取消 rAF，仅 activeAnimated 持续采样；
- `pnpm run check` 57/57、Rust 46/46、前端、Release 与 NSIS 构建通过；用户确认 Release 人工生命周期矩阵 5/5 通过，C-DRP-05 已关闭；当前静态海报只证明暂停控制状态正确，不声称已验证尚不存在的视觉动画停帧。
- C-DRP-06 第一切片的 `performance-gate-v1` 只覆盖启动后约 52 秒；普通 Release 长期复核推翻早期 PASS，历史纠正过程保留在日志中；
- 用户确认改用可归因双预算：主进程私有工作集 50 MiB + Desktop-Up/WebView2 完整后代进程树私有工作集 256 MiB，总工作集只作诊断，进程树采集不完整直接失败；
- 控制中心从冷启动静态窗口移除，仅在首次请求时由 `MainWindowCoordinator` 创建；Release 实测冷启动 7 个树进程/1 renderer，请求控制中心后为 8 个/2 renderer；
- `performance-gate-v3` / `performance-baseline-v4` 四态全部 PASS：CPU 62/62/79/62ms，FPS 均为 0，主进程私有工作集峰值约 7.2 MiB，树私有工作集峰值约 109.1 MiB，采集完整；
- `pnpm run check` 58/58、Rust 52/52、Release/NSIS 通过；最新版产物哈希和完整样本见 C-DRP-06 稳定态工作集纠正日志。
- 用户确认最新版普通 Release 四项烟雾全部 PASS：无白条/裁切，空白穿透与全部既有交互无回退，控制中心首次按需创建、关闭后重开正常且海报不受影响；

## 精确下一步

用户已选择接受“当前发布基线只认证主屏单海报”的平台限制。C-DRP-06 已关闭，活动链已交接到 [`C-PCB Poster Contract Baseline`](../../PosterContractBaseline/30_Sync/CURRENT_HANDOFF.md)。

第二显示器、混合 DPI、负坐标副屏、主屏切换、跨屏重启和热插拔仍未认证；后续若扩大平台认证范围，必须单独恢复物理矩阵，不得引用当前关闭结论替代。

## 阻塞

无当前阻塞。C-DRP 按用户接受的明确发布限制关闭；未认证平台矩阵作为后续发布范围条件保留。

## 工作区注意事项

- 当前分支：`impl/poster-redesign-V1`；未经用户明确要求不提交、不推送、不合并；
- `.claude/` 是未跟踪的用户目录，本工作流不读取、不修改；
- 保留 `sync_host_surface` 为唯一生产几何写入口；旧两步命令只能继续作为调用同一内部函数的兼容包装；
- HostSurface 请求的宽高必须来自目标业务状态；DOM 测量只用于表面偏移，不得把 resize 前的旧宿主宽高回写为目标 interaction region；
- 生产 `HostSurfaceRequest` 使用同 revision 的 `surfaceRegistry v1`；当前只登记一个海报表面，并分别提交完整 `visualRegion` 与零到多个 `interactionRegions`；
- `SetWindowRgn` 只维护完整视觉轮廓；输入命中图独立按 `effectiveScaleFactor` 换算，通过事件驱动样式切换穿透内部空白。不得退回“窗口区域等于输入区域”，也不得增加高频轮询；
- 不把过渡 `poster-snapshot-bridge-v1.json` 扩展为正式仓库；正式 SQLite 状态仓库属于后继 C-PSS；
- 不引入 Explorer 注入、未验证 COM 指针或高频轮询。
