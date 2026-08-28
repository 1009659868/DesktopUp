# QTS-04-02 实施日志（2026-08-27）

状态：`CLOSED — AUTO PASS / USER VISUAL PASS 2026-08-27`
## 过程摘要

1. 读取 QTS-04-01 交接和冻结的单海报视觉需求，确认本阶段只负责 renderer/pixels；
2. 审计旧最小 renderer，确认整幅半透明 `fillRect` 会产生矩形外包络；
3. 建立 `NightOpsScene`、六项 fixture、纯视觉 presentation/lock 状态和 `NightOpsLayout`；
4. 将画布改为全透明 ARGB32 premultiplied，仅在圆角 QPainterPath 内绘制玻璃背景、装饰和内容；
5. 绘制 NIGHT OPS 标题、状态、六张卡片、footer、锁定与收起 glyph；
6. 建立 420×620 expanded 与 420×60 collapsed 的参考几何，保持 header x/y/width 不变；
7. Runtime 默认 frame 使用 renderer 的冻结参考尺寸；
8. 新增像素、几何、确定性和 buffer 生命周期夹具；
9. 首轮编译修正一处括号错误及测试字段名与合同 `bounds` 的一致性；
10. Release 构建成功，全量 11/11 CTest PASS；
11. 从同一测试二进制导出 `qts04-night-ops.png` 并完成本地视觉审阅；
12. 保留用户实际桌面视觉门，不提前关闭 QTS-04-02。

## 未扩大范围

- 没有修改 DesktopHost、manifest、Explorer target 或 z-order；
- 没有实现 input islands、桌面空白命中或拖动/缩放；
- 没有执行 Win+D、Explorer 重启、显示变化或完整 P0；
- 没有提交、推送、合并或清理用户已有工作树。

## 下一动作

用户已确认最新版 Release 可见并满足视觉门；QTS-04-02 关闭，交接到 QTS-04-03 split input islands。