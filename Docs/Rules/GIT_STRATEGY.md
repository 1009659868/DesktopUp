# Desktop-Up Git 与分支同步策略

最后检查：2026-08-04

状态：`ACTIVE`

本规则参考 TerrainGenerationPlugin 的主干式协作策略，并按 Desktop-Up 当前规模精简。

## 1. 分支模型

```text
main ───────────────────────────────────────→ 稳定发布基线
  └─ dev/mainline ──────────────────────────→ 日常集成线
       ├─ impl/<module>-a<stage>             → Codex 模块实施
       ├─ feat/<task>                        → 短期功能
       ├─ fix/<task>                         → 短期修复
       └─ chore/<task>                       → 构建和整理
```

| 分支 | 用途 | 规则 |
| --- | --- | --- |
| `main` | 已验证发布/阶段检查点 | 不直接开发，只接受 `dev/mainline` 的 `--no-ff` 合并 |
| `dev/mainline` | 日常集成 | 只合并已验证主题分支 |
| `impl/<module>-a<stage>` | 一个活跃 B 模块在某 A 阶段的实施 | 每个已验收 C 子项保持独立提交；未经要求不推送 |
| `feat/*`、`fix/*`、`chore/*` | 短期独立任务 | 原则上 7 天内合并并删除 |

当前实施分支：`impl/poster-redesign-V1`。该分支从 `impl/poster-redesign-a0` 的文档基线出发，将实际代码恢复到 M1 落地前状态后重新推进；废弃实现不得直接合回。

## 2. 创建与同步

开始模块实施：

```powershell
git fetch origin
git switch dev/mainline
git merge --ff-only origin/dev/mainline
git switch -c impl/poster-redesign-V1
```

实施分支同步集成线：

```powershell
git fetch origin
git switch impl/poster-redesign-V1
git merge dev/mainline
```

只有未推送的短期分支可以选择 rebase；发生协作或已推送后使用 merge 保留来源。禁止 force-push `main` 或 `dev/mainline`。

## 3. 合并策略

| 来源 → 目标 | 策略 |
| --- | --- |
| 主题/实施分支 → `dev/mainline` | 审阅后 `git merge --no-ff` |
| `dev/mainline` → `main` | 阶段门或发布验收后 `git merge --no-ff` |
| 从远端同步永久分支 | `git merge --ff-only` |

合并后的短期分支立即删除。长周期 `impl/*` 在对应 A 阶段或 B 模块完成后关闭。

## 4. 提交格式

```text
<type>: <简短祈使句>

<可选正文：原因、验证、对应 C 日志或阶段门>
```

允许类型：

- `feat:` 新能力；
- `fix:` 缺陷修复；
- `perf:` 性能优化；
- `refactor:` 不改变行为的重构；
- `docs:` 文档；
- `test:` 测试；
- `contract:` 契约或不变量冻结；
- `chore:` 工具、构建和仓库卫生。

一个验收门至少一个可审阅提交。实现、测试和对应 C 日志应在同一提交或相邻提交中，不混入无关修改。

## 5. 提交前检查

- 当前工作树没有无关用户修改；
- 前端检查和构建通过；
- Rust/Tauri 工具链可用时，相关 `cargo check`、测试和 Release 构建通过；
- 活跃 C 的日志、代码锚点和 handoff 已同步；
- `git status` 中只有本任务文件；
- 只暂存具体路径，不使用无差别 `git add -A`；
- 用户未要求时不 commit、不 push。

## 6. 发布检查点

只有以下条件满足才能从 `dev/mainline` 合并到 `main`：

- 当前 A 阶段门通过；
- 安装/升级/卸载矩阵达到该版本要求；
- 用户数据迁移与快捷方式恢复验证通过；
- A/B/C 文档状态一致；
- 对外产物哈希、许可证和测试报告齐备。

发布使用带注释标签，例如：

```powershell
git tag -a v0.1.0 -m "Desktop-Up 0.1.0"
```

## 7. 安全与仓库卫生

- 禁止 `git reset --hard`、`git clean -fd` 或回退用户改动；
- 构建产物、依赖缓存、运行时数据库和日志必须被 `.gitignore` 排除；
- 不提交真实用户快捷方式、绝对路径、诊断隐私数据或签名密钥；
- 二进制资源只有产品需要且来源/许可证明确时才提交；
- 大文件或主题资源市场后续采用独立分发，不把 Git 仓库当资源仓库。
