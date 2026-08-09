# C-DRP 计划激活与路线重排

日期：2026-08-09

状态：`COMPLETE — DOCUMENT ACTIVATION`

## 目标

在 C-DHF 关闭后建立唯一活跃 C 工作流，并修正原路线中“完整模板平台早于真实可用价值”、启动与恢复耦合、状态仓库和安装基线缺少显式阶段门的问题。

## 已完成事实

- 唯一活跃链切换为 `V1-P1 → B-DRF → C-DRP`；C-DHF 保留为完成输入，不再承担当前实施任务；
- 建立 C-DRP 的 Catalog/Plan/Log/Sync 完整目录和六项可验证任务；
- Poster Core 新增 `C-PSS Poster State Store`，明确版本化 SQLite、迁移、幂等测试和过渡 JSON 退出门；
- Collection and Launch 将原 `C-LRC` 拆为 `C-LCH Real Launch` 与 `C-RCV Recovery and Uninstall Interface`；
- “只读发现 → 仅添加 → 真实启动”提前形成 Usable MVP；真实文件移动仍晚于保险库、账本和故障注入；
- 完整模板平台移动到 Safe Collection MVP 之后，主题、AI 和视觉扩展继续后置；
- C-IBC 被纳入早期契约顺序，但最终 setup/uninstall 仍由发布候选门封板；
- 增加 Release 空闲渲染、CPU、内存和 schema 迁移预算。

## 验证

本记录只证明计划和文档结构已经调整，不证明任何新运行时代码完成。

- `git diff --check` 通过；
- `pnpm run check` 通过：32 项 Node/契约测试全部通过；
- 文档链接检查通过：87 个 Markdown 文件无断链；
- 全部 C `WORKSTREAM_PLAN.md` 中只有 C-DRP 标记为 `ACTIVE`。

## 后继

执行 C-DRP-01 宿主接口与所有权契约；完成前不同时激活 C-PCB。
