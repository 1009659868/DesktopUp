# QTS-04-03 实施日志（2026-08-27）

状态：`R2 IMPLEMENTED / AUTO 13 OF 13 PASS / INITIAL USER GATE FAILED / RETEST PENDING`

## 过程摘要

1. 用户确认 QTS-04-02 最新 Release 可见，关闭 renderer 用户桌面视觉门；
2. 复核 TS-H01-C 与 Qt 候选 R2 的 split-input 证据，冻结“透明 visual + sibling islands + Explorer blank”结构；
3. 扩展公共 contracts 和 `IDesktopHost`，让 pixels 与 hit regions 使用同一 projection revision 发布；
4. 建立 `Win32InputIslandSet`，实现区域校验、priority 排序、同身份原地更新、不同集合 replacement、capture 和语义事件；
5. 扩展 `NightOpsLayout`，让 input projection 使用 renderer 实际标题、按钮、卡片和边缘几何；
6. Runtime 增加 `--editing`、标题拖动、三方向缩放、5px 阈值、工作区夹紧、锁定、收起/展开和卡片选中反馈；
7. 更新 lifecycle/Host fixtures，并新增 split-input 自动矩阵；
8. 首轮 Release 构建成功，既有 11 项均保持 PASS；
9. 新增矩阵最初在 repeated present 后连锁失败，逐层排除 island create/batch/z-order；
10. 证据确认 `InvalidateRect/UpdateWindow` 第二轮返回成功但没有新的 `WM_PAINT`；
11. Host 改为每次 present 主动向 visual DC 提交完整像素，同时保留 `WM_PAINT` 作为系统重绘入口；
12. split-input 初版单测转绿，完整 Release 构建和 12/12 CTest PASS；
13. 执行 2.5 秒 `--editing` Runtime 预检，退出码 0，ledger ownedResourceCount=0；
14. 首次人工门确认“标题无法拖动”和“右下角缩过顶部后报错消失”，QTS-04-03 不关闭；
15. 定位旧 present 端口未携带 screen bounds，Runtime 的 left/top 在 Host 边界丢失；
16. 定位 Runtime 240 px 与 renderer 300 px 阈值冲突，可能在同步 move 中撤销持有 capture 的 resize island；
17. present 改为同次接收 screen bounds、frame、pixels 和 hit regions，Host 成功后才提交 requested bounds；
18. 移除 Runtime 临时 GeometryGesture，接入 Core GeometryGestureSession 和 Qt R2 的 320×420/固定边/工作区钳制语义；
19. live present 失败增加上一有效几何回滚，避免失败后永久隐藏；
20. 新增 qts04.geometry-gesture，并扩展真实 HWND 测试覆盖 visual/island 位移和 capture 稳定性；
21. 重新配置、构建 Release，完整 CTest 13/13 PASS；
22. 未执行 commit、push、merge或清理用户已有工作树。

## 本轮未扩大范围

- 没有把六张 fixture 接成正式 Item；
- 没有把交互状态写入正式 StateStore；
- 没有实现控制中心/托盘联动；
- 没有宣称 Win+D、文件选择器、Explorer 人工重启或显示变化通过；
- 没有改变普通窗口 > 海报 > 图标 > 壁纸的层级合同。

## 下一动作

运行 R2 最新 Release 重新执行 QTS-04-03 用户单海报输入门，重点确认连续标题拖动、右下角越过顶部时保持 320×420 且不消失、释放后可再次缩放。全部人工项通过后才关闭 QTS-04-03。
