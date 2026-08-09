# Desktop Runtime Productionization 当前交接

最后更新：2026-08-09

状态：`ACTIVE — C-DRP-02 IN PROGRESS`

## 已建立

- C-DHF-01..08 已关闭，A0 的 ToolWindow/WorkerW 适配、桌面 Z 序、命中、恢复、暂停和性能采集是输入基线；
- V1 已冻结单全局透明宿主；当前阶段只要求主显示器单海报闭环，后续多海报共享同一宿主；
- C-DRP 已拆为接口契约、单宿主拓扑、合并命中、恢复协调、生命周期和 Release/性能六个可验证任务；
- A 层路线已明确 C-DRP 完成后依次进入 C-PCB、C-PSS、C-IBC/C-RLC，再进入大幅面海报核心；
- C-DRP-01 已完成：Rust/JS `host-contract-v1`、DisplayTopology、HostSurface revision、native geometry owner、单次 canonical sync 和旧 A0 命令兼容包装均已落地；
- C-DRP-01 收起同步缺陷已修复：interaction region 使用目标业务尺寸，不再读取 resize 前的旧展开高度；同步参数构造异常已纳入可恢复边界；
- C-DRP-02 代码闭环已实现：唯一桌面宿主拓扑、DisplayTopology scale policy、monitor/renderer 比例分层、effective logical work area、响应宿主尺寸与前端等比呈现均已落地；
- 当前 2560×1440 / WebView renderer 1.2 环境得到 responsive `1.074`、effective `1.2888`；收起宿主/区域 `657×175 / 631×149`，展开宿主 `657×531`，DOM 与 viewport 无裁切；
- HostTopology 实测 `1` 个桌面宿主、`1` 个隐藏控制中心、总计 `2` 个 WebView，单宿主不变量为真；
- 冷启动顶部 DWM 白条第一轮结论已撤销：用户复测证明 ready 顺序不是充分条件；异常现场 Win32 style `0x14C80000` 显示 `WS_CAPTION` 被 Tauri/tao 后续生命周期重新写回；
- 第二轮已在 `WM_STYLECHANGING` 建立非客户区 style 不变量；5 次连续冷启动均为无 caption 的 `0x14000000`，第 6 次实屏无白条，当前等待用户再次退出/启动确认；
- `pnpm run check` 46 项 Node/契约测试、Rust 32 项测试、UI/Release build、连续冷启动 Win32 style 与实屏探针通过；
- C-DRP-02 尚未关闭：当前机器只有一台物理显示器，混合 DPI/负坐标副屏/主屏切换/热插拔矩阵待用户环境。

## 精确下一步

关闭 C-DRP-02 的剩余物理矩阵：

1. 用户退出并重新启动当前新版 Release，确认首次可见无顶部白条，再验证展开/收起无裁切、按钮命中正常；
2. 若存在第二显示器，将宿主拖入副屏，记录两屏分辨率、Windows 缩放、HostSurface targetDisplayId/responsive/effective scale 和 Win32 矩形；
3. 执行混合 DPI 跨屏往返、主屏切换、重启恢复和可用时的热插拔；确认视觉比例相近、命中吻合、锚点不漂移；
4. 物理矩阵通过后把 C-DRP-02 标为 `COMPLETE`，再激活 C-DRP-03；失败则保留主屏单海报回退并记录限制。

## 阻塞

代码无阻塞。关闭任务需要第二显示器/混合 DPI 物理环境和用户目视确认；当前机器只枚举到 `\\.\DISPLAY1`。

## 工作区注意事项

- 当前分支：`impl/poster-redesign-V1`；未经用户明确要求不提交、不推送、不合并；
- `.claude/` 是未跟踪的用户目录，本工作流不读取、不修改；
- 保留 `sync_host_surface` 为唯一生产几何写入口；旧两步命令只能继续作为调用同一内部函数的兼容包装；
- HostSurface 请求的宽高必须来自目标业务状态；DOM 测量只用于表面偏移，不得把 resize 前的旧宿主宽高回写为目标 interaction region；
- 不把过渡 `poster-snapshot-bridge-v1.json` 扩展为正式仓库；正式 SQLite 状态仓库属于后继 C-PSS；
- 不引入 Explorer 注入、未验证 COM 指针或高频轮询。
