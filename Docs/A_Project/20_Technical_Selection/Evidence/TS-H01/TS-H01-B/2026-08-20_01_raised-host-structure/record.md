# TS-H01-B-20260820-01 — raised host structure

状态：`AUTOMATED STRUCTURE PASS / VISUAL FAIL RECORDED BY TS-H01-B-20260820-02`

日期：2026-08-20

## 本记录能证明什么

在本机 Windows 11 build 26100 的 `RAISED_PROGMAN` 拓扑中，一个无 owner、
非 TopMost、无 caption 的原生 child HWND 可以直接创建在图标宿主
`Progman` 中，并连续保持在 `SHELLDLL_DefView` 上方。

这支持当前宿主假设，但尚未完成 TS-H01-B，因为“普通程序始终视觉覆盖
探针、探针视觉覆盖图标”仍需人工观察，traditional WorkerW 拓扑也没有在
本机出现。

## 正式运行结果

- 进程退出码：`0`；
- 运行时长：4 秒；
- JSONL：84 行，其中结构采样 80 条；
- 拓扑：`RAISED_PROGMAN`；
- 自有宿主 parent：`Progman 0x3221344`；
- `SHELLDLL_DefView`：`0x263B0AA0`；
- 80/80 采样中宿主 child index 为 0，DefView child index 为 1；
- 80/80 采样中宿主位于 DefView 上方；
- 80/80 采样中 owner 为 null，未最小化、未 cloak、未 TopMost；
- 宿主和 DefView 上方可见普通顶层窗口计数始终为 32；
- 结束时自有 HWND 正常销毁；
- JSONL SHA-256：
  `3F530C5C71A038AE0DA2FFBEF144668E9C2C96EC3E933E75CE6FC9DDAB4BF0B3`；
- Release 探针 SHA-256：
  `6DBCD3209547DEEADEC6EE02E882778E1FE5B158A309D34CC0B49761701D8941`。

## 本记录不能证明什么

- 不能仅凭 child/parent 结构宣称肉眼无任何一帧覆盖普通窗口；
- 没有验证空白点击、框选、右键菜单或桌面图标操作；
- 没有按 Win+D、任务栏显示桌面或触控板手势；
- 没有重启 Explorer；
- 没有验证 traditional WorkerW 拓扑；
- 没有验证透明、圆角、DPI 改变、拖动、缩放或渲染框架；
- 没有产生任何候选框架胜出结论。

## 判定

`TS-H01-B raised 自动结构子门：PASS`。

`TS-H01-B 完整门：PENDING`，原因是人工视觉层级门尚未执行，traditional
拓扑保持 `UNKNOWN_NOT_PRESENT`。人工门步骤见 `reproduce.md`。

通过人工视觉门后，下一项才是 `TS-H01-C 空白穿透与交互岛`；不得把本探针
当前会接管矩形区域输入的行为当作产品方案。
