# TS-H01-D3-1 125% / 120 DPI

状态：`PASS — 5 / 5 COLD STARTS`

日期：2026-08-21

125% 档使用当前 x64 Release 完成五个独立冷启动，自动遥测 5/5 PASS，用户确认人工视觉 5/5 PASS。每轮至少覆盖标题拖动、右下缩放和完整收起/展开，并在达到最低动作次数后继续 1.5 秒稳定观察。

## DPI 权威结论

本档在预检中发现并修正了 DPI 来源假设：Windows 设置截图与 PMv2 进程的主屏系统 DPI 均证明主屏为 120，但 Explorer `Progman` 与嵌入桌面子 HWND 的 `GetDpiForWindow` 仍报告 96。最终模型因此使用 PMv2 进程的主屏系统 DPI 作为主屏逻辑投影权威，将 Shell parent/window DPI 仅记录为诊断字段。

三个预检 run 均保留：

1. 使用 Shell parent DPI：在创建前 fail-closed，错误地报告 expected mismatch；
2. 初建用系统 DPI、随后用自有 child HWND DPI：120 被错误降回 96，边界一致但 DPI contract FAIL；
3. 主屏系统 DPI 持续权威：605/605 样本 PASS，effective=120、window=96、shell parent=96 的差异被显式记录。

## 后续约束

本修正改变了二进制。旧 100% 档仍保留为历史 PASS，但最终四档统一汇总前，必须使用当前二进制补测 100% / 96 DPI，不能把旧二进制证据借给当前构建。
