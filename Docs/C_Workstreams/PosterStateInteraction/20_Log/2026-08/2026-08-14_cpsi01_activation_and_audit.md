# C-PSI-01 激活与现状审计

日期：2026-08-14

状态：`COMPLETE — C-PSI-02 ACTIVE`

## 1. 目标

激活 Poster State and Interaction，依据 C-PCB 冻结的状态/权限契约和 C-LST 已关闭表面基线，逐项判断现有实现是 PASS、PARTIAL 还是 GAP。只有 GAP 才进入生产代码修改。

## 2. 审计对象

- `USING/EDITING × EXPANDED/COLLAPSED × locked/unlocked` 权限矩阵；
- SQLite 正式业务状态与原生 RuntimeSnapshot 的所有权；
- 自制标题栏、控制按钮、Item 命中与透明空白；
- 控制中心编辑投影、移动/缩放逆投影和 CAS 失败保留；
- 240px 最小宽/高、45% 最大宽及工作区最大高；
- 暂停、显隐、控制中心释放/重建与真实海报同步。

## 3. 当前边界

- 不重新打开 C-DRP、C-PCB、C-PSS、C-RLC 或 C-LST；
- 不实现 Item 仓库、真实启动、模板参数权威、多海报或多显示器认证；
- 不扫描或移动用户桌面文件；
- 当前工作树已有未提交实现，审计和修复必须保留这些改动。

## 4. 审计裁决

- 冻结 [`C_PSI_AUDIT_MATRIX_V1.md`](../../10_Plan/C_PSI_AUDIT_MATRIX_V1.md)，记录 7 组 PASS/ABSORBED 和 7 个真实 GAP；
- 权限纯函数、SQLite/CAS、正式状态投影、控制中心坐标投影、原生顶部/层级、命中与生命周期入口均直接吸收；
- 真实缺口是 UI 权限接入、pending mutation 保留、暂停原因分流、动态尺寸约束、非法编辑态 fail-closed、编辑内容区拖动，以及标题文字/拖动阈值/海报菜单入口；
- 正式 Item 数据、启动、排序和 Item 菜单命令明确留给 C-CIM/C-LCH/C-LAR；视觉参数回弹继续归 Template Platform 参数权威。

## 5. 关键冲突裁决

早期 C-PCB 纯矩阵把全部 `PAUSED` 解释为禁止输入，但已通过的 C-DRP-05 Release 验收明确要求“暂停动态效果”时海报不隐藏或闪烁，锁定/收起按钮仍可使用。C-PSI 将其裁决为两个维度：手动效果暂停只控制绘制；系统不可见、锁屏、显示器关闭、退出和宿主未就绪才禁用输入。C-PSI-02 必须显式实现该分流。

## 6. 生产代码与验证边界

本任务只做审计和文档冻结，没有修改生产代码，因此不新增 Release 人工验证。C-PSI-02 成为唯一活动任务，只实现矩阵 GAP-01..03。
