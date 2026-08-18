# Desktop-Up V2 Git 与分支同步策略

最后检查：2026-08-18

状态：`ACTIVE`

沿用 V1 的主干式协作策略并适配 V2 仓库。

## 1. 分支模型

```text
main ───────────────────────────────────────→ 稳定发布基线
  └─ dev/mainline ──────────────────────────→ 日常集成线
       └─ impl/Refactor                     → V2 重构实施(当前)
       ├─ feat/<task> / fix/<task>          → 短期任务
```

| 分支 | 用途 | 规则 |
| --- | --- | --- |
| `main` | 已验证发布/阶段检查点 | 不直接开发,只接受 `dev/mainline` 的 `--no-ff` 合并 |
| `dev/mainline` | 日常集成 | 只合并已验证主题分支 |
| `impl/Refactor` | V2 重构实施 | 每个已验收 C 子项保持独立提交;未经要求不推送 |

V1 仓库(`G:\A_\Tools\DesktopUp`)保持只读追溯,其分支 `impl/poster-redesign-V1` 不再推进。

## 2. 提交格式

```text
<type>: <简短祈使句>

<可选正文:原因、验证、对应 C 日志或阶段门>
```

类型沿用 V1:`feat / fix / perf / refactor / docs / test / contract / chore`。

## 3. 提交前检查

- 活跃 C 的日志、代码锚点和 handoff 已同步;
- 用户未要求时不 commit、不 push;
- 构建产物、依赖缓存、运行时数据库和日志被 `.gitignore` 排除;
- 不提交真实用户数据、绝对路径或诊断隐私。

## 4. 发布检查点

同 V1:阶段门、安装/升级/卸载矩阵、快捷方式恢复验证、A/B/C 状态一致、产物哈希与测试报告齐备;发布使用带注释标签。
