# TS-H01-C Split Input Preflight

状态：`PASS — RAISED_PROGMAN SPLIT INPUT PATH`

## 结论

当前 `RAISED_PROGMAN` layered 路径已成功拆分为：

1. 一张带 `WS_EX_LAYERED | WS_EX_TRANSPARENT` 的完整视觉表面；
2. 标题、Item、操作按钮三个独立 layered 交互岛。

正式运行得到 50/50 自动样本通过。视觉表面始终位于三个交互岛下方、`SHELLDLL_DefView` 上方，没有 TopMost，也没有收到任何鼠标按钮/菜单消息。Progman 内部预检把空白测试点路由到 `SHELLDLL_DefView`，三个交互点分别路由到自己的交互岛。

用户随后完成 120 秒人工门并确认六项全部通过，因此 TS-H01-C 的当前 `RAISED_PROGMAN` split-input 路径正式判定为 PASS。自动预检仍不单独等于跨进程输入证明；最终结论由原始日志与用户人工观察共同组成。

## 自动证据

- Build ID：`TS-H01-C-20260820-01-SPLIT-INPUT`
- Binary SHA-256：`02C45000B32705305B05D7FB1496292F8E0507A6416DF6905C31F743409CCCB5`
- 样本：`50 / 50 PASS`
- Visual ex-style：`0x80800A0`
- Visual child index：`3`
- DefView child index：`4`
- Visual pointer messages：`0`
- Log SHA-256：`08049625895B598F014B8479C5FF4870972EB3729C3104AA8711516121A966C2`

## 为什么不用 HTTRANSPARENT

`HTTRANSPARENT` 的官方语义只会继续搜索同一线程中的下层窗口，而 Explorer 属于另一个进程/线程，因此它不能作为本项目空白穿透的证明。本探针使用 layered window 的 `WS_EX_TRANSPARENT` 官方命中行为，并通过独立 sibling HWND 恢复明确的海报交互岛。

## 人工确认结果

- PASS：空白左拖为 Explorer 原生桌面框选；
- PASS：空白右键为 Windows 桌面菜单；
- PASS：空白覆盖下的快捷方式可单击选择、双击启动；
- PASS：三个交互岛分别响应且不激活/抬高海报；
- PASS：普通程序仍覆盖整张探针；
- PASS：定时结束后四个自有 HWND 全部清理。

人工运行日志 `manual-input-run.jsonl` 另记录：

- `1200 / 1200` 自动样本持续 PASS；
- title click events：`14`；
- item click events：`10`；
- action click events：`4`；
- unexpected visual input：`0`；
- log SHA-256：`FE8F2B8DF0BC9CF14D81F8A94861E6734C77BB1072058C848D17E32102FC7167`。

TS-H01-C 已关闭，下一阶段为 TS-H01-D。
