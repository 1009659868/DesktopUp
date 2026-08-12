# Poster Contract Baseline 当前交接

最后更新：2026-08-12

状态：`COMPLETE — C-PCB-01..05 PASS / C-PSS ACTIVATED`

## 已知稳定输入

- C-DRP 已关闭，宿主 canonical 入口为 `host-contract-v1`、`sync_host_surface`、`runtime-lifecycle-v1` / `runtime-state-changed`。
- 当前 Release 单海报的人工作业矩阵已通过；C-PCB 首切片不得改变现有投影。
- 当前平台认证限制为主屏单海报；多显示器/DPI/热插拔未认证。

## 当前实施

C-PCB-01 已完成：JS/Rust 共用判定矩阵，专项 10/10、全量 68/68、Rust 59/59 通过；正式 v1 严格校验和无版本 A0 legacy 分流已接线，现有数据不被自动重写。

C-PCB-02 已完成：用户确认 `24px` top/right/bottom 安全边距、expanded 最小高度 `240px`、默认/最大高度 `workArea.height - 48px`、收起内容 `60px`，并保留宿主四周 `10px` gutter（collapsed host 为 `80px`）。JS/Rust 共用工作区矩阵覆盖 1366×768、1920×1080、4K 与非法工作区；专项 14/14、全量 JS 72/72、Rust 61/61 通过。

几何契约要求 initial anchor 使用真实不透明 `displayId`；收起/展开的 host offset 完全相同并提交 `preserve`，所以 top-left 不移动。该模块尚未接管当前 A0/Release 投影，本切片不会改变用户已验收的视觉和交互。

C-PCB-03 已完成：自制标题栏角色、拖动起点/排除项和 `5px` 阈值已冻结；`USING/EDITING × expanded/collapsed × locked/unlocked × runtime` 权限由 JS/Rust 纯函数解析。锁定的 EDITING 海报按 USING 权限运行，编辑态区分单击选中与明确启动，PAUSED/STOPPED 拒绝全部输入。

共用矩阵覆盖 8 个 active 组合、activeAnimated、PAUSED、STOPPED 和非法枚举；C-PCB-03 JS 专项 5/5、全量 JS 77/77、Rust 63/63 通过。新契约未接管现有 A0/Release 事件监听，因此没有改变已验收视觉和桌面输入。

C-PCB-04 已完成：正常 `transparent-default`、用户请求模板和内部 `minimal-fallback` 已由单一 JS/Rust 解析契约区分。16 场景共享矩阵覆盖正常、缺失/损坏/未知模板、非法持久 fallback、无效快照、恢复失败、fallback 自身失败和非法输入。

所有回退结果均保留原始请求键并声明 `persistenceMutation=none`；fallback 自身不可用时明确返回不可渲染，而不是静默重置或假装恢复。旧 JS 模板接口只保留兼容返回形状，选择决策已委托给新解析器。C-PCB-04 JS 专项 5/5、全量 JS 83/83、Rust 65/65 通过。

新模板解析器尚未接管当前 A0/Release 模板加载或恢复路径，因此没有改变已验收的视觉和桌面输入，也不需要本切片新增人工验收。

C-PCB-05 自动实施已完成：`poster-legacy-bridge.js` 成为 A0 localStorage、过渡 JSON 命令和两海报演示数据的唯一入口；正式 v1 快照不会被 legacy bridge 读取或覆盖。`poster-contract.js` 导出持久/运行时字段边界，`poster-contract-seal.test.mjs` 锁定 legacy 所有权、canonical `sync_host_surface` 和默认/fallback 常量唯一性。

[`C_PSS_INPUT_CONTRACT_V1.md`](../10_Plan/C_PSS_INPUT_CONTRACT_V1.md) 已形成冻结输入，但不激活 C-PSS。SQLite schema、revision/CAS、迁移事务、备份回滚、A0 额外演示海报处理和过渡桥退出时机仍归 C-PSS。

Release 核心回归发现 Tauri 外壳 HWND 已在桌面层，但 `com.allmight.desktopup-*` 可见内容 HWND 仍在普通应用层。原生宿主现枚举同进程可见顶层 ToolWindow 表面并整组锚定，首次显示后只做 `16/48/96ms` 三个有限收敛点。最终 PID `10636` 的 Z 序为外壳 `485`、可见内容 `486`、`Progman` `487`，无需外部补救即通过。

最终自动门禁：JS/文档 `89/89`、Markdown `113`、Rust `66/66`、Rust fmt 和 Release build 全部通过。

## 人工验收结论

1. 连续三轮 `Win+D` 后海报稳定回到桌面，无白条、裁切或持续闪烁；
2. 普通应用可以正常覆盖海报，再次 `Win+D` 后海报恢复桌面可见；
3. 空白穿透、卡片、标题栏拖动、锁定、收起/展开无回退，顶部位置不移动；
4. 控制中心可由托盘双击反复打开/关闭，海报显示和 Z 序不受影响；
5. Release 退出并重启后首次显示正常，且不覆盖普通应用。
2026-08-12，用户确认以上五项全部通过。
C-PCB-05 与整个 C-PCB 工作流据此关闭。发布认证范围仍仅为主屏单海报。

## 精确下一步

C-PSS 已于 2026-08-12 激活；后续实施以 [C-PSS 当前交接](../../PosterStateStore/30_Sync/CURRENT_HANDOFF.md) 为准。本目录仅保留 C-PCB 冻结输入与关闭证据。

## 禁止事项

- 不实现多海报创建 UI 或虚构多海报人工验证；
- 不把 `minimal-fallback` 写入用户快照；
- 不从 C-PCB 访问 WorkerW、Progman、HWND、DPI API 或输入钩子；
- 不新增旧两步 resize/region 命令调用；
- 未经用户明确要求不提交、不推送、不合并。
