# QTS-04-01 实施日志（2026-08-27）

状态：`CLOSED — REAL HOST + RELEASE 10/10 CTEST PASS`

## 过程摘要

1. 读取 QTS-03 交接、DesktopHost 冻结合同、TS-H01 证据和现有生产骨架；
2. 扩展合同为物理 PixelRect、premultiplied frame view、Host topology/state/observation 与 maintenance result；
3. 实现 raised-Progman target 发现、Explorer PID/唯一性校验、Host-owned layered visual 和首帧隐藏门；
4. 实现 message-only coordinator、WinEvent create/destroy hook、generation replacement、Runtime recovery callback 与同步退出；
5. 建立真实 attach/reconnect 自动夹具；首次运行在发现正确 Progman/DefView/IconView 后仍于 `CreateWindowExW` 失败；
6. 逐项排除初始隐藏、屏幕位置、Qt 启动延迟、透明 style 和窗口过程；诊断确认失败发生在 `WM_NCCREATE` 之前；
7. 对比同机可通过的 TS-H01-B 和 Qt R2，定位到新生产 executable 缺少 Windows 8+ compatibility manifest；
8. 嵌入 Host-capable manifest，并在 adapter/夹具中显式验证资源；真实 layered child 随即创建成功；
9. 在首帧前建立隐藏 sibling Z 序，随后完成首帧、四类 bounds、旧 HWND 销毁、新 epoch、一次恢复通知和资源归零断言；
10. 过滤非 `OBJID_WINDOW/CHILDID_SELF` WinEvent，补齐重复窗口类注册和失败路径资源清理；
11. 最终 Release 全量 10/10 CTest PASS；1.2 秒有界原生运行 exit 0，进程残留 0。

## 关键结论

- 初轮 `CreateWindowExW` 失败的根因是 Host-capable executable 缺少 Windows 8+ manifest，不是 Qt、DPI、窗口位置或首帧时序；
- layered child compatibility 是构建/进程能力合同，不能只藏在旧探针中；
- QTS-04-01 证明真实 attach/reconnect 类别，不证明 NIGHT OPS 视觉、split input、Win+D 或 Explorer 人工重启矩阵。

## 未扩大范围

- 没有重启 Explorer；
- 没有执行 Win+D 或完整人工 P0；
- 没有实现 NIGHT OPS、input islands、托盘、Item、主题或 AI；
- 没有提交、推送或合并 Git；
- 没有覆盖用户已有未提交改动。
