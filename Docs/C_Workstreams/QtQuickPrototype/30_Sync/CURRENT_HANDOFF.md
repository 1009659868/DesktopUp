# C-QTP 当前交接

最后更新：2026-08-18

状态：`闸口 A — 自动化证据 5/8 PASS,等待用户实机验证`

## 当前

C-QTP 五任务全部完成(自动化部分)。证据见:
- `20_Log/2026-08/2026-08-18_cqtp01_02_skeleton_stdio_ipc.md`(骨架/IPC/构图)
- `20_Log/2026-08/2026-08-18_cqtp03_input_passthrough.md`(穿透与命中)
- `20_Log/2026-08/2026-08-18_cqtp05_evidence_matrix.md`(8 项矩阵 → 闸口 A 交付)

关键事实:stdio IPC 1:1 拓扑;WS_EX_TRANSPARENT 单穿透机制(样式位切换已读回验证);呈现自检 = 强制产帧 + 品红标记扫描(正向 256px PASS / 隐藏态 FAIL);0-FPS 证据 = frameSwapped 计数仅随自检增长;核心 11MB / 渲染端 72MB / CPU ≤0.9%;崩溃有限重建 3 次实证(2 真实 + 1 主动);主题热切换 0-1ms;windeployqt 载荷 21 文件 54.9MB。

已修复:OnceLock 未初始化、frameSwapped 内 grabWindow 崩溃(0xC0000374/0xC0000005)、隐藏态陈旧缓冲误报。沙箱边界:SendInput 被 UIPI 阻断,程序化输入验证改用直接样式位施加。

## 下一动作

1. **用户闸口 A 实机验证**(清单见证据矩阵文档尾部):穿透、拖动 20 轮、不打扰/Win+D/Explorer;
2. 全过 → 选定决策文档草稿 → V2-P1(宿主重建 + 真实核心);
3. 任一失败 → 淘汰记录,激活 C-D2D。

## 边界

不写产品功能代码;不迁移 V1 代码;原型不进入发布路径;windeployqt 净机路径与 DLL 裁剪并入正式宿主阶段。
