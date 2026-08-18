# DesktopUP V2 落地推进计划 V1

状态：`ACTIVE`

建立日期：2026-08-18

适用分支：`impl/Refactor`（`G:\A_\Tools\DesktopUp_V2`）

设计输入：[`DesktopUP V2 完整需求画像 V1`](DESKTOPUP_V2_REQUIREMENTS_PROFILE_V1.md)、[`技术选型候选与评估框架 V1`](TECH_SELECTION_CANDIDATES_V1.md)（含用户三项裁决）

## 1. 文档职责

本文是 V2 唯一活跃的 A 层实施路线：负责阶段顺序、闸口（用户实机验证点）、B/C 工作流映射、交付节奏与完成定义。执行模式由用户裁决为**长任务自主推进、设闸口验证**：我连续推进各阶段，在每个闸口停下交付用户验证；每阶段产出文档、代码、编译产物与 git 快照（可随时回退）。

## 2. 推进原则

1. **证据先行**：渲染候选必须通过固定证据矩阵才算通过；未验证不宣布选定；
2. **闸口串联**：上游闸口未过，下游不开始（计划可提前准备）；
3. **用户数据分级开闸**：只读发现与仅添加先于任何文件移动；安全收纳必须晚于账本/保险库/故障注入；
4. **单一透明机制**：任何宿主方案只保留一种透明机制，禁止多机制叠加（V1 教训）；
5. **呈现可自检**：每个宿主阶段都带呈现健康自检，S1/S2/S3 类劣化必须可程序发现；
6. **提交即回退点**：每阶段完成一次 git 提交并推送 origin；闸口前的状态永远可还原；
7. **代码全重写**：V1 代码与测试仅作参考文档，不迁移。

## 3. 阶段与闸口

| 阶段 | 目标 | 关键工作流 | 闸口（用户验证） |
| --- | --- | --- | --- |
| V2-P0 | 渲染宿主选定 | 环境配置、C-QTP 原型（C-D2D 保底） | **闸口 A**：8 项证据矩阵实机 PASS/FAIL |
| V2-P1 | 桌面宿主重建 | 选定渲染器生产化、核心桩扩为真核心（状态/托盘/生命周期）、几何/命中/层级 | **闸口 B**：主屏单海报交互门（无白条/层级/穿透/Win+D） |
| V2-P2 | 控制中心与状态权威 | 控制中心重建、预览投影、单一状态权威、防回弹 | **闸口 C**：控制中心与三表面一致性验收 |
| V2-P3 | 可用 MVP | 正式 Item 仓库/投影、只读发现、仅添加、真实启动 | **闸口 D**：数据驱动海报 + 启动可用验收 |
| V2-P4 | 安全收纳 MVP | 保险库、两阶段账本、撤销、异常恢复、卸载恢复接口 | **闸口 E**：收纳/撤销/恢复闭环 + 故障注入 |
| V2-P5 | 主题平台 | 三模板 + fallback、schema 参数、素材槽、热切换、包导入导出 | **闸口 F1**：主题平台验收 |
| V2-P6 | 布局与活跃度 | 排名、缩放、弹性流、锚定、跳变 | **闸口 F2**：布局可解释/可撤销验收 |
| V2-P7 | 主题作者与 AI | 受限覆盖、导入导出闭环、BYOK AI | **闸口 F3**：安全与越权拒绝验收 |
| V2-P8 | 平台来源发现 | Steam/Epic/Xbox 只读发现、去重、人工修正 | 随闸口 F3 合并验收 |
| V2-P9 | 发布 | 安装器、独立卸载器、升级/迁移、净机矩阵、签名 | **闸口 G**：发布矩阵（必须用户全程参与） |

## 4. B/C 工作流映射

| B 模块（来自 B_MODULE_INDEX） | C 工作流（随阶段建立） | 状态 |
| --- | --- | --- |
| B-RND Renderer Selection | C-QTP（ACTIVE）、C-D2D（PLANNED）、C-WCC（PLANNED） | `ACTIVE — C-QTP` |
| B-FND Product Foundations | 随 V2-P1 建立（状态/命令/修订/生命周期/诊断契约） | `PENDING` |
| B-SRF Runtime Surfaces | 随 V2-P1/V2-P2 建立 | `PENDING` |
| B-ITM Content Item Model | 随 V2-P3 建立 | `PENDING` |
| B-LAY Activity and Layout | 随 V2-P6 建立 | `PENDING` |
| B-VIS Poster Visual System | 随 V2-P1/P5 建立 | `PENDING` |
| B-THM Theme Platform | 随 V2-P5 建立 | `PENDING` |
| B-AI AI Assistance | 随 V2-P7 建立 | `PENDING` |
| B-RLS Install and Release | 随 V2-P9 建立 | `PENDING` |

## 5. 交付节奏与回退

- 每个 C 任务完成即提交（`feat/fix/docs/contract` 规范提交信息），阶段末提交并 `git push origin impl/Refactor`；
- 闸口验证失败：记录失败项 → 修复或按计划转保底候选 → 重新提交 → 重验；
- 任何阶段可用 `git reset --hard <commit>` 回退，远端与本地双份快照。

## 6. 环境基线（2026-08-18 盘点）

| 组件 | 状态 | 位置 |
| --- | --- | --- |
| Rust 1.97.1 / cargo | ✓ | 系统 PATH |
| VS 18 Community（cl / vcvars） | ✓ | `C:\Program Files\Microsoft Visual Studio\18\Community` |
| CMake | ✓（winget 版） | 系统 PATH；VS 自带备用 |
| Ninja | ✓ | VS 自带：`...\CMake\Ninja\ninja.exe` |
| Qt 6.8.3 (msvc2022_64) | ✓ 已安装（aqt），按用户要求迁移至 F 盘 | `F:\Qt\6.8.3\msvc2022_64` |
| Python 3.12（aqt 载体） | ✓ 已安装（winget，系统托管路径） | `%LOCALAPPDATA%\Programs\Python\Python312` |
| 磁盘 | ✓ | F: 1208GB 空闲 |

**路径策略（用户要求，2026-08-18）**：所有下载的软件环境统一放入 `F:\`；Qt 已迁移至 `F:\Qt`；后续安装的工具链、签名证书、运行时依赖包与构建缓存一律放到 F 盘，不得写入 G:\A_\Tools 之外的杂散位置。

## 7. 完成定义

以画像 §28 十条为唯一完成定义；另加两条执行约束：

1. 所有 P0/P1 闸口均有用户实机证据；
2. 每个阶段的状态、日志、交接与 git 快照一致，可随时回退。

## 8. 修订记录

| 日期 | 版本 | 内容 |
| --- | --- | --- |
| 2026-08-18 | V1 | 建立 V2 落地推进计划：九阶段与七闸口、B/C 映射、交付/回退节奏、环境基线 |
