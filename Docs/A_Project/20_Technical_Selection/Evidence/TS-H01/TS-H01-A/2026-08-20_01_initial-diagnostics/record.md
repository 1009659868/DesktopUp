# TS-H01-A 初始诊断探针记录

Evidence ID：`TS-H01-A-20260820-01`

状态：`PASS — TS-H01-A OBSERVABILITY SCOPE ONLY`

## 对应范围

- 阶段：TS-02 / TS-H01-A；
- 公共前置：TS-H01，不归属任何 UI/渲染候选；
- 相关矩阵：HG-BLD-01、HG-BLD-02、HG-DIAG-01 的窗口/Shell 观测基础；
- 本记录不判定 HG-Z、HG-IN、HG-RND 或完整 TS-H01 通过。

## 构建身份

- Probe schema：`desktopup.ts-h01-a.snapshot.v1`；
- Probe version：`0.1.0`；
- Release SHA-256：`66B8C26965CF09144A1319F5C419A71FA539B108E54BC8D94DCBC7C7FFBCDAE7`；
- 构建：Visual Studio 18 2026 / MSVC 19.50 / Windows SDK 10.0.26100.0 / x64 Release。
- 原始遥测：`snapshot.jsonl`，5 行，1,657,015 bytes；
- 原始遥测 SHA-256：`0A623E2049914C52F6B35BB3FE1EF4DE8305516CDDEEFE23371476B81043AB38`。

## 观测结果

- OS build：26100；
- Per-Monitor v2 设置成功，awareness 为 `PER_MONITOR_AWARE`；
- 当前 Shell 分类：`RAISED_PROGMAN`；
- `SHELLDLL_DefView`：1 个，直接父窗口为 `Progman`；
- 图标视图：1 个；
- 顶层 `WorkerW`：14 个；
- 首次快照相关桌面窗口：20 个；顶层窗口：482 个；
- 5 次、50 ms 间隔采样中 topology、Explorer generation、DefView 数量均保持一致；
- 未创建窗口、未重排 z-order、未调用 `SetParent`、未发送 `0x052C`。

## 裁决

TS-H01-A 的只读构建、JSONL 连续采样、Explorer 世代、拓扑分类以及 parent/owner/z-order/cloak/DPI/边界字段可用。允许进入 TS-H01-B 的有界宿主/层级实验。

当前事实只能说明本机是 raised Progman 拓扑，且 DefView 当时位于 Progman 子窗口 z-order 顶部；它不能证明把自有宿主插到其上方后仍满足 Win+D、普通程序层级或空白输入。
